using System.Collections.Immutable;
using Quentra.Application;
using Quentra.Core;
using Quentra.Infrastructure;

namespace Quentra.Tests;

// Fixtures A–E and their expected values come from quentradesigndoc.md §33.
// These are illustrative inputs; engineer review of the fixtures remains pending.
public class ValidationFixtureTests
{
    internal static TakeoffSnapshot Load(string name) => TakeoffJson.Parse<TakeoffSnapshot>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "validation", name)));

    private static ElementTakeoff Element(TakeoffResult r, string id) => r.Elements.Single(x => x.ObjectId == id);

    // Coordinates in millimetres, section and thickness dimensions in inches.
    private static TakeoffSnapshot MillimetreAndInch(TakeoffSnapshot s)
    {
        static SourceLength Inches(SourceLength? x) => new() { Value = x!.Metres / .0254, Unit = LengthUnit.Inch };
        static ImmutableArray<Point3> Mm(ImmutableArray<Point3> points) => points.Select(p => p.Scale(1000)).ToImmutableArray();
        return s with
        {
            Model = s.Model with
            {
                CoordinateUnit = LengthUnit.Millimetre,
                Frames = s.Model.Frames.Select(f => f with
                {
                    Start = f.Start.Scale(1000), End = f.End.Scale(1000),
                    Section = f.Section! with { Width = Inches(f.Section.Width), Depth = Inches(f.Section.Depth) }
                }).ToImmutableArray()
            },
            Areas = s.Areas.Select(a => a with
            {
                Boundary = Mm(a.Boundary), Openings = a.Openings.Select(Mm).ToImmutableArray(), Thickness = Inches(a.Thickness)
            }).ToImmutableArray()
        };
    }

    // Replaces one area with pieces that each carry the original openings, metadata and steel inputs.
    private static TakeoffSnapshot Subdivide(TakeoffSnapshot s, string id, params ImmutableArray<Point3>[] pieces)
    {
        var source = s.Areas.Single(x => x.ObjectId == id);
        var meta = s.Metadata.Single(x => x.ObjectId == id);
        var steel = s.Reinforcement.Where(x => x.ObjectId == id).ToArray();
        var names = pieces.Select((_, i) => $"{id}-{i + 1}").ToArray();
        return s with
        {
            Areas = s.Areas.Remove(source).AddRange(pieces.Select((p, i) => source with { ObjectId = names[i], Boundary = p })),
            Metadata = s.Metadata.Remove(meta).AddRange(names.Select(n => meta with { ObjectId = n })),
            Reinforcement = s.Reinforcement.RemoveRange(steel).AddRange(names.SelectMany(n => steel.Select(x => x with { ObjectId = n })))
        };
    }

    private static ImmutableArray<Point3> Rect(double x0, double y0, double x1, double y1, double z) =>
        [new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)];
    private static ImmutableArray<Point3> Vertical(double x0, double x1, double y, double z0, double z1) =>
        [new(x0, y, z0), new(x1, y, z0), new(x1, y, z1), new(x0, y, z1)];

    public static TheoryData<string, double, double, double?> Expected() => new()
    {
        { "A-beam.json", 1.080, 1.080, 117.75 },
        { "B-column.json", .576, .576, 71.02512671 },
        { "C-slab.json", 6.000, 5.800, 455.30 },
        { "D-wall.json", 2.400, 1.980, 155.43 },
        { "E-bay.json", 15.192, 14.772, null }
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void FixtureMatchesSection33(string name, double gross, double openingAdjusted, double? steelKg)
    {
        var r = TakeoffEngine.Run(Load(name));
        Assert.Equal(r.Summary.InScope, r.Summary.Quantified);
        Assert.Equal(gross, r.Summary.CompleteGrossM3!.Value, 1e-9);
        Assert.Equal(openingAdjusted, r.Summary.CompleteOpeningAdjustedM3!.Value, 1e-9);
        if (steelKg is null) Assert.Null(r.Summary.CompleteSteelKg);
        else Assert.Equal(steelKg.Value, r.Summary.CompleteSteelKg!.Value, 1e-6);
        Assert.Equal(r.Summary.KnownGrossM3, r.StoryAllocations.Sum(x => x.GrossM3), 9);
        Assert.All(r.StoryAllocations, x => Assert.Equal("L1", x.StoryId));
        Assert.Contains(r.Warnings, x => x.Code == "POLICY_PENDING");
    }

    [Theory]
    [MemberData(nameof(Expected))]
    public void FixtureIsUnitInvariant(string name, double gross, double openingAdjusted, double? steelKg)
    {
        var r = TakeoffEngine.Run(MillimetreAndInch(Load(name)));
        Assert.Equal(gross, r.Summary.CompleteGrossM3!.Value, 1e-9);
        Assert.Equal(openingAdjusted, r.Summary.CompleteOpeningAdjustedM3!.Value, 1e-9);
        Assert.Equal(steelKg ?? 0, r.Summary.KnownSteelKg, 1e-6);
    }

    [Fact]
    public void BeamSteelIsTopPlusBottomDemand()
    {
        var steel = TakeoffEngine.Run(Load("A-beam.json")).Steel.ToDictionary(x => x.Component, x => x.MassKg);
        Assert.Equal(47.1, steel["LongitudinalTop"]!.Value, 1e-9);
        Assert.Equal(70.65, steel["LongitudinalBottom"]!.Value, 1e-9);
    }

    [Fact]
    public void CheckModeResultsCannotCompleteBeamSteel()
    {
        var s = Load("A-beam.json");
        var r = TakeoffEngine.Run(s with { Reinforcement = s.Reinforcement.Select(x => x with { DesignEvidence = DesignEvidence.CheckMode }).ToImmutableArray() });
        Assert.All(r.Steel, x => Assert.Null(x.MassKg));
        Assert.Null(r.Summary.CompleteSteelKg);
        Assert.Equal(2, r.Summary.MissingSteelComponents);
    }

    [Fact]
    public void SlabLayersAreEachOverOpeningAdjustedArea()
    {
        var r = TakeoffEngine.Run(Load("C-slab.json"));
        Assert.Equal(29, Element(r, "C1").SurfaceAreaM2!.Value, 1e-9);
        Assert.All(r.Steel, x => Assert.Equal(113.825, x.MassKg!.Value, 1e-9));
    }

    [Fact]
    public void DoorOnWallBaseIsDeductedOnce()
    {
        var wall = Element(TakeoffEngine.Run(Load("D-wall.json")), "D1");
        Assert.Equal(.42, wall.OpeningDeductionM3!.Value, 1e-9);
        Assert.Equal(9.9, wall.SurfaceAreaM2!.Value, 1e-9);
    }

    public static TheoryData<string> Subdivisions() => ["C across opening", "C into quarters", "D through door", "D across door head"];

    [Theory]
    [MemberData(nameof(Subdivisions))]
    public void SubdividedObjectsConserveConcreteAndSteel(string name)
    {
        var (file, split, gross, adjusted, steel) = name switch
        {
            "C across opening" => ("C-slab.json", Subdivide(Load("C-slab.json"), "C1", Rect(0, 0, 3, 5, 3), Rect(3, 0, 6, 5, 3)), 6.0, 5.8, 455.30),
            "C into quarters" => ("C-slab.json", Subdivide(Load("C-slab.json"), "C1",
                Rect(0, 0, 3, 2.5, 3), Rect(3, 0, 6, 2.5, 3), Rect(0, 2.5, 3, 5, 3), Rect(3, 2.5, 6, 5, 3)), 6.0, 5.8, 455.30),
            "D through door" => ("D-wall.json", Subdivide(Load("D-wall.json"), "D1", Vertical(0, 2, 0, 0, 3), Vertical(2, 4, 0, 0, 3)), 2.4, 1.98, 155.43),
            "D across door head" => ("D-wall.json", Subdivide(Load("D-wall.json"), "D1", Vertical(0, 4, 0, 0, 1.5), Vertical(0, 4, 0, 1.5, 3)), 2.4, 1.98, 155.43),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        var r = TakeoffEngine.Run(split);
        Assert.True(r.Summary.Quantified > 1, file);
        Assert.Equal(gross, r.Summary.CompleteGrossM3!.Value, 1e-9);
        Assert.Equal(adjusted, r.Summary.CompleteOpeningAdjustedM3!.Value, 1e-9);
        Assert.Equal(steel, r.Summary.CompleteSteelKg!.Value, 1e-6);
    }

    [Fact]
    public void BayElementsMatchSection33Rows()
    {
        var r = TakeoffEngine.Run(Load("E-bay.json"));
        Assert.Equal(1.920, r.Elements.Where(x => x.Category == "Column").Sum(x => x.GrossM3!.Value), 9);
        Assert.Equal(3.960, r.Elements.Where(x => x.Category == "Beam").Sum(x => x.GrossM3!.Value), 9);
        Assert.Equal(6.912, Element(r, "E-S1").GrossM3!.Value, 9);
        Assert.Equal(2.400, Element(r, "E-W1").GrossM3!.Value, 9);
        Assert.Equal(.420, Element(r, "E-W1").OpeningDeductionM3!.Value, 9);
        Assert.DoesNotContain(r.Warnings, x => x.Code is "AREA_OVERLAP" or "COINCIDENT_FRAMES" or "UNALLOCATED_STORY");
    }

    [Fact]
    public void BayWithoutComponentScheduleHasNoSteelTotal()
    {
        // §33: expected E steel is added only after an engineer-reviewed component schedule exists.
        var r = TakeoffEngine.Run(Load("E-bay.json"));
        Assert.Empty(r.Steel);
        Assert.Equal(0, r.Summary.KnownSteelKg);
        Assert.Null(r.Summary.CompleteSteelKg);
        Assert.Equal(4 * 2 + 4 * 3 + 4 + 1, r.Summary.MissingSteelComponents);
        Assert.Contains(r.Warnings, x => x.Code == "PARTIAL_STEEL");
    }

    [Theory]
    [InlineData("A-beam.json")]
    [InlineData("B-column.json")]
    [InlineData("C-slab.json")]
    [InlineData("D-wall.json")]
    [InlineData("E-bay.json")]
    public void FixtureRunReplaysIdentically(string name)
    {
        var saved = TakeoffJson.Serialize(TakeoffJson.Calculate(Load(name)));
        Assert.Equal(saved, TakeoffJson.Serialize(TakeoffJson.Replay(saved)));
    }
}
