using System.Text.Json;
using Quentra.Etabs;
using Quentra.EtabsFixtures;
using Quentra.Infrastructure;

namespace Quentra.Tests;

// Replays raw captures taken from ETABS 22.7 (tools/Quentra.EtabsFixtures generate) through the snapshot builder and
// the engine, and compares them with the hand-calculated values in fixtures/etabs/v22.7/golden.json. No ETABS needed;
// the live re-extraction of the same models is the ETABS integration suite (tools/Quentra.EtabsFixtures verify).
public class EtabsCaptureTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "fixtures", "etabs");
    private static readonly JsonElement Golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "golden.json"))).RootElement;
    private static double Tolerance => Golden.GetProperty("tolerance").GetDouble();

    private static EtabsRawModel Raw(string name) => EtabsRawJson.Parse(File.ReadAllText(Path.Combine(Dir, name + ".etabs-raw.json")));

    private static TakeoffRun Run(string capture, JsonElement expected) =>
        TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(Raw(capture), GoldenCheck.Options(expected)).Snapshot);

    public static TheoryData<string, string> Captures() => new()
    {
        { "01-single-beam", "01-single-beam" }, { "02-single-column", "02-single-column" }, { "03-circular-column", "03-circular-column" },
        { "04-slab", "04-slab" }, { "05-slab-openings", "05-slab-openings" }, { "06-wall", "06-wall" }, { "07-wall-opening", "07-wall-opening" },
        { "08-one-story-frame", "08-one-story-frame" }, { "09-multi-story", "09-multi-story" }, { "10-meshed", "10-meshed" },
        { "10-meshed.after-mesh", "10-meshed.after-mesh" }, { "11-units.kN-m", "11-units" }, { "11-units.N-mm", "11-units" },
        { "11-units.kgf-m", "11-units" }, { "11-units.kip-ft", "11-units" }, { "11-units.lb-in", "11-units" },
        { "12-unsupported", "12-unsupported" }, { "13-designed-frame", "13-designed-frame" }
    };

    [Theory]
    [MemberData(nameof(Captures))]
    public void CaptureMatchesHandCalculation(string capture, string golden)
    {
        var expected = Golden.GetProperty("models").GetProperty(golden);
        var failures = GoldenCheck.Compare(expected, Run(capture, expected), Tolerance).Where(x => !x.Passed).Select(x => $"{x.Test}: {x.Detail}").ToArray();
        Assert.True(failures.Length == 0, $"{capture}:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void QuantitiesDoNotDependOnEtabsDisplayUnits()
    {
        var baseline = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(Raw("11-units.kN-m"), new()).Snapshot).Result;
        foreach (var units in new[] { "N-mm", "kgf-m", "kip-ft", "lb-in" })
        {
            var raw = Raw("11-units." + units);
            var result = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(raw, new()).Snapshot).Result;
            Assert.Equal(baseline.Elements.Select(x => x.ObjectId), result.Elements.Select(x => x.ObjectId));
            foreach (var (a, b) in baseline.Elements.Zip(result.Elements))
            {
                Assert.Equal(a.GrossM3!.Value, b.GrossM3!.Value, 1e-9);
                Assert.Equal(a.OpeningAdjustedM3!.Value, b.OpeningAdjustedM3!.Value, 1e-9);
            }
            Assert.Equal(baseline.StoryAllocations.Select(x => x.StoryId), result.StoryAllocations.Select(x => x.StoryId));
        }
    }

    [Fact]
    public void UnitCapturesReallyUseDifferentEtabsUnits()
    {
        var lengths = new[] { "kN-m", "N-mm", "kgf-m", "kip-ft", "lb-in" }.Select(u => Raw("11-units." + u).Model.PresentUnits).ToArray();
        Assert.Equal(["m", "mm", "m", "ft", "inch"], lengths.Select(x => x.Length));
        Assert.Equal(["kN", "N", "kgf", "kip", "lb"], lengths.Select(x => x.Force));
        // The raw coordinates differ by the unit factor, so the equality above is not an accident of identical input.
        var m = Raw("11-units.kN-m").Frames[0].Point2; var inch = Raw("11-units.lb-in").Frames[0].Point2;
        Assert.Equal(m.Z / 0.0254, inch.Z, 1e-6);
    }

    [Fact]
    public void MeshingDoesNotChangeQuantities()
    {
        var before = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(Raw("10-meshed"), new()).Snapshot).Result;
        var after = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(Raw("10-meshed.after-mesh"), new()).Snapshot).Result;
        var failures = GoldenCheck.MeshInvariance(before, after, Tolerance).Where(x => !x.Item2).Select(x => $"{x.Item1}: {x.Item3}").ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
        // Frames and slabs come from objects only: the analysis mesh added line and area elements, not objects.
        Assert.Equal(Raw("10-meshed").Frames.Length, Raw("10-meshed.after-mesh").Frames.Length);
    }

    [Fact]
    public void DesignedFrameClassifiesSteelEvidence()
    {
        var raw = Raw("13-designed-frame");
        var unapproved = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(raw, new()).Snapshot);
        // Without a named approver the ETABS evidence is recorded but not counted: known steel stays 0 and complete is unknown.
        Assert.Equal(0, unapproved.Result.Summary.KnownSteelKg);
        Assert.Null(unapproved.Result.Summary.CompleteSteelKg);
        Assert.Contains("ETABS_STEEL_UNAPPROVED", TakeoffJson.WarningCodes(unapproved));

        // ETABS puts column design stations 1.5 m apart; at the default 1.0 m limit those components are not counted, and say why.
        var strict = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(raw, new EtabsBuildOptions { SteelApprovedBy = "R. Reviewer" }).Snapshot);
        Assert.Equal(3, strict.Result.Elements.Count(e => e.Warnings.Any(w => w.Code == "ETABS_STATION_GAP")));
        Assert.Equal(3, strict.Result.Steel.Count(x => x.Evidence == "DesignDemandEquivalent" && x.MassKg is null));

        var approved = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(raw, new EtabsBuildOptions { SteelApprovedBy = "R. Reviewer", MaximumStationGapM = 2 }).Snapshot);
        var steel = approved.Result.Steel;
        Assert.All(steel.Where(x => x.Evidence == "DesignDemandEquivalent"), x => Assert.NotNull(x.MassKg));
        Assert.Contains(steel, x => x.Component == "LongitudinalTorsion");
        Assert.Single(steel, x => x.Evidence == "ModelAssignedEquivalent");
        // Beams and columns never become complete: shear (Transverse) steel is not converted, and the design stations
        // stop at the column and beam faces, so the longitudinal components cover only part of each member.
        Assert.Null(approved.Result.Summary.CompleteSteelKg);
        var frames = approved.Result.Elements.Where(x => x.Category is "Beam" or "Column").Select(x => x.ObjectId).ToHashSet();
        Assert.Equal(8, frames.Count);
        Assert.All(approved.Result.SteelCoverage.Where(x => frames.Contains(x.ObjectId)), x => Assert.Contains("Transverse", x.Missing));
        Assert.All(approved.Result.SteelCoverage.Where(x => frames.Contains(x.ObjectId) && x.ObjectId.StartsWith('B')), x => Assert.Contains("LongitudinalTop", x.Missing));
    }

    [Fact]
    public void BuildingIsDeterministic()
    {
        var raw = Raw("09-multi-story");
        Assert.Equal(TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(raw, new()).Snapshot).SnapshotSha256,
            TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(EtabsRawJson.Parse(EtabsRawJson.Serialize(raw)), new()).Snapshot).SnapshotSha256);
        Assert.Equal(EtabsRawJson.Sha256(raw), EtabsRawJson.Sha256(EtabsRawJson.Parse(EtabsRawJson.Serialize(raw))));
    }

    [Fact]
    public void SnapshotRecordsItsSource()
    {
        var raw = Raw("01-single-beam");
        var snapshot = EtabsSnapshotBuilder.Run(raw, new()).Snapshot;
        Assert.Equal(Core.SnapshotOrigin.Etabs, snapshot.Model.Origin);
        Assert.StartsWith("22.7.", snapshot.Source!.ProgramBuild, StringComparison.Ordinal);
        Assert.EndsWith(@"ETABS 22\ETABSv1.dll", snapshot.Source.ApiAssembly, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EtabsRawJson.Sha256(raw), snapshot.Source.RawCaptureSha256);
        Assert.All(snapshot.Model.Frames, f => Assert.StartsWith("ETABS frame object", f.SourceReference, StringComparison.Ordinal));
    }
}
