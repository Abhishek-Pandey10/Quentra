using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
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
        TakeoffJson.Accept(run, "Synthetic Reviewer", "Checked against fixture", partial, TakeoffJson.RequiredAcknowledgements(run).ToImmutableArray(), At);

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
        var top = changed.Result.Steel.Single(x => x.ObjectId == "B1" && x.Component == "LongitudinalTop");
        Assert.Null(top.MassKg);
        // The warning names the override that caused the steel to drop out.
        Assert.StartsWith("Not counted: override o1 (depth 600 mm → 700 mm) changed B1's section", top.Warnings.Single().Message, StringComparison.Ordinal);
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
    [InlineData("same value")]
    [InlineData("same value within tolerance")]
    [InlineData("already excluded")]
    [InlineData("include when included")]
    [InlineData("far too thick")]
    [InlineData("far too thin")]
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
            "same value" => [valid with { ReplacementValue = .2 }],
            "same value within tolerance" => [valid with { ReplacementValue = .2 + 1e-7 }],
            "already excluded" => [Override(run, "W1", OverrideField.Excluded, 0, 1), Override(run, "W1", OverrideField.Excluded, 1, 1, "o2")],
            "include when included" => [Override(run, "W1", OverrideField.Excluded, 0, 0)],
            "far too thick" => [valid with { ReplacementValue = 1e300 }],
            "far too thin" => [valid with { ReplacementValue = .001 }],
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
    public async Task InputsOverTheirLimitAreRefused()
    {
        // The real limits are hundreds of MiB; the check is the same at any size.
        const long limit = 1024 * 1024;
        var path = Path.Combine(Path.GetTempPath(), $"quentra-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, new string(' ', (int)limit + 1));
            await Assert.ThrowsAsync<InvalidDataException>(() => SnapshotJson.ReadAsync(path, limit));
            Assert.Equal(limit + 1, (await SnapshotJson.ReadAsync(path, 2 * limit)).Length);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RunLimitCoversARunFromTheLargestSnapshot()
    {
        // Indented runs reach about four times a compact snapshot, and a run is read as one .NET string.
        Assert.True(SnapshotJson.MaximumRunBytes >= 4 * SnapshotJson.MaximumSnapshotBytes);
        Assert.True(SnapshotJson.MaximumRunBytes < Array.MaxLength);
    }

    [Fact]
    public async Task RepeatedExportsAreByteIdenticalAndReadable()
    {
        var root = Path.Combine(Path.GetTempPath(), "quentra-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var run = TakeoffJson.Calculate(Fixture());
            await ReportExporter.ExportAsync(run, Path.Combine(root, "a"));
            await Task.Delay(2100); // Zip timestamps have two-second resolution.
            await ReportExporter.ExportAsync(run, Path.Combine(root, "b"));
            foreach (var file in Directory.GetFiles(Path.Combine(root, "a")).Select(Path.GetFileName))
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(root, "a", file!)), await File.ReadAllBytesAsync(Path.Combine(root, "b", file!)));
            using var workbook = new ClosedXML.Excel.XLWorkbook(Path.Combine(root, "a", "report.xlsx"));
            // One sheet per table, plus the Contents sheet.
            Assert.Equal(ReportExporter.Tables(run).Count + 1, workbook.Worksheets.Count);
            Assert.Equal("Contents", workbook.Worksheets.First().Name);
            Assert.Equal(run.Snapshot.Model.CapturedAt.UtcDateTime, workbook.Properties.Created.ToUniversalTime());
            Assert.Equal(0.576, workbook.Worksheet("Elements").Cell(3, 6).GetDouble());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ReportQuantitiesAreRoundedAndSteelRollUpsMatchTheSummary()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var tables = ReportExporter.Tables(run).ToDictionary(x => x.Name);
        var csv = ReportExporter.Csv(tables["Elements"]);
        Assert.Contains("\"C1\",\"Column\",\"C40\",\"C400x400\",\"Quantified\",\"0.576\"", csv);
        Assert.DoesNotContain("0000000", csv);
        foreach (var name in new[] { "Steel By Category", "Steel By Material", "Steel By Story", "Steel By Evidence" })
        {
            var column = Array.IndexOf(tables[name].Headers, "Known steel (kg)");
            Assert.Equal(run.Result.Summary.KnownSteelKg, tables[name].Rows.Sum(x => x[column] is Rounded r ? r.Value : 0), 6);
        }
        // B1's Transverse component has no reinforcement entry; it is counted as unknown, not hidden.
        Assert.Contains(tables["Steel By Evidence"].Rows, x => (string)x[0]! == "Not supplied" && (int)x[3]! == run.Result.Summary.MissingSteelComponents);
        Assert.Equal("1297.5", ReportExporter.Csv(tables["Summary"]).Split("\r\n").Single(x => x.StartsWith("\"Known steel\"")).Split(',')[1].Trim('"'));
    }

    [Fact]
    public void PreparedOverridesReadOriginalValuesFromTheCurrentRun()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var first = Overrides.Prepare(run.Snapshot, run.SnapshotSha256, run.Overrides, "W1", OverrideField.AreaThicknessM, .3, "Site check", "A. Reviewer", At);
        Assert.Equal((.25, "o1"), (first.OriginalValue, first.Id));
        var second = Overrides.Prepare(run.Snapshot, run.SnapshotSha256, [first], "W1", OverrideField.AreaThicknessM, .35, "Site check", "A. Reviewer", At);
        Assert.Equal((.3, "o2"), (second.OriginalValue, second.Id));
        var changed = TakeoffJson.AddOverrides(run, [first, second]);
        Assert.Equal(10.8 / .25 * .35, changed.Result.Elements.Single(x => x.ObjectId == "W1").GrossM3!.Value, 9);
        Assert.Throws<ArgumentException>(() => Overrides.Prepare(run.Snapshot, run.SnapshotSha256, [], "W1", OverrideField.FrameWidthM, .3, "r", "a", At));
    }

    private static string FixtureJson() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "takeoff.json"));

    [Theory]
    [InlineData("\"capturedAt\": \"2026-09-26T00:00:00+00:00\"", "$.model.capturedAt")]
    [InlineData("\"approvedAt\": \"2026-09-26T00:00:00+00:00\"", "$.reinforcement[0].approvedAt")]
    public void DatesWithoutAnOffsetAreRejected(string original, string path)
    {
        var json = FixtureJson();
        var index = json.IndexOf(original, StringComparison.Ordinal);
        json = json[..index] + original.Replace("+00:00", "", StringComparison.Ordinal) + json[(index + original.Length)..];
        var e = Assert.Throws<JsonException>(() => TakeoffJson.Parse<TakeoffSnapshot>(json));
        Assert.Equal(UtcDateTimeConverter.MissingOffset, e.Message);
        Assert.Equal(path, e.Path);
    }

    [Fact]
    public void PolicyApprovalWithoutAnOffsetIsRejected()
    {
        var json = FixtureJson();
        // The last date in the fixture is the policy's approvedAt, which may be null.
        var index = json.LastIndexOf("+00:00", StringComparison.Ordinal);
        var e = Assert.Throws<JsonException>(() => TakeoffJson.Parse<TakeoffSnapshot>(json.Remove(index, 6)));
        Assert.Equal("$.policy.approvedAt", e.Path);
    }

    [Fact]
    public void SameInstantWithAnotherOffsetHashesIdentically()
    {
        var json = FixtureJson();
        var shifted = json.Replace("\"capturedAt\": \"2026-09-26T00:00:00+00:00\"", "\"capturedAt\": \"2026-09-26T05:30:00+05:30\"", StringComparison.Ordinal);
        Assert.NotEqual(json, shifted);
        var run = TakeoffJson.Calculate(TakeoffJson.Parse<TakeoffSnapshot>(shifted));
        Assert.Equal(TakeoffJson.Calculate(TakeoffJson.Parse<TakeoffSnapshot>(json)).SnapshotSha256, run.SnapshotSha256);
        Assert.Contains("\"capturedAt\": \"2026-09-26T00:00:00+00:00\"", TakeoffJson.Serialize(run), StringComparison.Ordinal);
    }

    [Fact]
    public void ImplausibleValuesAreAcknowledgedPerElement()
    {
        // 300 typed with a metre unit instead of millimetres.
        var s = Fixture();
        var run = TakeoffJson.Calculate(s with { Model = s.Model with { Frames = s.Model.Frames.Select(f => f.ObjectId == "B1"
            ? f with { Section = f.Section! with { Width = new() { Value = 300, Unit = LengthUnit.Metre } } } : f).ToImmutableArray() } });
        Assert.Equal(["B1"], TakeoffJson.FlaggedElements(run, "IMPLAUSIBLE_DIMENSION"));
        Assert.Contains("IMPLAUSIBLE_DIMENSION:B1", TakeoffJson.RequiredAcknowledgements(run));

        var e = Assert.Throws<ArgumentException>(() => TakeoffJson.Accept(run, "Synthetic Reviewer", "note", true, TakeoffJson.WarningCodes(run).ToImmutableArray(), At));
        Assert.Contains("IMPLAUSIBLE_DIMENSION:B1", e.Message, StringComparison.Ordinal);
        var withoutElement = TakeoffJson.RequiredAcknowledgements(run).Where(x => x != "IMPLAUSIBLE_DIMENSION:B1").ToImmutableArray();
        Assert.Throws<ArgumentException>(() => TakeoffJson.Accept(run, "Synthetic Reviewer", "note", true, withoutElement, At));
        var wrongElement = withoutElement.Add("IMPLAUSIBLE_DIMENSION:C1");
        Assert.Throws<ArgumentException>(() => TakeoffJson.Accept(run, "Synthetic Reviewer", "note", true, wrongElement, At));
        Assert.Equal("AcceptedPartial", TakeoffJson.ReviewStatus(AcceptAll(run)));
    }

    [Fact]
    public void HandExclusionsAreLabelledAsReducedScope()
    {
        var complete = TakeoffJson.Calculate(WithMetadata(Fixture(), "B1", m => m with { RequiredSteelComponents = ["LongitudinalTop", "LongitudinalBottom"] }));
        Assert.Equal("complete in declared scope", TakeoffJson.CompleteLabel(complete));
        var excluded = TakeoffJson.AddOverrides(complete, [Override(complete, "W1", OverrideField.Excluded, 0, 1)]);
        Assert.Equal(["W1 (o1)"], TakeoffJson.Exclusions(excluded));
        Assert.Equal("complete in scope reduced by override (excludes W1 (o1))", TakeoffJson.CompleteLabel(excluded));

        var summary = ReportExporter.Tables(excluded).Single(x => x.Name == "Summary").Rows.ToDictionary(x => (string)x[0]!, x => x[1]);
        Assert.Equal("W1 (o1)", summary["Excluded by override"]);
        Assert.DoesNotContain(summary.Keys, x => x.Contains("declared scope", StringComparison.Ordinal));
        Assert.Equal("7.608", summary["Complete gross in scope reduced by override (excludes W1 (o1))"]!.ToString());

        var restored = TakeoffJson.AddOverrides(excluded, [Override(excluded, "W1", OverrideField.Excluded, 1, 0, "o2")]);
        Assert.Empty(TakeoffJson.Exclusions(restored));
        Assert.Equal("complete in declared scope", TakeoffJson.CompleteLabel(restored));
    }

    [Fact]
    public void AcceptanceReportsEveryProblemAtOnceAndNamesPartial()
    {
        var run = TakeoffJson.Calculate(Fixture());
        var codes = TakeoffJson.RequiredAcknowledgements(run).Where(x => x != "PARTIAL_STEEL").ToImmutableArray();
        var e = Assert.Throws<ArgumentException>(() => TakeoffJson.Accept(run, "Synthetic Reviewer", "note", false, codes, At));
        Assert.Contains("--partial", e.Message, StringComparison.Ordinal);
        Assert.Contains("steel is incomplete", e.Message, StringComparison.Ordinal);
        Assert.Contains("Missing: PARTIAL_STEEL", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"value\": 200", "\"value\": 250", "the snapshot")]
    [InlineData("\"massKg\": 42.39", "\"massKg\": 42.4", "the saved quantities of B1")]
    [InlineData("\"reviewer\": \"Synthetic Reviewer\"", "\"reviewer\": \"Someone Else\"", "the review history")]
    public void IntegrityFailuresSayWhatChanged(string before, string after, string expected)
    {
        var json = TakeoffJson.Serialize(AcceptAll(TakeoffJson.Calculate(Fixture())));
        Assert.Contains(before, json);
        var e = Assert.Throws<InvalidDataException>(() => TakeoffJson.Replay(json.Replace(before, after)));
        Assert.Contains("integrity check failed: " + expected, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunsFromAnotherEngineVersionSayWhatToDo()
    {
        var json = TakeoffJson.Serialize(TakeoffJson.Calculate(Fixture())).Replace(TakeoffEngine.Version, "takeoff/0.0.1");
        var e = Assert.Throws<InvalidDataException>(() => TakeoffJson.Replay(json));
        Assert.Contains("engine takeoff/0.0.1", e.Message, StringComparison.Ordinal);
        Assert.Contains("Calculate the snapshot again", e.Message, StringComparison.Ordinal);
    }
}
