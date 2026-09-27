using System.Collections.Immutable;
using Quentra.Core;

namespace Quentra.Application;

public sealed record HealthWarning(string Code, int Count, string Example);

// A pre-calculation summary of a snapshot, built from the same validation and warnings as a calculation.
// Errors that make the snapshot unreadable block calculation. Invalid elements do not block it (their concrete
// is unknown and the totals say so), but they are listed as errors because a complete total is impossible.
public sealed record ModelHealth(ImmutableArray<string> Problems, ImmutableArray<string> InvalidElements, ImmutableArray<string> UnsupportedElements,
    ImmutableArray<HealthWarning> Warnings, TakeoffSummary? Summary)
{
    public bool Blocked => Problems.Length > 0;

    public string Status => Blocked
        ? $"Blocked by {Problems.Length} error{(Problems.Length == 1 ? "" : "s")}; fix the source model or snapshot, then extract again."
        : InvalidElements.Length + UnsupportedElements.Length > 0
            ? $"Ready to calculate. Concrete will be partial: {InvalidElements.Length} invalid and {UnsupportedElements.Length} unsupported element{(InvalidElements.Length + UnsupportedElements.Length == 1 ? "" : "s")} cannot be quantified."
            : "Ready to calculate.";

    public static ModelHealth Check(TakeoffSnapshot snapshot, CancellationToken token = default)
    {
        var problems = TakeoffEngine.Problems(snapshot).ToImmutableArray();
        if (problems.Length > 0) return new(problems, [], [], [], null);
        var result = TakeoffEngine.Run(snapshot, token);
        var all = result.Warnings.Select(w => (Subject: "run", w.Code, w.Message))
            .Concat(result.Elements.SelectMany(e => e.Warnings.Select(w => (Subject: e.ObjectId, w.Code, w.Message))))
            .Concat(result.Steel.SelectMany(s => s.Warnings.Select(w => (Subject: $"{s.ObjectId}/{s.Component}", w.Code, w.Message))));
        var warnings = all.GroupBy(x => x.Code, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new HealthWarning(g.Key, g.Count(), g.First().Subject == "run" ? g.First().Message : $"{g.First().Subject}: {g.First().Message}")).ToImmutableArray();
        return new([], Ids(result, QuantityStatus.Invalid), Ids(result, QuantityStatus.Unsupported), warnings, result.Summary);
    }

    private static ImmutableArray<string> Ids(TakeoffResult result, QuantityStatus status) =>
        [.. result.Elements.Where(x => x.Status == status).Select(x => x.ObjectId)];
}
