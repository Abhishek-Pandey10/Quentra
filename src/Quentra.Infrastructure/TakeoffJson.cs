using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Quentra.Application;
using Quentra.Core;

namespace Quentra.Infrastructure;

// Who recorded an acceptance, as the computer reports it. Recorded, not verified: it is not a signature.
// Omitted from runs that do not have it, so earlier runs keep their hashes.
public sealed record ReviewEntry(string Status, string Reviewer, DateTimeOffset ReviewedAt,
    string CalculationSha256, bool AcceptPartial, string Note, ImmutableArray<string> AcknowledgedWarningCodes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RecordedByAccount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RecordedOnComputer = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RecordedWithVersion = null);

public sealed record ReviewerContext(string Account, string Computer, string QuentraVersion)
{
    public static ReviewerContext Current() => new(Environment.UserName, Environment.MachineName, TakeoffJson.QuentraVersion);
}
public sealed record TakeoffRun(int SchemaVersion, string SnapshotSha256, string CalculationSha256,
    TakeoffSnapshot Snapshot, ImmutableArray<QuantityOverride> Overrides, TakeoffResult Result,
    ImmutableArray<ReviewEntry> ReviewHistory, string PackageSha256);

public static class TakeoffJson
{
    public static JsonSerializerOptions Options(bool indented = false)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = indented, NewLine = "\n",
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true,
            MaxDepth = 64
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.Converters.Add(new UtcDateTimeConverter());
        return options;
    }

    public static T Parse<T>(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        CheckDuplicates(doc.RootElement);
        return JsonSerializer.Deserialize<T>(json, Options()) ?? throw new JsonException("Document cannot be null.");
    }
    public static int SchemaVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("schemaVersion").GetInt32();
    }
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options(true)) + "\n";
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Options()))).ToLowerInvariant();

    public static TakeoffSnapshot Canonical(TakeoffSnapshot s)
    {
        TakeoffEngine.Validate(s);
        return s with
        {
            Model = s.Model with { Frames = s.Model.Frames.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ToImmutableArray() },
            Areas = s.Areas.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ToImmutableArray(),
            Stories = s.Stories.OrderBy(x => x.LowerElevationM).ToImmutableArray(),
            Metadata = s.Metadata.OrderBy(x => x.ObjectId, StringComparer.Ordinal).Select(x => x with
                { RequiredSteelComponents = x.RequiredSteelComponents.Order(StringComparer.Ordinal).ToImmutableArray() }).ToImmutableArray(),
            Reinforcement = s.Reinforcement.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Component, StringComparer.Ordinal).ToImmutableArray(),
            SourceWarnings = s.SourceWarnings.IsDefault ? s.SourceWarnings : s.SourceWarnings.OrderBy(x => x.ObjectId ?? "", StringComparer.Ordinal)
                .ThenBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Message, StringComparer.Ordinal).ToImmutableArray()
        };
    }

    public static TakeoffRun Calculate(TakeoffSnapshot snapshot, ImmutableArray<QuantityOverride> overrides = default,
        ImmutableArray<ReviewEntry> history = default, CancellationToken cancellationToken = default)
    {
        snapshot = Canonical(snapshot);
        if (overrides.IsDefault) overrides = [];
        if (history.IsDefault) history = [];
        var sourceHash = Hash(snapshot);
        var applied = Application.Overrides.Apply(snapshot, overrides, sourceHash);
        var result = TakeoffEngine.Run(applied.Effective, cancellationToken, applied.Excluded, applied.SectionChanges);
        var hash = Hash(new { sourceHash, overrides, result });
        return Seal(new(2, sourceHash, hash, snapshot, overrides, result, history, ""));
    }

    public static TakeoffRun Replay(string json, CancellationToken cancellationToken = default)
    {
        var run = Parse<TakeoffRun>(json);
        if (run.SchemaVersion != 2 || run.Overrides.IsDefault || run.ReviewHistory.IsDefault)
            throw new InvalidDataException("Incomplete takeoff run: schemaVersion must be 2, with overrides and reviewHistory arrays.");
        if (run.Result.CalculationVersion != TakeoffEngine.Version)
            throw new InvalidDataException($"This run was calculated by engine {run.Result.CalculationVersion}; this Quentra has {TakeoffEngine.Version} and replays only its own runs. " +
                "Calculate the snapshot again with this version (the run keeps it under \"snapshot\"), or use the Quentra version that wrote the run.");
        if (Seal(run).PackageSha256 != run.PackageSha256)
            throw new InvalidDataException($"Run package integrity check failed: {Tampered(run, cancellationToken)} changed after Quentra wrote the file. " +
                "Use the original run file, or calculate its snapshot again.");
        var actual = Calculate(run.Snapshot, run.Overrides, run.ReviewHistory, cancellationToken);
        if (run.SnapshotSha256 != actual.SnapshotSha256 || run.CalculationSha256 != actual.CalculationSha256 || Hash(run.Result) != Hash(actual.Result))
            throw new InvalidDataException($"Saved quantities do not reproduce: {Tampered(run, cancellationToken)} differ from a recalculation.");
        foreach (var review in run.ReviewHistory)
        {
            if (review is null || string.IsNullOrWhiteSpace(review.Reviewer) || review.ReviewedAt == default || review.AcknowledgedWarningCodes.IsDefault ||
                review.Status is not ("Accepted" or "AcceptedPartial") || string.IsNullOrWhiteSpace(review.Note) || string.IsNullOrWhiteSpace(review.CalculationSha256))
                throw new InvalidDataException("Malformed review history.");
            if (review.CalculationSha256 == actual.CalculationSha256) ValidateReview(actual, review);
        }
        return actual;
    }

    // Says which part of an altered run no longer matches, so the user knows what was edited.
    private static string Tampered(TakeoffRun run, CancellationToken token)
    {
        TakeoffRun actual;
        try { actual = Calculate(run.Snapshot, run.Overrides, run.ReviewHistory, token); }
        catch (ArgumentException) { return "the snapshot or override ledger (it is no longer valid)"; }
        if (actual.SnapshotSha256 != run.SnapshotSha256) return "the snapshot or its recorded hash";
        if (Hash(actual.Result) != Hash(run.Result))
        {
            string[] Rows(TakeoffResult r) =>
            [
                .. r.Elements.IsDefault ? [] : r.Elements.Select(x => x.ObjectId + "\u0000" + Hash(x)),
                .. r.Steel.IsDefault ? [] : r.Steel.Select(x => x.ObjectId + "\u0000" + Hash(x)),
                .. r.StoryAllocations.IsDefault ? [] : r.StoryAllocations.Select(x => x.ObjectId + "\u0000" + Hash(x))
            ];
            var ids = Rows(run.Result).Except(Rows(actual.Result)).Concat(Rows(actual.Result).Except(Rows(run.Result)))
                .Select(x => x[..x.IndexOf('\u0000')]).Distinct().Order(StringComparer.Ordinal).ToArray();
            return ids.Length == 0 ? "the saved totals or warnings"
                : $"the saved quantities of {string.Join(", ", ids.Take(10))}{(ids.Length > 10 ? $" and {ids.Length - 10} more" : "")}";
        }
        if (actual.CalculationSha256 != run.CalculationSha256) return "the override ledger or the calculation hash";
        return "the review history (reviewer, note, date or acknowledgements)";
    }

    // Elements taken out of scope by an override, with the override that did it, e.g. "W1 (o1)".
    public static IReadOnlyList<string> Exclusions(TakeoffRun run)
    {
        var by = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var o in run.Overrides.Where(x => x.Field == OverrideField.Excluded))
            if (o.ReplacementValue == 1) by[o.ObjectId] = o.Id; else by.Remove(o.ObjectId);
        return by.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key} ({x.Value})").ToArray();
    }

    // A total is "complete" only relative to a scope; a hand exclusion reduces that scope, and the label must say so.
    // Until a reviewer has accepted the run (acknowledging each flagged element), a total containing implausible values
    // (probable unit slips) is labelled as including them, so it is never read as an unqualified complete quantity.
    public static string CompleteLabel(TakeoffRun run)
    {
        var label = Exclusions(run) is { Count: > 0 } excluded
            ? $"complete in scope reduced by override (excludes {List(excluded)})" : "complete in declared scope";
        var flagged = FlaggedElements(run, PerElementCodes);
        if (flagged.Count == 0) return label;
        return ReviewStatus(run) == "Draft"
            ? $"{label}, including unreviewed implausible values in {List(flagged)}"
            : $"{label}, including implausible values in {List(flagged)} accepted by the reviewer";
    }

    // One line saying what a report or screen of this run is: never lets a draft pass for a reviewed quantity.
    public static string StatusBanner(TakeoffRun run)
    {
        var last = run.ReviewHistory.LastOrDefault();
        var day = last?.ReviewedAt.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return ReviewStatus(run) switch
        {
            "Accepted" => $"ACCEPTED by {last!.Reviewer} on {day} UTC for this exact calculation.",
            "AcceptedPartial" => $"ACCEPTED AS PARTIAL SCOPE by {last!.Reviewer} on {day} UTC: some quantities are unknown or excluded; see the Summary.",
            _ => "DRAFT - NOT ACCEPTED. Calculated but not reviewed; do not use as a reviewed quantity." +
                 (run.ReviewHistory.Length > 0 ? " An earlier acceptance applies to a different calculation (the run changed after it)." : "")
        };
    }

    private static string List(IReadOnlyList<string> items) =>
        items.Count <= 10 ? string.Join(", ", items) : string.Join(", ", items.Take(10)) + $" and {items.Count - 10} more";

    public static TakeoffRun AddOverrides(TakeoffRun run, ImmutableArray<QuantityOverride> changes) =>
        Calculate(run.Snapshot, run.Overrides.AddRange(changes), run.ReviewHistory);

    public static string QuentraVersion =>
        typeof(TakeoffJson).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "unknown";

    public static TakeoffRun Accept(TakeoffRun run, string reviewer, string note, bool acceptPartial, ImmutableArray<string> acknowledgedCodes, DateTimeOffset at,
        ReviewerContext? recordedBy = null)
    {
        var partial = IsPartial(run);
        var review = new ReviewEntry(partial ? "AcceptedPartial" : "Accepted", reviewer, at, run.CalculationSha256, acceptPartial, note, acknowledgedCodes,
            recordedBy?.Account, recordedBy?.Computer, recordedBy?.QuentraVersion);
        ValidateReview(run, review);
        return Seal(run with { ReviewHistory = run.ReviewHistory.Add(review) });
    }

    public static string ReviewStatus(TakeoffRun run) => run.ReviewHistory.LastOrDefault()?.CalculationSha256 == run.CalculationSha256
        ? run.ReviewHistory[^1].Status : "Draft";
    public static bool IsPartial(TakeoffRun run) => run.Result.Summary.CompleteOpeningAdjustedM3 is null || run.Result.Summary.CompleteSteelKg is null ||
        run.Result.Elements.Any(x => x.Warnings.Any(w => w.Code == "USER_EXCLUDED"));
    public static IEnumerable<string> WarningCodes(TakeoffRun run) => run.Result.Warnings
        .Concat(run.Result.Elements.SelectMany(x => x.Warnings)).Concat(run.Result.Steel.SelectMany(x => x.Warnings)).Select(x => x.Code).Distinct(StringComparer.Ordinal);

    // An implausible value (usually a unit slip) can dominate a total, so the reviewer names each flagged
    // element (IMPLAUSIBLE_DIMENSION:B1); one code must not wave through every such element in the run.
    public static readonly string[] PerElementCodes = ["IMPLAUSIBLE_DIMENSION", "IMPLAUSIBLE_STEEL_INTENSITY"];

    public static IEnumerable<string> RequiredAcknowledgements(TakeoffRun run) => WarningCodes(run).SelectMany(code => PerElementCodes.Contains(code)
        ? FlaggedElements(run, code).Select(id => code + ":" + id) : [code]);

    public static IReadOnlyList<string> FlaggedElements(TakeoffRun run, params string[] codes) => run.Result.Elements
        .Where(x => x.Warnings.Any(w => codes.Contains(w.Code))).Select(x => x.ObjectId).ToArray();

    // Every problem is reported at once, so a reviewer does not fix them one run at a time.
    private static void ValidateReview(TakeoffRun run, ReviewEntry review)
    {
        if (review.CalculationSha256 != run.CalculationSha256) throw new ArgumentException("Review is bound to a different calculation.");
        var problems = new List<string>();
        if (run.Snapshot.Policy.ApprovedAt is null || string.IsNullOrWhiteSpace(run.Snapshot.Policy.ApprovedBy))
            problems.Add(run.Snapshot.Source is not null
                ? "The measurement policy has not been approved. Extract again naming the approver (--policy-approved-by \"Name\", or 'Measurement policy approved by' in the GUI), then calculate."
                : "Approve the measurement policy (policy.approvedBy and approvedAt in the snapshot) and calculate again.");
        if (run.Result.Summary.InScope == 0) problems.Add("An empty scope cannot be accepted.");
        if (string.IsNullOrWhiteSpace(review.Reviewer) || string.IsNullOrWhiteSpace(review.Note) || review.ReviewedAt == default || review.AcknowledgedWarningCodes.IsDefault)
            problems.Add("Review requires a named reviewer, date, note and warning acknowledgments.");
        var partial = IsPartial(run);
        if (partial && !review.AcceptPartial)
            problems.Add($"This run is partial ({string.Join("; ", PartialReasons(run))}). Accept it explicitly as partial scope: --partial on the command line, or 'Accept as partial scope' in the GUI.");
        if (review.Status != (partial ? "AcceptedPartial" : "Accepted")) problems.Add("Review completeness label is inconsistent.");
        if (!review.AcknowledgedWarningCodes.IsDefault)
        {
            var required = RequiredAcknowledgements(run).ToArray();
            if (review.AcknowledgedWarningCodes.Where(PerElementCodes.Contains)
                .Select(code => required.FirstOrDefault(x => x.StartsWith(code + ":", StringComparison.Ordinal))).FirstOrDefault(x => x is not null) is { } example)
                problems.Add($"{example[..example.IndexOf(':')]} is acknowledged per element. Name each one, for example {example}.");
            if (required.Except(review.AcknowledgedWarningCodes, StringComparer.Ordinal).ToArray() is { Length: > 0 } missing)
                problems.Add("Acknowledge every current warning before acceptance; acknowledgment does not resolve missing quantities. Missing: " + string.Join(",", missing));
            if (review.AcknowledgedWarningCodes.Except(required, StringComparer.Ordinal).Except(PerElementCodes, StringComparer.Ordinal).ToArray() is { Length: > 0 } unknown)
                problems.Add("These acknowledged codes are not raised by this run: " + string.Join(",", unknown));
        }
        if (problems.Count == 1) throw new ArgumentException(problems[0]);
        if (problems.Count > 1) throw new ArgumentException("Acceptance refused:\n  - " + string.Join("\n  - ", problems));
    }

    private static IEnumerable<string> PartialReasons(TakeoffRun run)
    {
        if (run.Result.Summary.CompleteOpeningAdjustedM3 is null) yield return "concrete is incomplete";
        if (run.Result.Summary.CompleteSteelKg is null) yield return "steel is incomplete";
        if (Exclusions(run) is { Count: > 0 } excluded) yield return "excluded by override: " + List(excluded);
    }

    private static TakeoffRun Seal(TakeoffRun run) => run with { PackageSha256 = Hash(run with { PackageSha256 = "" }) };

    public static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(path); var directory = Path.GetDirectoryName(full)!;
        try { Directory.CreateDirectory(directory); }
        catch (IOException e) { throw SnapshotJson.FolderFailed(directory, e); }
        var temp = Path.Combine(directory, ".quentra-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temp, Serialize(value), new UTF8Encoding(false), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); File.Move(temp, full, false);
        }
        catch (IOException e) { throw SnapshotJson.WriteFailed(full, e); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in element.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw new JsonException("Duplicate JSON property: " + p.Name);
                CheckDuplicates(p.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var value in element.EnumerateArray()) CheckDuplicates(value);
    }
}
