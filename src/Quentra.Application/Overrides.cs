using System.Collections.Immutable;
using System.Globalization;
using Quentra.Core;

namespace Quentra.Application;

public enum OverrideField { FrameWidthM, FrameDepthM, FrameDiameterM, AreaThicknessM, Excluded }
public sealed record QuantityOverride(string Id, string ObjectId, OverrideField Field,
    double OriginalValue, double ReplacementValue, string Reason, string Author,
    DateTimeOffset RecordedAt, string SnapshotSha256);

public static class Overrides
{
    // SectionChanges names, per frame, the overrides that changed its section, so steel that no longer
    // matches the section can say which override caused it.
    public static (TakeoffSnapshot Effective, HashSet<string> Excluded, Dictionary<string, string> SectionChanges) Apply(TakeoffSnapshot source,
        ImmutableArray<QuantityOverride> changes, string sourceHash)
    {
        if (changes.IsDefault) throw new ArgumentException("Override ledger must be an array.");
        var effective = source;
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var sectionChanges = new Dictionary<string, string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (change is null || string.IsNullOrWhiteSpace(change.Id)) throw new ArgumentException("Every override needs an id.");
            var name = $"Override {change.Id}";
            if (!ids.Add(change.Id)) throw new ArgumentException($"{name}: the id is used twice.");
            if (string.IsNullOrWhiteSpace(change.Author) || string.IsNullOrWhiteSpace(change.Reason) || change.RecordedAt == default)
                throw new ArgumentException($"{name}: author, reason and recordedAt are required.");
            if (change.SnapshotSha256 != sourceHash)
                throw new ArgumentException($"{name}: snapshotSha256 does not match this run's snapshot ({sourceHash}).");
            if (!Enum.IsDefined(change.Field) || !double.IsFinite(change.OriginalValue) || !double.IsFinite(change.ReplacementValue))
                throw new ArgumentException($"{name}: field must be one of {string.Join(", ", Enum.GetNames<OverrideField>())}, with finite originalValue and replacementValue.");
            var frame = effective.Model.Frames.FirstOrDefault(x => x.ObjectId == change.ObjectId);
            var area = effective.Areas.FirstOrDefault(x => x.ObjectId == change.ObjectId);
            if (frame is null && area is null) throw new ArgumentException($"{name}: no frame or area has objectId '{change.ObjectId}'.");
            if (change.Field == OverrideField.Excluded)
            {
                if (change.OriginalValue != (excluded.Contains(change.ObjectId) ? 1 : 0) || change.ReplacementValue is not (0 or 1))
                    throw new ArgumentException($"{name}: an exclusion uses 0 (included) or 1 (excluded), and originalValue must be the current state ({(excluded.Contains(change.ObjectId) ? 1 : 0)}).");
                if (change.ReplacementValue == change.OriginalValue)
                    throw new ArgumentException($"{name}: {change.ObjectId} is already {(change.OriginalValue == 1 ? "excluded" : "included")}; the override would change nothing.");
                if (change.ReplacementValue == 1) excluded.Add(change.ObjectId); else excluded.Remove(change.ObjectId);
                continue;
            }
            var (min, max) = Plausibility.OverrideRange(change.Field);
            if (change.ReplacementValue < min || change.ReplacementValue > max)
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"{name}: {Label(change.Field)} {Mm(change.ReplacementValue)} for {change.ObjectId} is outside the accepted override range {Mm(min)}–{Mm(max)}. Check the value and its unit."));
            SourceLength? current = change.Field switch
            {
                OverrideField.FrameWidthM => frame?.Section?.Width,
                OverrideField.FrameDepthM => frame?.Section?.Depth,
                OverrideField.FrameDiameterM => frame?.Section?.Diameter,
                OverrideField.AreaThicknessM => area?.Thickness,
                _ => null
            };
            if (current is null)
                throw new ArgumentException($"{name}: {change.ObjectId} has no {change.Field} to replace.");
            if (Math.Abs(current.Metres - change.OriginalValue) > 1e-12)
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"{name}: originalValue {change.OriginalValue} does not match the current {change.Field} of {change.ObjectId}, {current.Metres} m."));
            if (Math.Abs(change.ReplacementValue - current.Metres) <= effective.Policy.LinearToleranceM)
                throw new ArgumentException($"{name}: {change.ObjectId} {Label(change.Field)} is already {Mm(current.Metres)}; the override would change nothing.");
            var replacement = new SourceLength { Value = change.ReplacementValue, Unit = LengthUnit.Metre };
            if (change.Field == OverrideField.AreaThicknessM)
                effective = effective with { Areas = effective.Areas.Select(x => x.ObjectId == change.ObjectId ? x with { Thickness = replacement } : x).ToImmutableArray() };
            else
            {
                var section = frame!.Section!;
                section = change.Field switch
                {
                    OverrideField.FrameWidthM => section with { Width = replacement },
                    OverrideField.FrameDepthM => section with { Depth = replacement },
                    OverrideField.FrameDiameterM => section with { Diameter = replacement },
                    _ => section
                };
                effective = effective with { Model = effective.Model with
                { Frames = effective.Model.Frames.Select(x => x.ObjectId == change.ObjectId ? x with { Section = section } : x).ToImmutableArray() } };
                var described = $"override {change.Id} ({Label(change.Field)} {Mm(current.Metres)} → {Mm(change.ReplacementValue)})";
                sectionChanges[change.ObjectId] = sectionChanges.TryGetValue(change.ObjectId, out var earlier) ? earlier + ", " + described : described;
            }
        }
        // Dimension overrides invalidate previously asserted design/geometry matching.
        effective = effective with { Reinforcement = effective.Reinforcement.Select(x => sectionChanges.ContainsKey(x.ObjectId) && x.Method == SteelMethod.DemandEquivalent
            ? x with { DesignEvidence = DesignEvidence.Stale } : x).ToImmutableArray() };
        return (effective, excluded, sectionChanges);
    }

    public static string Label(OverrideField field) => field switch
    {
        OverrideField.FrameWidthM => "width", OverrideField.FrameDepthM => "depth", OverrideField.FrameDiameterM => "diameter",
        OverrideField.AreaThicknessM => "thickness", _ => "exclusion"
    };

    // Override dimensions are shown in millimetres, to 0.1 mm, everywhere a person reads them.
    // Values large enough to be typing errors use scientific notation rather than hundreds of digits.
    public static string Mm(double metres) => (Math.Abs(metres * 1000) < 1e7
        ? (Math.Round(metres * 1000, 1, MidpointRounding.AwayFromZero) + 0.0).ToString("0.#", CultureInfo.InvariantCulture)
        : (metres * 1000).ToString("0.###E+0", CultureInfo.InvariantCulture)) + " mm";

    // Builds an override from the run's current effective state, so users never type hashes or original values.
    public static QuantityOverride Prepare(TakeoffSnapshot source, string sourceHash, ImmutableArray<QuantityOverride> existing,
        string objectId, OverrideField field, double replacement, string reason, string author, DateTimeOffset at)
    {
        var (effective, excluded, _) = Apply(source, existing, sourceHash);
        var frame = effective.Model.Frames.FirstOrDefault(x => x.ObjectId == objectId);
        var area = effective.Areas.FirstOrDefault(x => x.ObjectId == objectId);
        if (frame is null && area is null) throw new ArgumentException($"No frame or area has objectId '{objectId}'.");
        var original = field switch
        {
            OverrideField.Excluded => excluded.Contains(objectId) ? 1 : 0,
            OverrideField.FrameWidthM => frame?.Section?.Width?.Metres,
            OverrideField.FrameDepthM => frame?.Section?.Depth?.Metres,
            OverrideField.FrameDiameterM => frame?.Section?.Diameter?.Metres,
            OverrideField.AreaThicknessM => area?.Thickness.Metres,
            _ => null
        } ?? throw new ArgumentException($"{objectId} has no {field} to replace.");
        var ids = existing.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var n = existing.Length + 1;
        while (ids.Contains("o" + n)) n++;
        return new("o" + n, objectId, field, original, replacement, reason, author, at, sourceHash);
    }
}
