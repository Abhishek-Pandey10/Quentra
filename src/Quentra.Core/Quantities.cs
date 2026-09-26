using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Quentra.Core;

public enum QuantityStatus { Quantified, Unsupported, Invalid, OutOfScope }

public readonly record struct Length
{
    public double Metres { get; }
    [JsonConstructor]
    public Length(double metres)
    {
        if (!double.IsFinite(metres) || metres <= 0)
            throw new ArgumentOutOfRangeException(nameof(metres), "Length must be finite and positive.");
        Metres = metres;
    }
}

public readonly record struct Area
{
    public double SquareMetres { get; }
    [JsonConstructor]
    public Area(double squareMetres)
    {
        if (!double.IsFinite(squareMetres) || squareMetres <= 0)
            throw new ArgumentOutOfRangeException(nameof(squareMetres), "Area must be finite and positive.");
        SquareMetres = squareMetres;
    }
}

public readonly record struct Volume
{
    public double CubicMetres { get; }
    [JsonConstructor]
    public Volume(double cubicMetres)
    {
        if (!double.IsFinite(cubicMetres) || cubicMetres < 0)
            throw new ArgumentOutOfRangeException(nameof(cubicMetres), "Volume must be finite and nonnegative.");
        CubicMetres = cubicMetres;
    }
}

public sealed record CalculationWarning(string Code, string Message);
public sealed record FrameTrace(Point3 StartMetres, Point3 EndMetres, Length AxisLength,
    Area SectionArea, string Formula);

public sealed record FrameQuantity(
    string ObjectId,
    string SourceReference,
    QuantityStatus Status,
    Volume? GrossModeledVolume,
    double? SteelMassKg,
    FrameTrace? Trace,
    ImmutableArray<CalculationWarning> Warnings);

public sealed record QuantitySummary(
    int FoundCount,
    int InScopeCount,
    int QuantifiedCount,
    int UnsupportedCount,
    int InvalidCount,
    int OutOfScopeCount,
    Volume KnownGrossModeledVolume,
    Volume? CompleteGrossModeledVolume,
    double KnownSteelMassKg,
    double? CompleteSteelMassKg,
    int UnknownSteelElementCount);

public sealed record CalculationResult(
    string CalculationVersion,
    string ConcreteBasis,
    string ReviewStatus,
    ImmutableArray<FrameQuantity> Frames,
    QuantitySummary Summary,
    ImmutableArray<CalculationWarning> Warnings);
