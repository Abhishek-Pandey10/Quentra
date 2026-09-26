using System.Collections.Immutable;
using Quentra.Core;

namespace Quentra.Application;

public enum OverrideField { FrameWidthM, FrameDepthM, FrameDiameterM, AreaThicknessM, Excluded }
public sealed record QuantityOverride(string Id, string ObjectId, OverrideField Field,
    double OriginalValue, double ReplacementValue, string Reason, string Author,
    DateTimeOffset RecordedAt, string SnapshotSha256);

public static class Overrides
{
    public static (TakeoffSnapshot Effective, HashSet<string> Excluded) Apply(TakeoffSnapshot source,
        ImmutableArray<QuantityOverride> changes, string sourceHash)
    {
        if (changes.IsDefault) throw new ArgumentException("Override ledger must be an array.");
        var effective = source;
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (change is null || string.IsNullOrWhiteSpace(change.Id) || !ids.Add(change.Id) ||
                string.IsNullOrWhiteSpace(change.Author) || string.IsNullOrWhiteSpace(change.Reason) || change.RecordedAt == default ||
                change.SnapshotSha256 != sourceHash || !Enum.IsDefined(change.Field) || !double.IsFinite(change.OriginalValue) || !double.IsFinite(change.ReplacementValue))
                throw new ArgumentException("Overrides require unique IDs, finite values, reason, author, date and a matching source snapshot hash.");
            var frame = effective.Model.Frames.FirstOrDefault(x => x.ObjectId == change.ObjectId);
            var area = effective.Areas.FirstOrDefault(x => x.ObjectId == change.ObjectId);
            if (frame is null && area is null) throw new ArgumentException("Override refers to an unknown object.");
            if (change.Field == OverrideField.Excluded)
            {
                if (change.OriginalValue != (excluded.Contains(change.ObjectId) ? 1 : 0) || change.ReplacementValue is not (0 or 1))
                    throw new ArgumentException("Exclusion override must match current state and use 0 or 1.");
                if (change.ReplacementValue == 1) excluded.Add(change.ObjectId); else excluded.Remove(change.ObjectId);
                continue;
            }
            if (change.ReplacementValue <= 0) throw new ArgumentException("Replacement dimension must be positive.");
            SourceLength? current = change.Field switch
            {
                OverrideField.FrameWidthM => frame?.Section?.Width,
                OverrideField.FrameDepthM => frame?.Section?.Depth,
                OverrideField.FrameDiameterM => frame?.Section?.Diameter,
                OverrideField.AreaThicknessM => area?.Thickness,
                _ => null
            };
            if (current is null || Math.Abs(current.Metres - change.OriginalValue) > 1e-12)
                throw new ArgumentException("Override original value does not match the current dimension in metres.");
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
            }
        }
        // Dimension overrides invalidate previously asserted design/geometry matching.
        var changedFrames = changes.Where(x => x.Field is OverrideField.FrameWidthM or OverrideField.FrameDepthM or OverrideField.FrameDiameterM)
            .Select(x => x.ObjectId).ToHashSet(StringComparer.Ordinal);
        effective = effective with { Reinforcement = effective.Reinforcement.Select(x => changedFrames.Contains(x.ObjectId) && x.Method == SteelMethod.DemandEquivalent
            ? x with { DesignEvidence = DesignEvidence.Stale } : x).ToImmutableArray() };
        return (effective, excluded);
    }
}
