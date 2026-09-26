using System.Collections.Immutable;

namespace Quentra.Core;

public static class FrameCalculator
{
    public static FrameQuantity Calculate(FrameSnapshot frame, LengthUnit coordinateUnit)
    {
        if (frame.Material == MaterialKind.NonConcrete)
            return Missing(frame, QuantityStatus.OutOfScope, "NON_CONCRETE", "Non-concrete frame is outside RC scope.");
        if (frame.Material == MaterialKind.Unknown)
            return Missing(frame, QuantityStatus.Unsupported, "UNKNOWN_MATERIAL", "Resolve the effective material before calculating concrete.");
        if (frame.Kind == FrameKind.Other)
            return Missing(frame, QuantityStatus.Unsupported, "UNSUPPORTED_ROLE", "Only beam and column roles are supported. Review the object's classification.");
        if (!frame.IsStraight || !frame.IsPrismatic)
            return Missing(frame, QuantityStatus.Unsupported, "UNSUPPORTED_GEOMETRY", "Curved or non-prismatic frames need a verified geometry method.");
        if (frame.Section is null)
            return Missing(frame, QuantityStatus.Invalid, "MISSING_SECTION", "Concrete is unknown because section dimensions are missing.");
        if (frame.Section.Shape == SectionShape.Unsupported ||
            (frame.Kind == FrameKind.Beam && frame.Section.Shape == SectionShape.Circle))
            return Missing(frame, QuantityStatus.Unsupported, "UNSUPPORTED_SECTION", "Support is limited to rectangular beams and rectangular/circular columns.");

        try
        {
            var factor = Units.MetresPerUnit(coordinateUnit);
            var start = frame.Start.Scale(factor);
            var end = frame.End.Scale(factor);
            if (!start.IsFinite || !end.IsFinite)
                throw new ArgumentOutOfRangeException(nameof(frame), "Coordinates must be finite.");
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dz = end.Z - start.Z;
            var length = new Length(Math.Sqrt(dx * dx + dy * dy + dz * dz));
            var area = frame.Section.Shape switch
            {
                SectionShape.Rectangle => new Area(RequiredLength(frame.Section.Width) * RequiredLength(frame.Section.Depth)),
                SectionShape.Circle => new Area(Math.PI * Math.Pow(RequiredLength(frame.Section.Diameter), 2) / 4),
                _ => throw new ArgumentOutOfRangeException(nameof(frame), "Unknown section shape.")
            };
            var volumeValue = area.SquareMetres * length.Metres;
            if (volumeValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(frame), "Calculated volume must be positive.");
            var volume = new Volume(volumeValue);
            return new FrameQuantity(frame.ObjectId, frame.SourceReference, QuantityStatus.Quantified,
                volume, null, new FrameTrace(start, end, length, area, "section area × 3D modeled axis length"),
                [new("STEEL_UNKNOWN", "No reinforcement source was supplied; steel mass is unknown.")]);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Missing(frame, QuantityStatus.Invalid, "INVALID_GEOMETRY", "Finite positive section dimensions and a nonzero finite axis length are required. Review source geometry.");
        }
    }

    private static double RequiredLength(SourceLength? length) =>
        new Length(length?.Metres ?? double.NaN).Metres;

    private static FrameQuantity Missing(FrameSnapshot frame, QuantityStatus status, string code, string message) =>
        new(frame.ObjectId, frame.SourceReference, status, null, null, null,
            ImmutableArray.Create(new CalculationWarning(code, message)));
}
