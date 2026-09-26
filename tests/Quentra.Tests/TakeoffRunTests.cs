using System.Collections.Immutable;
using System.Security.Cryptography;
using Quentra.Application;
using Quentra.Core;
using Quentra.Infrastructure;
using static Quentra.Tests.TakeoffTests;

namespace Quentra.Tests;

public class TakeoffRunTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static QuantityOverride Override(TakeoffRun run, string objectId, OverrideField field, double original, double replacement, string id = "o1") =>
        new(id, objectId, field, original, replacement, "Field-verified dimension", "Synthetic Reviewer", At, run.SnapshotSha256);

    private static TakeoffRun AcceptAll(TakeoffRun run, bool partial = true) =>
        TakeoffJson.Accept(run, "Synthetic Reviewer", "Checked against fixture", partial, TakeoffJson.WarningCodes(run).ToImmutableArray(), At);

    [Fact]
    public void RunRoundTripIsByteIdentical()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var saved = TakeoffJson.Serialize(run);
        Assert.Equal(saved, TakeoffJson.Serialize(TakeoffJson.Replay(saved)));
        Assert.Equal(2, TakeoffJson.SchemaVersion(saved));
    }

    [Fact]
    public void InventoryOrderDoesNotChangeHashes()
    {
        var s = Fixture();
        var reordered = s with
        {
            Model = s.Model with { Frames = s.Model.Frames.Reverse().ToImmutableArray() },
            Areas = s.Areas.Reverse().ToImmutableArray(),
            Stories = s.Stories.Reverse().ToImmutableArray(),
            Metadata = s.Metadata.Reverse().ToImmutableArray(),
            Reinforcement = s.Reinforcement.Reverse().ToImmutableArray()
        };
        var a = TakeoffJson.Calculate(s); var b = TakeoffJson.Calculate(reordered);
        Assert.Equal(a.SnapshotSha256, b.SnapshotSha256);
        Assert.Equal(a.CalculationSha256, b.CalculationSha256);
        Assert.Equal(a.PackageSha256, b.PackageSha256);
    }

    [Theory]
    [InlineData("\"value\": 200", "\"value\": 250")]
    [InlineData("\"ratio\": 150", "\"ratio\": 160")]
    [InlineData("\"massKg\": 42.39", "\"massKg\": 42.4")]
    [InlineData("\"storyId\": \"L2\"", "\"storyId\": \"L1\"")]
    public void TamperedRunsFailReplay(string before, string after)
    {
        var json = TakeoffJson.Serialize(TakeoffJson.Calculate(Fixture()));
        Assert.Contains(before, json);
        Assert.Throws<InvalidDataException>(() => TakeoffJson.Replay(json.Replace(before, after)));
    }

    [Fact]
    public void ThicknessOverrideRecalculatesAndIsRecorded()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var changed = TakeoffJson.AddOverrides(run, [Override(run, "S1", OverrideField.AreaThicknessM, .2, .25)]);
        var slab = changed.Result.Elements.Single(x => x.ObjectId == "S1");
        Assert.Equal(6, slab.GrossM3!.Value, 9);
        Assert.Equal(run.SnapshotSha256, changed.SnapshotSha256);
        Assert.NotEqual(run.CalculationSha256, changed.CalculationSha256);
        // Raw evidence is kept; the override applies only to the effective model.
        Assert.Equal(200, changed.Snapshot.Areas.Single(x => x.ObjectId == "S1").Thickness.Value);
        Assert.Single(changed.Overrides);
        Assert.Equal(TakeoffJson.Serialize(changed), TakeoffJson.Serialize(TakeoffJson.Replay(TakeoffJson.Serialize(changed))));
    }

    [Fact]
    public void FrameDimensionOverrideMakesDemandStale()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var changed = TakeoffJson.AddOverrides(run, [Override(run, "B1", OverrideField.FrameDepthM, .6, .7)]);
        Assert.Equal(1.26, changed.Result.Elements.Single(x => x.ObjectId == "B1").GrossM3!.Value, 9);
        Assert.Null(changed.Result.Steel.Single(x => x.ObjectId == "B1" && x.Component == "LongitudinalTop").MassKg);
        Assert.NotNull(changed.Result.Steel.Single(x => x.ObjectId == "B1" && x.Component == "LongitudinalBottom").MassKg);
    }

    [Fact]
    public void ExclusionMovesElementOutOfScopeAndMarksRunPartial()
    {
        var complete = TakeoffJson.Calculate(WithMetadata(Fixture(), "B1", m => m with { RequiredSteelComponents = ["LongitudinalTop", "LongitudinalBottom"] }));
        Assert.False(TakeoffJson.IsPartial(complete));
        var excluded = TakeoffJson.AddOverrides(complete, [Override(complete, "W1", OverrideField.Excluded, 0, 1)]);
        Assert.Equal(QuantityStatus.OutOfScope, excluded.Result.Elements.Single(x => x.ObjectId == "W1").Status);
        Assert.Contains(excluded.Result.Elements.Single(x => x.ObjectId == "W1").Warnings, x => x.Code == "USER_EXCLUDED");
        Assert.Equal(7.608, excluded.Result.Summary.KnownGrossM3, 9);
        Assert.True(TakeoffJson.IsPartial(excluded));
        var restored = TakeoffJson.AddOverrides(excluded, [Override(excluded, "W1", OverrideField.Excluded, 1, 0, "o2")]);
        Assert.Equal(complete.Result.Summary, restored.Result.Summary);
        // A reversed exclusion leaves nothing excluded, so the run is complete again.
        Assert.False(TakeoffJson.IsPartial(restored));
        Assert.Equal("Accepted", TakeoffJson.ReviewStatus(AcceptAll(restored, partial: false)));
    }

    [Theory]
    [InlineData("stale original")]
    [InlineData("wrong snapshot")]
    [InlineData("frame field on area")]
    [InlineData("duplicate id")]
    [InlineData("nonpositive")]
    [InlineData("missing author")]
    public void InvalidOverridesAreRejected(string name)
    {
        var run = TakeoffJson.Calculate(Fixture());
        var valid = Override(run, "S1", OverrideField.AreaThicknessM, .2, .25);
        ImmutableArray<QuantityOverride> changes = name switch
        {
            "stale original" => [valid with { OriginalValue = .3 }],
            "wrong snapshot" => [valid with { SnapshotSha256 = new string('0', 64) }],
            "frame field on area" => [valid with { Field = OverrideField.FrameWidthM }],
            "duplicate id" => [valid, valid with { OriginalValue = .25, ReplacementValue = .3 }],
            "nonpositive" => [valid with { ReplacementValue = 0 }],
            "missing author" => [valid with { Author = "" }],
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        Assert.Throws<ArgumentException>(() => TakeoffJson.AddOverrides(run, changes));
    }

    [Fact]
    public void PartialRunNeedsExplicitPartialAcceptance()
    {
        var run = TakeoffJson.Calculate(Fixture());
        Assert.True(TakeoffJson.IsPartial(run));
        Assert.Equal("Draft", TakeoffJson.ReviewStatus(run));
        Assert.Throws<ArgumentException>(() => AcceptAll(run, partial: false));
        var accepted = AcceptAll(run);
        Assert.Equal("AcceptedPartial", TakeoffJson.ReviewStatus(accepted));
        Assert.Equal(TakeoffJson.Serialize(accepted), TakeoffJson.Serialize(TakeoffJson.Replay(TakeoffJson.Serialize(accepted))));
    }

    [Fact]
    public void CompleteRunIsAccepted()
    {
        var run = TakeoffJson.Calculate(WithMetadata(Fixture(), "B1", m => m with { RequiredSteelComponents = ["LongitudinalTop", "LongitudinalBottom"] }));
        Assert.Equal("Accepted", TakeoffJson.ReviewStatus(AcceptAll(run, partial: false)));
    }

    [Fact]
    public void AcceptanceRequiresAllWarningsAcknowledged()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var codes = TakeoffJson.WarningCodes(run).Where(x => x != "PARTIAL_STEEL").ToImmutableArray();
        Assert.Throws<ArgumentException>(() => TakeoffJson.Accept(run, "Synthetic Reviewer", "note", true, codes, At));
    }

    [Fact]
    public void UnapprovedPolicyBlocksAcceptance()
    {
        var s = Fixture();
        var run = TakeoffJson.Calculate(s with { Policy = s.Policy with { ApprovedBy = null, ApprovedAt = null } });
        Assert.Throws<ArgumentException>(() => AcceptAll(run));
    }

    [Fact]
    public void OverrideAfterAcceptanceReturnsRunToDraft()
    {
        var accepted = AcceptAll(TakeoffJson.Calculate(Fixture()));
        var changed = TakeoffJson.AddOverrides(accepted, [Override(accepted, "S1", OverrideField.AreaThicknessM, .2, .25)]);
        Assert.Equal("Draft", TakeoffJson.ReviewStatus(changed));
        Assert.Single(changed.ReviewHistory);
        Assert.Equal(TakeoffJson.Serialize(changed), TakeoffJson.Serialize(TakeoffJson.Replay(TakeoffJson.Serialize(changed))));
    }

    [Fact]
    public async Task ExportWritesVerifiedPackageAndRefusesExistingDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), "quentra-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var run = TakeoffJson.Calculate(Fixture());
            var destination = Path.Combine(root, "report");
            await ReportExporter.ExportAsync(run, destination);
            var files = Directory.GetFiles(destination).Select(Path.GetFileName).ToHashSet();
            Assert.Contains("run.json", files);
            Assert.Contains("report.xlsx", files);
            Assert.Contains("manifest.json", files);
            Assert.Contains("Summary.csv", files);
            Assert.Contains("Story_Allocations.csv", files);
            var manifest = TakeoffJson.Parse<ExportManifest>(await File.ReadAllTextAsync(Path.Combine(destination, "manifest.json")));
            Assert.Equal(run.CalculationSha256, manifest.CalculationSha256);
            Assert.Equal("Draft", manifest.ReviewStatus);
            Assert.Equal(files.Count - 1, manifest.FilesSha256.Count);
            foreach (var (name, hash) in manifest.FilesSha256)
                Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(destination, name)))).ToLowerInvariant());
            Assert.Equal(run.CalculationSha256, TakeoffJson.Replay(await File.ReadAllTextAsync(Path.Combine(destination, "run.json"))).CalculationSha256);

            await Assert.ThrowsAsync<IOException>(() => ReportExporter.ExportAsync(run, destination));
            Assert.Single(Directory.GetDirectories(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CancelledExportLeavesNoPartialPackage()
    {
        var root = Path.Combine(Path.GetTempPath(), "quentra-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ReportExporter.ExportAsync(TakeoffJson.Calculate(Fixture()), Path.Combine(root, "report"), cancel.Token));
            Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+1", "'+1")]
    [InlineData("-2", "'-2")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("  =x", "'  =x")]
    [InlineData("B300x600", "B300x600")]
    public void CsvTextCannotBecomeAFormula(string input, string expected) =>
        Assert.Equal(expected, ReportExporter.SafeText(input));

    [Fact]
    public void CsvQuotesAndKeepsNumbersInvariant()
    {
        var csv = ReportExporter.Csv(new("T", ["A", "B"], [["say \"hi\"", 1.5], [null, -0.25]]));
        Assert.Equal("\"A\",\"B\"\r\n\"say \"\"hi\"\"\",\"1.5\"\r\n\"\",\"-0.25\"\r\n", csv);
    }

    [Fact]
    public void ExcludedElementsRaiseNoSteelWarning()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var excluded = TakeoffJson.AddOverrides(run, [Override(run, "C2", OverrideField.Excluded, 0, 1)]);
        var steel = excluded.Result.Steel.Single(x => x.ObjectId == "C2");
        Assert.Null(steel.MassKg);
        Assert.Empty(steel.Warnings);
        Assert.Contains("USER_EXCLUDED", TakeoffJson.WarningCodes(excluded));
    }

    [Fact]
    public void AcknowledgingCodesTheRunDoesNotRaiseIsRejected()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var codes = TakeoffJson.WarningCodes(run).Append("MADE_UP").ToImmutableArray();
        var e = Assert.Throws<ArgumentException>(() => TakeoffJson.Accept(run, "Synthetic Reviewer", "note", true, codes, At));
        Assert.EndsWith("MADE_UP", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunsLargerThanTheSnapshotLimitCanBeRead()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quentra-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, new string(' ', (int)SnapshotJson.MaximumSnapshotBytes + 1));
            await Assert.ThrowsAsync<InvalidDataException>(() => SnapshotJson.ReadAsync(path, SnapshotJson.MaximumSnapshotBytes));
            Assert.Equal(SnapshotJson.MaximumSnapshotBytes + 1, (await SnapshotJson.ReadAsync(path, SnapshotJson.MaximumRunBytes)).Length);
        }
        finally { File.Delete(path); }
    }
}
