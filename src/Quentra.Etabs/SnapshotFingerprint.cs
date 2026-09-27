using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Quentra.Core;

namespace Quentra.Etabs;

public sealed record SourceComparison(ImmutableArray<string> Changed, ImmutableArray<string> Added, ImmutableArray<string> Removed, bool StoriesChanged)
{
    public bool Matches => Changed.IsEmpty && Added.IsEmpty && Removed.IsEmpty && !StoriesChanged;

    public string Describe()
    {
        if (Matches) return "The model matches: every element, story and steel input is the same as when the run was extracted.";
        static string List(ImmutableArray<string> ids) => ids.Length <= 10 ? string.Join(", ", ids) : string.Join(", ", ids.Take(10)) + $" and {ids.Length - 10} more";
        var parts = new List<string>();
        if (!Changed.IsEmpty) parts.Add($"{Changed.Length} changed ({List(Changed)})");
        if (!Added.IsEmpty) parts.Add($"{Added.Length} added ({List(Added)})");
        if (!Removed.IsEmpty) parts.Add($"{Removed.Length} removed ({List(Removed)})");
        if (StoriesChanged) parts.Add("stories changed");
        return "The ETABS model has changed since this run was extracted: " + string.Join("; ", parts) + ".";
    }
}

// Decides whether the model in ETABS still matches a snapshot, element by element. Only what determines quantities is
// compared: geometry, sections, materials, openings, stories, steel evidence and source warnings. Timestamps, approvals,
// process ids and the ETABS display units are ignored; numbers are compared to 10 significant digits, so the same model
// read in other units matches.
public static class SnapshotFingerprint
{
    public static SourceComparison Compare(TakeoffSnapshot extracted, TakeoffSnapshot current)
    {
        var a = Elements(extracted); var b = Elements(current);
        return new([.. a.Keys.Intersect(b.Keys).Where(k => a[k] != b[k]).Order(StringComparer.Ordinal)],
            [.. b.Keys.Except(a.Keys).Order(StringComparer.Ordinal)], [.. a.Keys.Except(b.Keys).Order(StringComparer.Ordinal)],
            Stories(extracted) != Stories(current));
    }

    public static IReadOnlyDictionary<string, string> Elements(TakeoffSnapshot s)
    {
        var meta = s.Metadata.ToDictionary(x => x.ObjectId, StringComparer.Ordinal);
        var steel = s.Reinforcement.ToLookup(x => x.ObjectId, StringComparer.Ordinal);
        var warnings = (s.SourceWarnings.IsDefault ? [] : s.SourceWarnings).Where(x => x.ObjectId is not null).ToLookup(x => x.ObjectId!, StringComparer.Ordinal);
        var factor = Units.MetresPerUnit(s.Model.CoordinateUnit);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in s.Model.Frames)
            result[f.ObjectId] = Hash($"F|{f.Kind}|{f.Material}|{f.IsStraight}|{f.IsPrismatic}|{P(f.Start, factor)}|{P(f.End, factor)}|{f.Section?.Shape}|{L(f.Section?.Width)}|{L(f.Section?.Depth)}|{L(f.Section?.Diameter)}|" +
                Tail(f.ObjectId, meta, steel, warnings));
        foreach (var a in s.Areas)
            result[a.ObjectId] = Hash($"A|{a.Kind}|{a.Material}|{a.PhysicalThicknessVerified}|{a.OpeningsVerified}|{L(a.Thickness)}|{string.Join(";", a.Boundary.Select(p => P(p, factor)))}|" +
                string.Join("/", a.Openings.Select(o => string.Join(";", o.Select(p => P(p, factor))))) + "|" + Tail(a.ObjectId, meta, steel, warnings));
        return result;
    }

    private static string Stories(TakeoffSnapshot s) => string.Join(";", s.Stories.OrderBy(x => x.LowerElevationM).Select(x => $"{x.Id}:{N(x.LowerElevationM)}:{N(x.UpperElevationM)}"));

    private static string Tail(string id, Dictionary<string, ElementMetadata> meta, ILookup<string, SteelInput> steel, ILookup<string, SourceWarning> warnings)
    {
        var m = meta.GetValueOrDefault(id);
        return $"{m?.MaterialName}|{m?.SectionName}|{m?.AssignedStoryId}|{string.Join(",", m is null ? [] : m.RequiredSteelComponents.Order(StringComparer.Ordinal))}|" +
            string.Join("/", steel[id].OrderBy(x => x.Component, StringComparer.Ordinal).Select(x =>
                $"{x.Component}:{x.Method}:{x.DesignEvidence}:{x.DesignCode}:{x.Ratio?.ToString("G10", CultureInfo.InvariantCulture)}:" +
                string.Join(";", x.Stations.Select(st => $"{N(st.PositionM)}={N(st.AreaM2)}")) + ":" + string.Join(";", x.Bars.Select(b => $"{b.Count}x{N(b.AreaM2)}x{N(b.LengthM)}")))) + "|" +
            string.Join("/", warnings[id].Select(w => w.Code).Order(StringComparer.Ordinal));
    }

    private static string N(double v) => v.ToString("G10", CultureInfo.InvariantCulture);
    private static string L(SourceLength? l) => l is null ? "-" : N(l.Metres);
    private static string P(Point3 p, double f) => $"{N(p.X * f)},{N(p.Y * f)},{N(p.Z * f)}";
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
