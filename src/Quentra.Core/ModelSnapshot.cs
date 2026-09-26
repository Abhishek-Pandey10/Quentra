using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Quentra.Core;

public enum LengthUnit { Metre, Millimetre, Foot, Inch }
public enum FrameKind { Beam, Column, Other }
public enum MaterialKind { Concrete, NonConcrete, Unknown }
public enum SectionShape { Rectangle, Circle, Unsupported }
public enum SnapshotOrigin { Synthetic, Etabs }

// Source values remain unchanged in the exported snapshot. Coordinates share an explicit
// source unit; each section dimension carries its own unit, independent of coordinates.
public sealed record SourceLength
{
    public required double Value { get; init; }
    public required LengthUnit Unit { get; init; }

    [JsonIgnore]
    public double Metres => Value * Units.MetresPerUnit(Unit);
}

public static class Units
{
    public static double MetresPerUnit(LengthUnit unit) => unit switch
    {
        LengthUnit.Metre => 1,
        LengthUnit.Millimetre => 0.001,
        LengthUnit.Foot => 0.3048,
        LengthUnit.Inch => 0.0254,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown length unit.")
    };
}

[method: JsonConstructor]
public readonly record struct Point3(double X, double Y, double Z)
{
    public Point3 Scale(double factor) => new(X * factor, Y * factor, Z * factor);
    [JsonIgnore]
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

public sealed record FrameSection
{
    public required SectionShape Shape { get; init; }
    public SourceLength? Width { get; init; }
    public SourceLength? Depth { get; init; }
    public SourceLength? Diameter { get; init; }
}

public sealed record FrameSnapshot
{
    public required string ObjectId { get; init; }
    public required string SourceReference { get; init; }
    public required FrameKind Kind { get; init; }
    public required MaterialKind Material { get; init; }
    public required bool IsStraight { get; init; }
    public required bool IsPrismatic { get; init; }
    public required Point3 Start { get; init; }
    public required Point3 End { get; init; }
    public FrameSection? Section { get; init; }
}

public sealed record ModelSnapshot
{
    public required int SchemaVersion { get; init; }
    public required string ModelId { get; init; }
    public required SnapshotOrigin Origin { get; init; }
    public required string SourceDescription { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required LengthUnit CoordinateUnit { get; init; }
    public required ImmutableArray<FrameSnapshot> Frames { get; init; }
}
