using System.Collections.Immutable;
using Quentra.Application;
using Quentra.Core;
using Quentra.Etabs;
using Quentra.Infrastructure;

namespace Quentra.Tests;

// Mapping rules of the ETABS snapshot builder, on ETABS 22.7 captures modified to show one case at a time.
public class EtabsBuilderTests
{
    private static EtabsRawModel Capture(string name) =>
        EtabsRawJson.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "etabs", name + ".etabs-raw.json")));

    private static TakeoffRun Run(EtabsRawModel raw, EtabsBuildOptions? options = null) =>
        TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(raw, options ?? new()).Snapshot);

    private static IEnumerable<string> Codes(TakeoffRun run, string objectId) =>
        run.Result.Elements.Single(x => x.ObjectId == objectId).Warnings.Select(x => x.Code);

    [Fact]
    public void WeightlessNonConcreteMaterialMarksADummyMemberOutOfScope()
    {
        var raw = Capture("01-single-beam");
        raw = raw with
        {
            Materials = [.. raw.Materials, new RawMaterial("-", "NoDesign", null, null, 0, 0)],
            FrameSections = [.. raw.FrameSections.Select(s => s.Name == "B300x600" ? s with { Material = "-" } : s)]
        };
        var run = Run(raw);
        var beam = Assert.Single(run.Result.Elements);
        Assert.Equal(QuantityStatus.OutOfScope, beam.Status);
        Assert.Contains("ETABS_WEIGHTLESS_MATERIAL", Codes(run, beam.ObjectId));
        Assert.Null(run.Result.Summary.CompleteGrossM3); // nothing left in scope
    }

    [Fact]
    public void NoDesignMaterialWithWeightIsUnknownNotAssumed()
    {
        var raw = Capture("01-single-beam");
        raw = raw with
        {
            Materials = [.. raw.Materials, new RawMaterial("X", "NoDesign", null, null, 24, 2.4)],
            FrameSections = [.. raw.FrameSections.Select(s => s.Name == "B300x600" ? s with { Material = "X" } : s)]
        };
        var run = Run(raw);
        var beam = Assert.Single(run.Result.Elements);
        Assert.Equal(QuantityStatus.Unsupported, beam.Status);
        Assert.Contains("ETABS_MATERIAL_UNRESOLVED", Codes(run, beam.ObjectId));
    }

    [Fact]
    public void JointOffsetsMoveTheMeasuredAxisAndAreDisclosed()
    {
        var raw = Capture("01-single-beam");
        // Offsets in present units (m): the J end moves 0.5 m further along x, so the axis is 6.5 m.
        raw = raw with { Frames = [.. raw.Frames.Select(f => f with { Offset2 = new RawPoint(0.5, 0, 0) })] };
        var run = Run(raw);
        var beam = Assert.Single(run.Result.Elements);
        Assert.Equal(0.3 * 0.6 * 6.5, beam.GrossM3!.Value, 1e-12);
        Assert.Contains("ETABS_JOINT_OFFSET", Codes(run, beam.ObjectId));
    }

    [Fact]
    public void UntestedVersionsAndMultipleTowersAreFlagged()
    {
        var raw = Capture("01-single-beam");
        raw = raw with { Model = raw.Model with { ProgramBuild = "23.1.1.4293", Towers = ["T1", "T2"] } };
        var codes = TakeoffJson.WarningCodes(Run(raw)).ToArray();
        Assert.Contains("ETABS_UNTESTED_VERSION", codes);
        Assert.Contains("ETABS_MULTIPLE_TOWERS", codes);
    }

    [Fact]
    public void RepeatedLabelsFallBackToUniqueNames()
    {
        var raw = Capture("08-one-story-frame");
        // Two frames reporting the same label and story cannot both be B1@Story1.
        raw = raw with { Frames = [.. raw.Frames.Select(f => f.Label is "B2" ? f with { Label = "B1" } : f)] };
        var ids = EtabsSnapshotBuilder.Run(raw, new()).Snapshot.Model.Frames.Select(x => x.ObjectId).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(2, ids.Count(x => x.StartsWith("frame:", StringComparison.Ordinal)));
    }

    [Fact]
    public void UndeclaredStoryIsReportedAndAllocatedByElevation()
    {
        var raw = Capture("01-single-beam");
        raw = raw with { Frames = [.. raw.Frames.Select(f => f with { Story = "Mezzanine" })] };
        var run = Run(raw);
        var beam = Assert.Single(run.Result.Elements);
        Assert.Contains("ETABS_STORY_UNDECLARED", Codes(run, beam.ObjectId));
        Assert.Equal("Story1", Assert.Single(run.Result.StoryAllocations).StoryId);
    }

    [Theory]
    [InlineData("error", DesignEvidence.Failed)]
    [InlineData("section", DesignEvidence.Stale)]
    [InlineData("unlocked", DesignEvidence.Stale)]
    [InlineData("none", DesignEvidence.VerifiedCurrent)]
    public void DesignEvidenceIsClassifiedStrictly(string change, DesignEvidence expected)
    {
        var raw = Capture("13-designed-frame");
        raw = change switch
        {
            "error" => raw with { Design = raw.Design with { Beams = [.. raw.Design.Beams.Select(b => b with { Errors = [.. b.Errors.Select(_ => "Shear stress exceeds maximum allowed")] })] } },
            "section" => raw with { Design = raw.Design with { Beams = [.. raw.Design.Beams.Select(b => b with { DesignSection = "B300x600" })] } },
            "unlocked" => raw with { Model = raw.Model with { ModelLocked = false } },
            _ => raw
        };
        var snapshot = EtabsSnapshotBuilder.Run(raw, new() { SteelApprovedBy = "R", MaximumStationGapM = 2 }).Snapshot;
        var beamSteel = snapshot.Reinforcement.Where(x => x.Component == "LongitudinalTop").ToArray();
        Assert.Equal(4, beamSteel.Length);
        Assert.All(beamSteel, x => Assert.Equal(expected, x.DesignEvidence));
        var run = TakeoffJson.Calculate(snapshot);
        if (expected == DesignEvidence.VerifiedCurrent)
            Assert.All(run.Result.Steel.Where(x => x.Component == "LongitudinalTop"), x => Assert.NotNull(x.MassKg));
        else
        {
            Assert.All(run.Result.Steel.Where(x => x.Component == "LongitudinalTop"), x => Assert.Null(x.MassKg));
            Assert.Contains("ETABS_DESIGN_NOT_CURRENT", TakeoffJson.WarningCodes(run));
        }
    }

    [Fact]
    public void RepeatedDesignStationsKeepTheLargerArea()
    {
        var raw = Capture("13-designed-frame");
        var beam = raw.Design.Beams[0];
        var snapshot = EtabsSnapshotBuilder.Run(raw, new() { SteelApprovedBy = "R", MaximumStationGapM = 2 }).Snapshot;
        var torsion = snapshot.Reinforcement.First(x => x.Component == "LongitudinalTorsion" && x.SourceReference.Contains($"'{beam.Frame}'", StringComparison.Ordinal));
        // ETABS reports 18 stations with 4 locations twice; the snapshot keeps 14 distinct positions, positions strictly increasing.
        Assert.Equal(18, beam.Location.Length);
        Assert.Equal(14, torsion.Stations.Length);
        Assert.True(torsion.Stations.Zip(torsion.Stations.Skip(1)).All(p => p.Second.PositionM > p.First.PositionM));
        // At 1.2 m ETABS gives 669 mm² and 0: the larger governs.
        Assert.Equal(beam.TorsionLongitudinalArea.Max(), torsion.Stations.Single(s => Math.Abs(s.PositionM - 1.2) < 1e-9).AreaM2, 1e-12);
    }

    [Fact]
    public void FailedApiCallsAreAttachedToTheirObjectWithTheConsequence()
    {
        var raw = Capture("01-single-beam");
        var frame = raw.Frames[0].Name;
        raw = raw with { Issues = [new("Read frame GUID", frame, "FrameObj.GetGUID", 1, null, "The GUID is not recorded.")] };
        var run = Run(raw);
        var warning = run.Result.Elements.Single().Warnings.Single(x => x.Code == "ETABS_API_CALL_FAILED");
        Assert.Contains("FrameObj.GetGUID returned code 1", warning.Message, StringComparison.Ordinal);
        Assert.Contains("The GUID is not recorded.", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawCaptureRejectsOtherFormats() =>
        Assert.Throws<InvalidDataException>(() => EtabsSnapshotBuilder.Run(Capture("01-single-beam") with { Format = "other/9" }, new()));

    [Fact]
    public void InstanceChoicePrefersTheTestedVersionAndNeverSwitchesSilently()
    {
        EtabsInstance I(int pid, string version) => new(pid, @"C:\x\ETABS.exe", version, "");
        Assert.Equal(2, EtabsDiscovery.Choose([I(1, "23.1.1.4293"), I(2, "22.7.0.4095")], null, false).ProcessId);
        Assert.Contains("--pid", Assert.Throws<EtabsUnavailableException>(() => EtabsDiscovery.Choose([I(1, "22.7.0.4095"), I(2, "22.7.0.4095")], null, false)).Message, StringComparison.Ordinal);
        Assert.Contains("22.7", Assert.Throws<EtabsUnavailableException>(() => EtabsDiscovery.Choose([I(1, "23.1.1.4293")], null, false)).Message, StringComparison.Ordinal);
        Assert.Contains("untested", Assert.Throws<EtabsUnavailableException>(() => EtabsDiscovery.Choose([I(1, "23.1.1.4293")], 1, false)).Message, StringComparison.Ordinal);
        Assert.Equal(1, EtabsDiscovery.Choose([I(1, "23.1.1.4293")], 1, true).ProcessId);
        Assert.Contains("not running", Assert.Throws<EtabsUnavailableException>(() => EtabsDiscovery.Choose([], null, false)).Message, StringComparison.Ordinal);
        Assert.False(EtabsExtractor.IsSavedModel("(Untitled)"));
        Assert.False(EtabsExtractor.IsSavedModel(""));
        Assert.True(EtabsExtractor.IsSavedModel(@"C:\p\Tower.EDB"));
    }

    [Fact]
    public void FingerprintNamesChangedElements()
    {
        var raw = Capture("08-one-story-frame");
        var before = EtabsSnapshotBuilder.Run(raw, new()).Snapshot;
        var moved = raw with { Frames = [.. raw.Frames.Select((f, i) => i == 0 ? f with { Point2 = f.Point2 with { Z = f.Point2.Z + 0.1 } } : f)] };
        var comparison = SnapshotFingerprint.Compare(before, EtabsSnapshotBuilder.Run(moved, new()).Snapshot);
        Assert.False(comparison.Matches);
        Assert.Single(comparison.Changed);
        Assert.Empty(comparison.Added);
        Assert.True(SnapshotFingerprint.Compare(before, EtabsSnapshotBuilder.Run(raw with { ExtractedAt = raw.ExtractedAt.AddHours(1) }, new()).Snapshot).Matches);
    }
}

public class SourceWarningAndLabelTests
{
    private static TakeoffSnapshot Fixture() => TakeoffJson.Parse<TakeoffSnapshot>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "takeoff.json")));

    [Fact]
    public void SourceWarningsReachTheirElementOrTheRun()
    {
        var s = Fixture() with { SourceWarnings = [new("B1", "ETABS_JOINT_OFFSET", "offset"), new(null, "ETABS_SOURCE", "from ETABS")] };
        var run = TakeoffJson.Calculate(s);
        Assert.Contains(run.Result.Elements.Single(x => x.ObjectId == "B1").Warnings, w => w.Code == "ETABS_JOINT_OFFSET");
        Assert.Contains(run.Result.Warnings, w => w.Code == "ETABS_SOURCE");
        Assert.Contains("ETABS_JOINT_OFFSET", TakeoffJson.RequiredAcknowledgements(run));
    }

    [Theory]
    [InlineData("NOPE", "B1")]
    [InlineData("lower_case", "B1")]
    [InlineData("ETABS_X", "missing-element")]
    public void InvalidSourceWarningsAreRejected(string code, string objectId) =>
        Assert.Throws<ArgumentException>(() => TakeoffJson.Calculate(Fixture() with { SourceWarnings = [new(objectId, code, "m")] }));

    [Fact]
    public void SourceWarningOrderDoesNotChangeTheHash()
    {
        var a = Fixture() with { SourceWarnings = [new("B1", "ETABS_A_B", "1"), new(null, "ETABS_C_D", "2")] };
        var b = Fixture() with { SourceWarnings = [new(null, "ETABS_C_D", "2"), new("B1", "ETABS_A_B", "1")] };
        Assert.Equal(TakeoffJson.Calculate(a).SnapshotSha256, TakeoffJson.Calculate(b).SnapshotSha256);
    }

    [Fact]
    public void UnreviewedImplausibleValuesQualifyTheCompleteLabelUntilAccepted()
    {
        var s = Fixture();
        // A 6 m wide beam is far outside the section review band: a probable unit slip.
        s = s with { Model = s.Model with { Frames = [.. s.Model.Frames.Select(f => f.ObjectId == "B1" ? f with { Section = f.Section! with { Width = new() { Value = 6, Unit = LengthUnit.Metre } } } : f)] } };
        var run = TakeoffJson.Calculate(s);
        Assert.Contains("including unreviewed implausible values in B1", TakeoffJson.CompleteLabel(run), StringComparison.Ordinal);
        Assert.StartsWith("DRAFT", TakeoffJson.StatusBanner(run), StringComparison.Ordinal);
        var accepted = TakeoffJson.Accept(run, "R. Reviewer", "checked", true, [.. TakeoffJson.RequiredAcknowledgements(run)], DateTimeOffset.UtcNow,
            new ReviewerContext("acct", "pc", "v"));
        Assert.Contains("accepted by the reviewer", TakeoffJson.CompleteLabel(accepted), StringComparison.Ordinal);
        Assert.StartsWith("ACCEPTED AS PARTIAL SCOPE", TakeoffJson.StatusBanner(accepted), StringComparison.Ordinal);
        Assert.Equal("acct", accepted.ReviewHistory[^1].RecordedByAccount);
        // The identity fields survive replay and are part of the sealed package.
        Assert.Equal("pc", TakeoffJson.Replay(TakeoffJson.Serialize(accepted)).ReviewHistory[^1].RecordedOnComputer);
        Assert.Throws<InvalidDataException>(() => TakeoffJson.Replay(TakeoffJson.Serialize(accepted).Replace("\"pc\"", "\"other\"", StringComparison.Ordinal)));
    }
}
