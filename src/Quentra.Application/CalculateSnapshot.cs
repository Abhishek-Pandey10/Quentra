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
        ThrowIfAny(Problems(snapshot));
    }

    // Every independent problem is reported, so a hand-edited snapshot is fixed in one pass rather than
    // one error per run. Checking stops early only where later checks cannot run.
    public static IEnumerable<string> Problems(ModelSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != 1)
        {
            yield return $"Unsupported snapshot schema version {snapshot.SchemaVersion}; expected 1.";
            yield break;
        }
        if (string.IsNullOrWhiteSpace(snapshot.ModelId) || string.IsNullOrWhiteSpace(snapshot.SourceDescription))
            yield return "Model identity and source description must be supplied.";
        if (snapshot.CapturedAt == default)
            yield return "Snapshot capture time must be supplied.";
        if (!Enum.IsDefined(snapshot.CoordinateUnit) || !Enum.IsDefined(snapshot.Origin))
            yield return "Unknown coordinate unit or snapshot origin.";
        if (snapshot.Frames.IsDefault)
        {
            yield return "Frames must be an array, including for an empty snapshot.";
            yield break;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < snapshot.Frames.Length; i++)
        {
            var frame = snapshot.Frames[i];
            if (frame is null || string.IsNullOrWhiteSpace(frame.ObjectId))
            {
                yield return $"Frame {i + 1}: objectId is required.";
                continue;
            }
            if (string.IsNullOrWhiteSpace(frame.SourceReference))
                yield return $"Frame {frame.ObjectId}: sourceReference is required.";
            if (!ids.Add(frame.ObjectId))
                yield return $"Duplicate object identity: {frame.ObjectId}.";
            if (!Enum.IsDefined(frame.Kind) || !Enum.IsDefined(frame.Material) ||
                (frame.Section is not null && !Enum.IsDefined(frame.Section.Shape)))
                yield return $"Unknown frame classification: {frame.ObjectId}.";
            if (frame.Section is { } section &&
                new[] { section.Width, section.Depth, section.Diameter }.Any(d => d is not null && !Enum.IsDefined(d.Unit)))
                yield return $"Unknown section unit: {frame.ObjectId}.";
        }
    }

    public const int ProblemsShown = 50;

    // A single problem keeps its own message; several are listed together.
    public static void ThrowIfAny(IEnumerable<string> problems)
    {
        var list = problems.ToList();
        if (list.Count == 1) throw new ArgumentException(list[0]);
        if (list.Count > 1)
            throw new ArgumentException($"The snapshot has {list.Count} problems:" + string.Concat(list.Take(ProblemsShown).Select(x => "\n  - " + x)) +
                (list.Count > ProblemsShown ? $"\n  ... and {list.Count - ProblemsShown} more." : ""));
    }
}
