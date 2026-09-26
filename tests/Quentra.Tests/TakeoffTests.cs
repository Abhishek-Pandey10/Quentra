using System.Collections.Immutable;
using Quentra.Application;
using Quentra.Core;
using Quentra.Infrastructure;

namespace Quentra.Tests;

// Expected values are hand calculated from fixtures/synthetic/takeoff.json.
public class TakeoffTests
{
    // Fixture polygons are axis aligned, so clipping at 1 µm introduces no rounding.
    internal const double ClipTolerance = 1e-9;

    internal static TakeoffSnapshot Fixture() => TakeoffJson.Parse<TakeoffSnapshot>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "takeoff.json")));

    internal static TakeoffSnapshot WithArea(TakeoffSnapshot s, string id, Func<AreaSnapshot, AreaSnapshot> change) =>
        s with { Areas = s.Areas.Select(x => x.ObjectId == id ? change(x) : x).ToImmutableArray() };

    internal static TakeoffSnapshot WithMetadata(TakeoffSnapshot s, string id, Func<ElementMetadata, ElementMetadata> change) =>
        s with { Metadata = s.Metadata.Select(x => x.ObjectId == id ? change(x) : x).ToImmutableArray() };

    internal static TakeoffSnapshot WithSteel(TakeoffSnapshot s, string id, string component, Func<SteelInput, SteelInput> change) =>
        s with { Reinforcement = s.Reinforcement.Select(x => x.ObjectId == id && x.Component == component ? change(x) : x).ToImmutableArray() };

    private static ElementTakeoff Element(TakeoffResult r, string id) => r.Elements.Single(x => x.ObjectId == id);
    private static SteelQuantity Steel(TakeoffResult r, string id, string component) => r.Steel.Single(x => x.ObjectId == id && x.Component == component);
    private static ImmutableArray<Point3> Rect(double x0, double y0, double x1, double y1, double z) =>
        [new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)];

    [Fact]
    public void FrameSlabAndWallVolumesMatchHandCalculations()
    {
        var r = TakeoffEngine.Run(Fixture());
        Assert.Equal(1.08, Element(r, "B1").GrossM3!.Value, 9);
        Assert.Equal(.576, Element(r, "C1").GrossM3!.Value, 9);
        Assert.Equal(1.152, Element(r, "C2").GrossM3!.Value, 9);
        // Overlapping openings are unioned: 1 + 1 − 0.25 = 1.75 m², never 2 m².
        var slab = Element(r, "S1");
        Assert.Equal(4.8, slab.GrossM3!.Value, 9);
        Assert.Equal(.35, slab.OpeningDeductionM3!.Value, ClipTolerance);
        Assert.Equal(4.45, slab.OpeningAdjustedM3!.Value, ClipTolerance);
        Assert.Equal(22.25, slab.SurfaceAreaM2!.Value, ClipTolerance);
        var wall = Element(r, "W1");
        Assert.Equal(10.8, wall.GrossM3!.Value, 9);
        Assert.Equal(10.55, wall.OpeningAdjustedM3!.Value, ClipTolerance);
        Assert.Equal(18.408, r.Summary.KnownGrossM3, 9);
        Assert.Equal(17.808, r.Summary.KnownOpeningAdjustedM3, ClipTolerance);
        Assert.Equal(r.Summary.KnownOpeningAdjustedM3, r.Summary.CompleteOpeningAdjustedM3);
        Assert.Equal(5, r.Summary.Quantified);
    }

    [Fact]
    public void OpeningsOutsideTheHostAreClippedBeforeDeduction()
    {
        var s = WithArea(Fixture(), "S1", a => a with { Openings = [Rect(5.5, 3.5, 6.5, 4.5, 3.6)] });
        Assert.Equal(.25 * .2, Element(TakeoffEngine.Run(s), "S1").OpeningDeductionM3!.Value, ClipTolerance);
    }

    [Fact]
    public void RotatedSlabStaysWithinClippingPrecision()
    {
        // The fixture slab and openings rotated 30° in plan about the origin.
        var (sin, cos) = Math.SinCos(Math.PI / 6);
        Point3 Rotate(Point3 p) => new(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos, p.Z);
        var s = WithArea(Fixture(), "S1", a => a with
        {
            Boundary = a.Boundary.Select(Rotate).ToImmutableArray(),
            Openings = a.Openings.Select(o => o.Select(Rotate).ToImmutableArray()).ToImmutableArray()
        });
        var slab = Element(TakeoffEngine.Run(s), "S1");
        Assert.Equal(4.8, slab.GrossM3!.Value, 1e-6);
        Assert.Equal(4.45, slab.OpeningAdjustedM3!.Value, 1e-6);
    }

    [Fact]
    public void AreaUnitsPreservePhysicalVolume()
    {
        var s = Fixture();
        const double perMetre = 1000;
        var mm = s with
        {
            Model = s.Model with
            {
                CoordinateUnit = LengthUnit.Millimetre,
                Frames = s.Model.Frames.Select(f => f with { Start = f.Start.Scale(perMetre), End = f.End.Scale(perMetre) }).ToImmutableArray()
            },
            Areas = s.Areas.Select(a => a with
            {
                Boundary = a.Boundary.Select(p => p.Scale(perMetre)).ToImmutableArray(),
                Openings = a.Openings.Select(o => o.Select(p => p.Scale(perMetre)).ToImmutableArray()).ToImmutableArray()
            }).ToImmutableArray()
        };
        var r = TakeoffEngine.Run(mm);
        Assert.Equal(18.408, r.Summary.KnownGrossM3, 6);
        Assert.Equal(17.808, r.Summary.KnownOpeningAdjustedM3, ClipTolerance);
    }

    [Fact]
    public void StoryAllocationSplitsVerticalMembersAndConservesVolume()
    {
        var r = TakeoffEngine.Run(Fixture());
        var c2 = r.StoryAllocations.Where(x => x.ObjectId == "C2").ToArray();
        Assert.Equal(["L1", "L2"], c2.Select(x => x.StoryId));
        Assert.All(c2, x => Assert.Equal(.576, x.GrossM3, 9));
        // The window (z 1–2) lies wholly in L1.
        var w1 = r.StoryAllocations.Where(x => x.ObjectId == "W1").ToDictionary(x => x.StoryId);
        Assert.Equal(5.4, w1["L1"].GrossM3, 6);
        Assert.Equal(5.15, w1["L1"].OpeningAdjustedM3, ClipTolerance);
        Assert.Equal(5.4, w1["L2"].GrossM3, 6);
        Assert.Equal(5.4, w1["L2"].OpeningAdjustedM3, 6);
        Assert.Equal("L1", r.StoryAllocations.Single(x => x.ObjectId == "S1").StoryId);
        Assert.Equal("L1", r.StoryAllocations.Single(x => x.ObjectId == "B1").StoryId);
        var byStory = r.StoryAllocations.GroupBy(x => x.StoryId).ToDictionary(g => g.Key, g => g.Sum(x => x.GrossM3));
        Assert.Equal(12.432, byStory["L1"], 6);
        Assert.Equal(5.976, byStory["L2"], 6);
        Assert.Equal(r.Summary.KnownGrossM3, r.StoryAllocations.Sum(x => x.GrossM3), 6);
        Assert.DoesNotContain(r.Warnings, x => x.Code == "UNALLOCATED_STORY");
    }

    [Fact]
    public void GeometryAboveDeclaredStoriesIsUnallocatedNotDropped()
    {
        var s = Fixture() with { Stories = [new("L1", 0, 3.6)] };
        s = s with { Metadata = s.Metadata.Select(m => m.AssignedStoryId == "L2" ? m with { AssignedStoryId = null } : m).ToImmutableArray() };
        var r = TakeoffEngine.Run(s);
        Assert.Equal(.576, r.StoryAllocations.Single(x => x.ObjectId == "C2" && x.StoryId == "Unallocated").GrossM3, 9);
        Assert.Equal(5.4, r.StoryAllocations.Single(x => x.ObjectId == "W1" && x.StoryId == "Unallocated").GrossM3, 6);
        Assert.Equal(r.Summary.KnownGrossM3, r.StoryAllocations.Sum(x => x.GrossM3), 6);
        Assert.Contains(r.Warnings, x => x.Code == "UNALLOCATED_STORY");
    }

    [Fact]
    public void BeamAtALevelInFeetStaysInTheStoryBelow()
    {
        // 12 ft × 0.3048 evaluates to 3.6576000000000004 m, one ulp above the declared level.
        static Point3 Feet(Point3 p) => p with { Z = p.Z switch { 3.6 => 12, 7.2 => 24, _ => p.Z } };
        var s = Fixture();
        s = s with
        {
            Model = s.Model with { CoordinateUnit = LengthUnit.Foot, Frames = s.Model.Frames.Select(f => f with { Start = Feet(f.Start), End = Feet(f.End) }).ToImmutableArray() },
            Areas = s.Areas.Select(a => a with
            {
                Boundary = a.Boundary.Select(Feet).ToImmutableArray(),
                Openings = a.Openings.Select(o => o.Select(Feet).ToImmutableArray()).ToImmutableArray()
            }).ToImmutableArray(),
            Stories = [new("L1", 0, 3.6576), new("L2", 3.6576, 7.3152)]
        };
        Assert.Equal("L1", TakeoffEngine.Run(s).StoryAllocations.Single(x => x.ObjectId == "B1").StoryId);
    }

    [Fact]
    public void EachSteelMethodMatchesHandCalculation()
    {
        var r = TakeoffEngine.Run(Fixture());
        // max(1.2e-3, 0.6e-3) × 3 m, twice → 7.2e-3 m³ × 7850.
        Assert.Equal(56.52, Steel(r, "B1", "LongitudinalTop").MassKg!.Value, 6);
        Assert.Equal("DesignDemandEquivalent", Steel(r, "B1", "LongitudinalTop").Evidence);
        Assert.Equal(42.39, Steel(r, "B1", "LongitudinalBottom").MassKg!.Value, 6);
        Assert.Equal("ModelAssignedEquivalent", Steel(r, "B1", "LongitudinalBottom").Evidence);
        Assert.Equal(86.4, Steel(r, "C1", "AllIn").MassKg!.Value, 6);
        Assert.Equal(180.864, Steel(r, "C2", "AllIn").MassKg!.Value, 6);
        Assert.Equal("Estimated", Steel(r, "C2", "AllIn").Evidence);
        Assert.Equal(87.33125, Steel(r, "S1", "XBottom").MassKg!.Value, ClipTolerance * 7850);
        Assert.Equal(844, Steel(r, "W1", "AllIn").MassKg!.Value, ClipTolerance * 7850);
        Assert.Equal(1297.50525, r.Summary.KnownSteelKg, ClipTolerance * 7850);
    }

    [Fact]
    public void MissingRequiredComponentKeepsSteelPartial()
    {
        var r = TakeoffEngine.Run(Fixture());
        var b1 = r.SteelCoverage.Single(x => x.ObjectId == "B1");
        Assert.Equal(["Transverse"], b1.Missing.ToArray());
        Assert.Null(b1.CompleteMassKg);
        Assert.Null(r.Summary.CompleteSteelKg);
        Assert.Equal(1, r.Summary.MissingSteelComponents);
        Assert.Contains(r.Warnings, x => x.Code == "PARTIAL_STEEL");

        var complete = TakeoffEngine.Run(WithMetadata(Fixture(), "B1", m => m with { RequiredSteelComponents = ["LongitudinalTop", "LongitudinalBottom"] }));
        Assert.Equal(1297.50525, complete.Summary.CompleteSteelKg!.Value, ClipTolerance * 7850);
        Assert.DoesNotContain(complete.Warnings, x => x.Code == "PARTIAL_STEEL");
    }

    [Theory]
    [InlineData(DesignEvidence.Stale)]
    [InlineData(DesignEvidence.CheckMode)]
    [InlineData(DesignEvidence.Missing)]
    public void DemandWithoutVerifiedCurrentEvidenceIsUnknown(DesignEvidence evidence)
    {
        var r = TakeoffEngine.Run(WithSteel(Fixture(), "B1", "LongitudinalTop", x => x with { DesignEvidence = evidence }));
        var top = Steel(r, "B1", "LongitudinalTop");
        Assert.Null(top.MassKg);
        Assert.Contains(top.Warnings, x => x.Code == "STEEL_UNAVAILABLE");
        Assert.Contains("LongitudinalTop", r.SteelCoverage.Single(x => x.ObjectId == "B1").Missing);
    }

    [Fact]
    public void DemandStationGapsAndExtrapolationAreRejected()
    {
        var gap = TakeoffEngine.Run(WithSteel(Fixture(), "B1", "LongitudinalTop", x => x with { MaximumStationGapM = 2 }));
        Assert.Null(Steel(gap, "B1", "LongitudinalTop").MassKg);
        var beyond = TakeoffEngine.Run(WithSteel(Fixture(), "B1", "LongitudinalTop", x => x with { DomainEndM = 7 }));
        Assert.Null(Steel(beyond, "B1", "LongitudinalTop").MassKg);
    }

    [Fact]
    public void PartialDemandDomainDoesNotCoverTheComponent()
    {
        var r = TakeoffEngine.Run(WithSteel(Fixture(), "B1", "LongitudinalTop", x => x with
        {
            DomainEndM = 3, Stations = [new(0, .0012), new(3, .0006)]
        }));
        var top = Steel(r, "B1", "LongitudinalTop");
        Assert.Equal(.0036 * 7850, top.MassKg!.Value, 6);
        Assert.Empty(top.CoversComponents);
        Assert.Contains(top.Warnings, x => x.Code == "PARTIAL_DEMAND_DOMAIN");
        Assert.Contains("LongitudinalTop", r.SteelCoverage.Single(x => x.ObjectId == "B1").Missing);
    }

    [Fact]
    public void UnapprovedSteelInputIsUnknown()
    {
        var r = TakeoffEngine.Run(WithSteel(Fixture(), "C1", "AllIn", x => x with { ApprovedBy = " " }));
        Assert.Null(Steel(r, "C1", "AllIn").MassKg);
        Assert.Equal(["Longitudinal", "Transverse"], r.SteelCoverage.Single(x => x.ObjectId == "C1").Missing.ToArray());
    }

    [Fact]
    public void UnverifiedAreasAreUnsupportedAndBlockCompleteness()
    {
        var r = TakeoffEngine.Run(WithArea(Fixture(), "S1", a => a with { OpeningsVerified = false }));
        var slab = Element(r, "S1");
        Assert.Equal(QuantityStatus.Unsupported, slab.Status);
        Assert.Null(slab.GrossM3);
        Assert.Null(r.Summary.CompleteOpeningAdjustedM3);
        Assert.Equal(13.608, r.Summary.KnownGrossM3, 9);
        Assert.Null(Steel(r, "S1", "XBottom").MassKg);
        Assert.Contains(r.Warnings, x => x.Code == "PARTIAL_CONCRETE");
    }

    [Fact]
    public void WarpedAndSelfIntersectingAreasAreInvalid()
    {
        var warped = WithArea(Fixture(), "S1", a => a with { Boundary = [new(0, 0, 3.6), new(6, 0, 3.6), new(6, 4, 3.7), new(0, 4, 3.6)] });
        Assert.Equal(QuantityStatus.Invalid, Element(TakeoffEngine.Run(warped), "S1").Status);
        var bowtie = WithArea(Fixture(), "S1", a => a with { Boundary = [new(0, 0, 3.6), new(6, 4, 3.6), new(6, 0, 3.6), new(0, 4, 3.6)], Openings = [] });
        Assert.Equal(QuantityStatus.Invalid, Element(TakeoffEngine.Run(bowtie), "S1").Status);
    }

    [Fact]
    public void NonConcreteAreaIsOutsideScope()
    {
        var r = TakeoffEngine.Run(WithArea(Fixture(), "S1", a => a with { Material = MaterialKind.NonConcrete }));
        Assert.Equal(QuantityStatus.OutOfScope, Element(r, "S1").Status);
        Assert.Equal("NON_CONCRETE", Assert.Single(Element(r, "S1").Warnings).Code);
        Assert.Equal(1, r.Summary.OutOfScope);
        Assert.DoesNotContain(r.SteelCoverage, x => x.ObjectId == "S1");
        Assert.Equal(13.358, r.Summary.CompleteOpeningAdjustedM3!.Value, ClipTolerance);
    }

    [Fact]
    public void OverlapsAndCoincidentFramesAreFlaggedButRetained()
    {
        var s = Fixture();
        var copy = s.Areas[0] with { ObjectId = "S2", SourceReference = "synthetic#S2", Openings = [] };
        s = s with
        {
            Model = s.Model with { Frames = s.Model.Frames.Add(s.Model.Frames[0] with { ObjectId = "B2", Start = s.Model.Frames[0].End, End = s.Model.Frames[0].Start }) },
            Areas = s.Areas.Add(copy),
            Metadata = s.Metadata.Add(s.Metadata[0] with { ObjectId = "B2" }).Add(s.Metadata[3] with { ObjectId = "S2" })
        };
        var r = TakeoffEngine.Run(s);
        Assert.Contains(r.Warnings, x => x.Code == "AREA_OVERLAP" && x.Message.Contains("S1") && x.Message.Contains("S2"));
        Assert.Contains(r.Warnings, x => x.Code == "COINCIDENT_FRAMES" && x.Message.Contains("B1") && x.Message.Contains("B2"));
        Assert.Equal(18.408 + 1.08 + 4.8, r.Summary.KnownGrossM3, 9);
    }

    [Fact]
    public void NearlyCoplanarLargeAreaIsFlaggedNotFatal()
    {
        // Tilted 1e-5 rad from S1: the normals pass the parallel test, but the far edge is 10 mm off S1's plane.
        var s = Fixture();
        var tilted = s.Areas[0] with
        {
            ObjectId = "S2", SourceReference = "synthetic#S2", Openings = [],
            Boundary = [new(1, 0, 3.6), new(1001, 0, 3.61), new(1001, 10, 3.61), new(1, 10, 3.6)]
        };
        s = s with { Areas = s.Areas.Add(tilted), Metadata = s.Metadata.Add(s.Metadata[3] with { ObjectId = "S2" }) };
        var r = TakeoffEngine.Run(s);
        Assert.Equal(QuantityStatus.Quantified, Element(r, "S2").Status);
        Assert.Contains(r.Warnings, x => x.Code == "OVERLAP_UNCHECKED" && x.Message.Contains("S1") && x.Message.Contains("S2"));
    }

    [Fact]
    public void PolicyApprovalIsReported()
    {
        var s = Fixture();
        Assert.DoesNotContain(TakeoffEngine.Run(s).Warnings, x => x.Code == "POLICY_PENDING");
        var pending = s with { Policy = s.Policy with { ApprovedBy = null, ApprovedAt = null } };
        Assert.Contains(TakeoffEngine.Run(pending).Warnings, x => x.Code == "POLICY_PENDING");
    }

    public static TheoryData<string> InvalidSnapshots() =>
        ["missing metadata", "overlapping stories", "all-in mixed with component", "all-in partial cover",
         "demand on transverse", "unknown component", "unrequired component", "duplicate area id", "bad tolerance", "half approval"];

    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void InvalidSnapshotsAreRejected(string name)
    {
        var s = Fixture();
        var invalid = name switch
        {
            "missing metadata" => s with { Metadata = s.Metadata.RemoveAt(0) },
            "overlapping stories" => s with { Stories = [new("L1", 0, 4), new("L2", 3.6, 7.2)] },
            "all-in mixed with component" => s with { Reinforcement = s.Reinforcement.Add(s.Reinforcement[0] with
                { ObjectId = "C1", Component = "Longitudinal", Method = SteelMethod.AssignedBars, Bars = [new(4, .0003, 3.6)] }) },
            "all-in partial cover" => WithSteel(s, "C1", "AllIn", x => x with { CoversComponents = ["Longitudinal"] }),
            "demand on transverse" => WithMetadata(s, "B1", m => m with { RequiredSteelComponents = ["LongitudinalBottom", "Transverse"] }) with
                { Reinforcement = s.Reinforcement.Select(x => x.Component == "LongitudinalTop" ? x with { Component = "Transverse" } : x).ToImmutableArray() },
            "unknown component" => WithMetadata(s, "S1", m => m with { RequiredSteelComponents = ["Mesh"] }),
            "unrequired component" => WithSteel(s, "S1", "XBottom", x => x with { Component = "YBottom" }),
            "duplicate area id" => WithArea(s, "S1", a => a with { ObjectId = "B1" }),
            "bad tolerance" => s with { Policy = s.Policy with { LinearToleranceM = .01 } },
            "half approval" => s with { Policy = s.Policy with { ApprovedAt = null } },
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        Assert.Throws<ArgumentException>(() => TakeoffEngine.Run(invalid));
    }

    [Fact]
    public void UnitSlipsAreWarnedButStillQuantified()
    {
        // 250 typed with a metre unit instead of millimetres.
        var r = TakeoffEngine.Run(WithArea(Fixture(), "W1", a => a with { Thickness = new() { Value = 250, Unit = LengthUnit.Metre } }));
        var wall = Element(r, "W1");
        Assert.Equal(QuantityStatus.Quantified, wall.Status);
        Assert.Contains(wall.Warnings, x => x.Code == "IMPLAUSIBLE_DIMENSION" && x.Message.Contains("Thickness 250 m"));
        Assert.DoesNotContain(Element(r, "S1").Warnings, x => x.Code.StartsWith("IMPLAUSIBLE", StringComparison.Ordinal));
    }

    [Fact]
    public void FrameSectionOutsideTheReviewBandIsWarned()
    {
        var s = Fixture();
        s = s with { Model = s.Model with { Frames = s.Model.Frames.Select(f => f.ObjectId == "B1"
            ? f with { Section = f.Section! with { Width = new() { Value = 300, Unit = LengthUnit.Inch } } } : f).ToImmutableArray() } };
        Assert.Contains(Element(TakeoffEngine.Run(s), "B1").Warnings, x => x.Code == "IMPLAUSIBLE_DIMENSION" && x.Message.StartsWith("Width 7.62 m", StringComparison.Ordinal));
    }

    [Fact]
    public void SteelRateAboveSteelDensityIsUnknown()
    {
        var r = TakeoffEngine.Run(WithSteel(Fixture(), "C1", "AllIn", x => x with { Ratio = 15000 }));
        var steel = Steel(r, "C1", "AllIn");
        Assert.Null(steel.MassKg);
        Assert.Contains("exceeds the steel density", Assert.Single(steel.Warnings).Message);
    }

    [Fact]
    public void HighSteelIntensityIsWarned()
    {
        var r = TakeoffEngine.Run(WithSteel(Fixture(), "W1", "AllIn", x => x with { Ratio = 700 }));
        Assert.Equal(700 * 10.55, Steel(r, "W1", "AllIn").MassKg!.Value, 6);
        Assert.Contains(Element(r, "W1").Warnings, x => x.Code == "IMPLAUSIBLE_STEEL_INTENSITY");
    }

    [Fact]
    public void ImplausibleSteelDensityPolicyIsWarned()
    {
        var s = Fixture();
        var r = TakeoffEngine.Run(s with { Policy = s.Policy with { SteelDensityKgM3 = 78500 } });
        Assert.Contains(r.Warnings, x => x.Code == "IMPLAUSIBLE_POLICY");
    }

    [Fact]
    public void OpeningsOutsideOrAcrossTheHostAreWarned()
    {
        var outside = TakeoffEngine.Run(WithArea(Fixture(), "S1", a => a with { Openings = [Rect(7, 1, 8, 2, 3.6)] }));
        Assert.Equal(0, Element(outside, "S1").OpeningDeductionM3!.Value, ClipTolerance);
        Assert.Equal("OPENING_OUTSIDE_HOST", Assert.Single(Element(outside, "S1").Warnings).Code);
        var crossing = TakeoffEngine.Run(WithArea(Fixture(), "S1", a => a with { Openings = [Rect(5.5, 3.5, 6.5, 4.5, 3.6)] }));
        Assert.Equal("OPENING_CROSSES_HOST", Assert.Single(Element(crossing, "S1").Warnings).Code);
        // A notch sharing two edges with the host lies inside it.
        var notch = TakeoffEngine.Run(WithArea(Fixture(), "S1", a => a with { Openings = [Rect(5, 0, 6, 1, 3.6)] }));
        Assert.Empty(Element(notch, "S1").Warnings);
    }

    [Fact]
    public void EmptyScopeSteelWarningDoesNotClaimMissingComponents()
    {
        var s = Fixture();
        var empty = s with { Model = s.Model with { Frames = [] }, Areas = [], Metadata = [], Reinforcement = [] };
        var warning = TakeoffEngine.Run(empty).Warnings.Single(x => x.Code == "PARTIAL_STEEL");
        Assert.StartsWith("Steel scope is empty", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationErrorsNameTheFailingObject()
    {
        var e = Assert.Throws<ArgumentException>(() => TakeoffEngine.Validate(WithMetadata(Fixture(), "W1", m => m with { RequiredSteelComponents = ["Hoops"] })));
        Assert.StartsWith("Metadata for W1: unknown steel component 'Hoops'", e.Message, StringComparison.Ordinal);
    }
}
