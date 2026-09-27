using System.Globalization;
using System.Text.Json;
using Quentra.Core;
using Quentra.Infrastructure;

namespace Quentra.EtabsFixtures;

// Compares a calculated run with the hand-calculated expectations in fixtures/etabs/v22.7/golden.json.
// Shared by the ETABS integration suite (live extraction) and the unit tests (committed raw captures).
public static class GoldenCheck
{
    // The build options a golden entry asks for (a named steel approver, a wider station gap).
    public static Etabs.EtabsBuildOptions Options(JsonElement expected) => new()
    {
        SteelApprovedBy = expected.TryGetProperty("steelApprovedBy", out var a) ? a.GetString() : null,
        MaximumStationGapM = expected.TryGetProperty("maxStationGapM", out var g) ? g.GetDouble() : Etabs.EtabsBuildOptions.DefaultMaximumStationGapM
    };

    public static IEnumerable<(string Test, bool Passed, string Detail)> Compare(JsonElement expected, TakeoffRun run, double tolerance)
    {
        var s = run.Result.Summary;
        foreach (var (field, actual) in new (string, double?)[]
        {
            ("knownGrossM3", s.KnownGrossM3), ("knownOpeningAdjustedM3", s.KnownOpeningAdjustedM3),
            ("completeGrossM3", s.CompleteGrossM3), ("completeOpeningAdjustedM3", s.CompleteOpeningAdjustedM3)
        })
            if (expected.TryGetProperty(field, out var e))
                yield return Number(field, e.ValueKind == JsonValueKind.Null ? null : e.GetDouble(), actual, tolerance);
        foreach (var (field, actual) in new[] { ("quantified", s.Quantified), ("unsupported", s.Unsupported), ("invalid", s.Invalid), ("outOfScope", s.OutOfScope) })
            if (expected.TryGetProperty(field, out var e))
                yield return (field, e.GetInt32() == actual, $"expected {e.GetInt32()}, got {actual}");
        if (expected.TryGetProperty("byCategory", out var categories))
            foreach (var c in categories.EnumerateObject())
                yield return Number($"category {c.Name}", c.Value.GetDouble(), run.Result.ByCategory.FirstOrDefault(x => x.Group == c.Name)?.KnownGrossM3, tolerance);
        if (expected.TryGetProperty("byStory", out var stories))
        {
            var actual = run.Result.StoryAllocations.GroupBy(x => x.StoryId).ToDictionary(g => g.Key, g => g.Sum(x => x.GrossM3));
            foreach (var st in stories.EnumerateObject())
                yield return Number($"story {st.Name}", st.Value.GetDouble(), actual.TryGetValue(st.Name, out var v) ? v : null, tolerance);
            var unexpected = actual.Keys.Except(stories.EnumerateObject().Select(x => x.Name)).ToArray();
            yield return ("no other stories", unexpected.Length == 0, unexpected.Length == 0 ? "none" : string.Join(", ", unexpected));
        }
        var codes = TakeoffJson.WarningCodes(run).ToHashSet(StringComparer.Ordinal);
        if (expected.TryGetProperty("warnings", out var present))
            foreach (var code in present.EnumerateArray().Select(x => x.GetString()!))
                yield return ($"raises {code}", codes.Contains(code), codes.Contains(code) ? "present" : "missing");
        if (expected.TryGetProperty("absentWarnings", out var absent))
            foreach (var code in absent.EnumerateArray().Select(x => x.GetString()!))
                yield return ($"does not raise {code}", !codes.Contains(code), codes.Contains(code) ? "present" : "absent");
        if (expected.TryGetProperty("steelByEvidenceKg", out var evidence))
            foreach (var ev in evidence.EnumerateObject())
            {
                var rows = run.Result.Steel.Where(x => x.Evidence == ev.Name).ToArray();
                yield return Number($"steel {ev.Name}", ev.Value.GetDouble(), rows.Length == 0 ? null : rows.Sum(x => x.MassKg ?? 0), Math.Max(tolerance, 1e-6));
            }
        if (expected.TryGetProperty("demandIntegration", out var integrate) && integrate.GetBoolean())
        {
            // Independent recomputation: sum over intervals of the larger end area × length × density.
            var inputs = run.Snapshot.Reinforcement.Where(x => x.Method == SteelMethod.DemandEquivalent).ToArray();
            var worst = 0.0; var counted = 0;
            foreach (var input in inputs)
            {
                var steel = run.Result.Steel.Single(x => x.ObjectId == input.ObjectId && x.Component == input.Component);
                if (steel.MassKg is not { } mass) continue;
                counted++;
                var st = input.Stations.OrderBy(x => x.PositionM).ToArray();
                var hand = Enumerable.Range(1, st.Length - 1).Sum(i => Math.Max(st[i - 1].AreaM2, st[i].AreaM2) * (st[i].PositionM - st[i - 1].PositionM)) * run.Snapshot.Policy.SteelDensityKgM3;
                worst = Math.Max(worst, Math.Abs(hand - mass));
            }
            yield return ("design demand integration", counted > 0 && worst <= 1e-6, $"{counted} demand components recomputed; largest difference {worst:E2} kg");
        }
    }

    // Meshing must not change frames or slabs at all. ETABS rewrites a wall containing an opening into wall pieces around
    // the opening, so walls keep their opening-adjusted volume while their gross volume loses exactly the opening deduction.
    public static IEnumerable<(string, bool, string)> MeshInvariance(TakeoffResult before, TakeoffResult after, double tolerance)
    {
        double Sum(TakeoffResult r, string category, Func<ElementTakeoff, double?> value) => r.Elements.Where(x => x.Category == category).Sum(x => value(x) ?? 0);
        foreach (var category in new[] { "Beam", "Column", "Slab" })
            yield return ($"{category} volume unchanged", Math.Abs(Sum(before, category, x => x.GrossM3) - Sum(after, category, x => x.GrossM3)) <= tolerance &&
                Math.Abs(Sum(before, category, x => x.OpeningAdjustedM3) - Sum(after, category, x => x.OpeningAdjustedM3)) <= tolerance,
                $"gross {Sum(before, category, x => x.GrossM3):F6} / {Sum(after, category, x => x.GrossM3):F6} m³");
        yield return ("opening-adjusted total unchanged", Math.Abs(before.Summary.KnownOpeningAdjustedM3 - after.Summary.KnownOpeningAdjustedM3) <= tolerance,
            $"{before.Summary.KnownOpeningAdjustedM3:F6} / {after.Summary.KnownOpeningAdjustedM3:F6} m³");
        var wallDeduction = Sum(before, "Wall", x => x.OpeningDeductionM3);
        yield return ("wall gross changes only by its opening deduction", Math.Abs(Sum(before, "Wall", x => x.GrossM3) - wallDeduction - Sum(after, "Wall", x => x.GrossM3)) <= tolerance,
            $"wall gross {Sum(before, "Wall", x => x.GrossM3):F6} - opening {wallDeduction:F6} = {Sum(after, "Wall", x => x.GrossM3):F6} m³ after meshing");
    }

    private static (string, bool, string) Number(string test, double? expected, double? actual, double tolerance)
    {
        var ok = expected is null ? actual is null : actual is { } a && Math.Abs(a - expected.Value) <= tolerance * Math.Max(1, Math.Abs(expected.Value));
        return (test, ok, $"expected {Show(expected)}, got {Show(actual)}");
        static string Show(double? v) => v is { } x ? x.ToString("0.000000", CultureInfo.InvariantCulture) : "unknown";
    }
}
