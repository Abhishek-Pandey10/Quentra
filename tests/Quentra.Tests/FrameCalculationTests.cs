using System.Collections.Immutable;
using Quentra.Application;
using Quentra.Core;
using Quentra.Infrastructure;

namespace Quentra.Tests;

public class FrameCalculationTests
{
    internal static ModelSnapshot Fixture() => SnapshotJson.ParseSnapshot(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "frames.json")));

    [Fact]
    public void ManualBeamAndColumnFixturesMatchAndSteelRemainsUnknown()
    {
        var result = CalculateSnapshot.Run(Fixture());
        Assert.Equal(1.08, result.Frames[0].GrossModeledVolume!.Value.CubicMetres, 12);
        Assert.Equal(.576, result.Frames[1].GrossModeledVolume!.Value.CubicMetres, 12);
        Assert.Equal(1.656, result.Summary.KnownGrossModeledVolume.CubicMetres, 12);
        Assert.Equal(result.Summary.KnownGrossModeledVolume, result.Summary.CompleteGrossModeledVolume);
        Assert.Null(result.Summary.CompleteSteelMassKg);
        Assert.Equal(2, result.Summary.UnknownSteelElementCount);
        Assert.All(result.Frames, frame => Assert.Null(frame.SteelMassKg));
        Assert.Equal("Draft", result.ReviewStatus);
    }

    [Theory]
    [InlineData(LengthUnit.Metre, 1)]
    [InlineData(LengthUnit.Millimetre, 1000)]
    [InlineData(LengthUnit.Foot, 3.280839895013123)]
    [InlineData(LengthUnit.Inch, 39.37007874015748)]
    public void CoordinateAndSectionUnitsPreservePhysicalVolume(LengthUnit unit, double perMetre)
    {
        var source = Fixture();
        var converted = source with
        {
            CoordinateUnit = unit,
            Frames = source.Frames.Select(f => f with
            {
                Start = f.Start.Scale(perMetre), End = f.End.Scale(perMetre),
                Section = f.Section! with
                {
                    Width = new SourceLength { Value = f.Section!.Width!.Metres * perMetre, Unit = unit },
                    Depth = new SourceLength { Value = f.Section!.Depth!.Metres * perMetre, Unit = unit }
                }
            }).ToImmutableArray()
        };
        Assert.Equal(1.656, CalculateSnapshot.Run(converted).Summary.KnownGrossModeledVolume.CubicMetres, 12);
    }

    [Fact]
    public void InclinedMemberUsesThreeDimensionalLength()
    {
        var beam = Fixture().Frames[0] with { Start = new(10, 20, 30), End = new(13, 20, 34) };
        var result = FrameCalculator.Calculate(beam, LengthUnit.Metre);
        Assert.Equal(5, result.Trace!.AxisLength.Metres);
        Assert.Equal(.9, result.GrossModeledVolume!.Value.CubicMetres, 12);
    }

    [Fact]
    public void CircularColumnMatchesIndependentReference()
    {
        var column = Fixture().Frames[1] with
        {
            Section = new FrameSection
            {
                Shape = SectionShape.Circle,
                Diameter = new SourceLength { Value = 500, Unit = LengthUnit.Millimetre }
            }
        };
        Assert.Equal(.7068583470577035,
            FrameCalculator.Calculate(column, LengthUnit.Metre).GrossModeledVolume!.Value.CubicMetres, 12);
    }

    [Fact]
    public void SubdivisionAndReversedEndpointsConserveVolume()
    {
        var source = Fixture();
        var beam = source.Frames[0];
        var midpoint = new Point3(3, 0, 3.6);
        var split = source with
        {
            Frames = [beam with { ObjectId = "a", End = midpoint },
                beam with { ObjectId = "b", Start = beam.End, End = midpoint }]
        };
        Assert.Equal(1.08, CalculateSnapshot.Run(split).Summary.KnownGrossModeledVolume.CubicMetres, 12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDimensionsNeverProduceQuantities(double value)
    {
        var source = Fixture();
        var beam = source.Frames[0];
        beam = beam with { Section = beam.Section! with { Width = new SourceLength { Value = value, Unit = LengthUnit.Metre } } };
        var result = CalculateSnapshot.Run(source with { Frames = [beam, source.Frames[1]] });
        Assert.Equal(QuantityStatus.Invalid, result.Frames[0].Status);
        Assert.Null(result.Frames[0].GrossModeledVolume);
        Assert.Null(result.Summary.CompleteGrossModeledVolume);
        Assert.Equal(.576, result.Summary.KnownGrossModeledVolume.CubicMetres, 12);
        Assert.Equal(1, result.Summary.InvalidCount);
    }

    [Fact]
    public void ZeroLengthAndMissingSectionsAreInvalid()
    {
        var beam = Fixture().Frames[0];
        Assert.Equal(QuantityStatus.Invalid, FrameCalculator.Calculate(beam with { End = beam.Start }, LengthUnit.Metre).Status);
        Assert.Equal(QuantityStatus.Invalid, FrameCalculator.Calculate(beam with { Section = null }, LengthUnit.Metre).Status);
    }

    [Fact]
    public void UnsupportedGeometryAndUnknownMaterialStayInMissingCoverage()
    {
        var source = Fixture();
        var result = CalculateSnapshot.Run(source with
        {
            Frames = [source.Frames[0] with { IsPrismatic = false }, source.Frames[1] with { Material = MaterialKind.Unknown }]
        });
        Assert.Equal(2, result.Summary.UnsupportedCount);
        Assert.Equal(2, result.Summary.InScopeCount);
        Assert.Null(result.Summary.CompleteGrossModeledVolume);
        Assert.All(result.Frames, f => Assert.Null(f.GrossModeledVolume));
    }

    [Fact]
    public void NonConcreteIsExplicitlyOutsideScope()
    {
        var source = Fixture();
        var result = CalculateSnapshot.Run(source with { Frames = [source.Frames[0] with { Material = MaterialKind.NonConcrete }] });
        Assert.Equal(1, result.Summary.OutOfScopeCount);
        Assert.Equal(0, result.Summary.InScopeCount);
        Assert.Null(result.Summary.CompleteGrossModeledVolume);
        Assert.Contains(result.Warnings, x => x.Code == "EMPTY_SCOPE");
    }

    [Fact]
    public void DuplicateIdentitiesCannotDoubleCount()
    {
        var source = Fixture();
        Assert.Throws<ArgumentException>(() => CalculateSnapshot.Run(source with { Frames = [source.Frames[0], source.Frames[0]] }));
    }

    [Fact]
    public void EmptySnapshotIsNotACompleteBuildingQuantity()
    {
        var result = CalculateSnapshot.Run(Fixture() with { Frames = [] });
        Assert.Equal(0, result.Summary.KnownGrossModeledVolume.CubicMetres);
        Assert.Null(result.Summary.CompleteGrossModeledVolume);
        Assert.Null(result.Summary.CompleteSteelMassKg);
    }
}
