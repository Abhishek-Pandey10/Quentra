using System.Collections.Immutable;
using Quentra.Core;

namespace Quentra.Application;

public static class CalculateSnapshot
{
    public const string Version = "frame-concrete/0.1.0";

    public static CalculationResult Run(ModelSnapshot snapshot)
    {
        Validate(snapshot);
        var frames = snapshot.Frames.OrderBy(x => x.ObjectId, StringComparer.Ordinal)
            .Select(x => FrameCalculator.Calculate(x, snapshot.CoordinateUnit)).ToImmutableArray();
        var quantified = frames.Count(x => x.Status == QuantityStatus.Quantified);
        var unsupported = frames.Count(x => x.Status == QuantityStatus.Unsupported);
        var invalid = frames.Count(x => x.Status == QuantityStatus.Invalid);
        var outOfScope = frames.Count(x => x.Status == QuantityStatus.OutOfScope);
        var inScope = frames.Length - outOfScope;
        var known = new Volume(frames.Sum(x => x.GrossModeledVolume?.CubicMetres ?? 0));
        var summary = new QuantitySummary(frames.Length, inScope, quantified, unsupported, invalid,
            outOfScope, known, inScope > 0 && quantified == inScope ? known : null,
            0, null, inScope);
        var warnings = ImmutableArray.CreateBuilder<CalculationWarning>();
        warnings.Add(new("GROSS_BASIS", "Gross modeled frame volumes retain member intersections. No net physical or BOQ quantity is calculated."));
        warnings.Add(new("FRAME_ONLY_SCOPE", "This snapshot calculation covers frames only. Slabs, walls, foundations, story allocation and steel calculation are not implemented."));
        if (snapshot.Origin == SnapshotOrigin.Synthetic)
            warnings.Add(new("SYNTHETIC_INPUT", "These are illustrative inputs, not extracted or verified ETABS results."));
        if (inScope == 0)
            warnings.Add(new("EMPTY_SCOPE", "No in-scope frames were found; known zero is not a complete building quantity."));
        if (unsupported + invalid > 0)
            warnings.Add(new("PARTIAL_CONCRETE", "Some in-scope concrete is unknown; the known subtotal is partial."));
        return new CalculationResult(Version, "GrossModeledFrameVolume", "Draft",
            frames, summary, warnings.ToImmutable());
    }

    public static void Validate(ModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != 1)
            throw new ArgumentException($"Unsupported snapshot schema version {snapshot.SchemaVersion}; expected 1.");
        if (string.IsNullOrWhiteSpace(snapshot.ModelId) || string.IsNullOrWhiteSpace(snapshot.SourceDescription))
            throw new ArgumentException("Model identity and source description must be supplied.");
        if (snapshot.CapturedAt == default)
            throw new ArgumentException("Snapshot capture time must be supplied.");
        if (!Enum.IsDefined(snapshot.CoordinateUnit) || !Enum.IsDefined(snapshot.Origin))
            throw new ArgumentException("Unknown coordinate unit or snapshot origin.");
        if (snapshot.Frames.IsDefault)
            throw new ArgumentException("Frames must be an array, including for an empty snapshot.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var frame in snapshot.Frames)
        {
            if (frame is null || string.IsNullOrWhiteSpace(frame.ObjectId) || string.IsNullOrWhiteSpace(frame.SourceReference))
                throw new ArgumentException("Every frame requires an object identity and source reference.");
            if (!ids.Add(frame.ObjectId))
                throw new ArgumentException($"Duplicate object identity: {frame.ObjectId}.");
            if (!Enum.IsDefined(frame.Kind) || !Enum.IsDefined(frame.Material) ||
                (frame.Section is not null && !Enum.IsDefined(frame.Section.Shape)))
                throw new ArgumentException($"Unknown frame classification: {frame.ObjectId}.");
            if (frame.Section is { } section)
                foreach (var dimension in new[] { section.Width, section.Depth, section.Diameter })
                    if (dimension is not null && !Enum.IsDefined(dimension.Unit))
                        throw new ArgumentException($"Unknown section unit: {frame.ObjectId}.");
        }
    }
}
