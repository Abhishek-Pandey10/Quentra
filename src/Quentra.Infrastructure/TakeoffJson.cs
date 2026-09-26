using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Quentra.Application;
using Quentra.Core;

namespace Quentra.Infrastructure;

public sealed record ReviewEntry(string Status, string Reviewer, DateTimeOffset ReviewedAt,
    string CalculationSha256, bool AcceptPartial, string Note, ImmutableArray<string> AcknowledgedWarningCodes);
public sealed record TakeoffRun(int SchemaVersion, string SnapshotSha256, string CalculationSha256,
    TakeoffSnapshot Snapshot, ImmutableArray<QuantityOverride> Overrides, TakeoffResult Result,
    ImmutableArray<ReviewEntry> ReviewHistory, string PackageSha256);

public static class TakeoffJson
{
    public static JsonSerializerOptions Options(bool indented = false)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = indented,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true,
            MaxDepth = 64
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
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
            Reinforcement = s.Reinforcement.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Component, StringComparer.Ordinal).ToImmutableArray()
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
        var result = TakeoffEngine.Run(applied.Effective, cancellationToken, applied.Excluded);
        var hash = Hash(new { sourceHash, overrides, result });
        return Seal(new(2, sourceHash, hash, snapshot, overrides, result, history, ""));
    }

    public static TakeoffRun Replay(string json, CancellationToken cancellationToken = default)
    {
        var run = Parse<TakeoffRun>(json);
        if (run.SchemaVersion != 2 || run.Result.CalculationVersion != TakeoffEngine.Version || run.Overrides.IsDefault || run.ReviewHistory.IsDefault)
            throw new InvalidDataException("Unsupported or incomplete takeoff run.");
        if (Seal(run).PackageSha256 != run.PackageSha256) throw new InvalidDataException("Run package integrity check failed.");
        var actual = Calculate(run.Snapshot, run.Overrides, run.ReviewHistory, cancellationToken);
        if (run.SnapshotSha256 != actual.SnapshotSha256 || run.CalculationSha256 != actual.CalculationSha256 || Hash(run.Result) != Hash(actual.Result))
            throw new InvalidDataException("Saved quantities do not reproduce.");
        foreach (var review in run.ReviewHistory)
        {
            if (review is null || string.IsNullOrWhiteSpace(review.Reviewer) || review.ReviewedAt == default || review.AcknowledgedWarningCodes.IsDefault ||
                review.Status is not ("Accepted" or "AcceptedPartial") || string.IsNullOrWhiteSpace(review.Note) || string.IsNullOrWhiteSpace(review.CalculationSha256))
                throw new InvalidDataException("Malformed review history.");
            if (review.CalculationSha256 == actual.CalculationSha256) ValidateReview(actual, review);
        }
        return actual;
    }

    public static TakeoffRun AddOverrides(TakeoffRun run, ImmutableArray<QuantityOverride> changes) =>
        Calculate(run.Snapshot, run.Overrides.AddRange(changes), run.ReviewHistory);

    public static TakeoffRun Accept(TakeoffRun run, string reviewer, string note, bool acceptPartial, ImmutableArray<string> acknowledgedCodes, DateTimeOffset at)
    {
        var partial = IsPartial(run);
        var review = new ReviewEntry(partial ? "AcceptedPartial" : "Accepted", reviewer, at, run.CalculationSha256, acceptPartial, note, acknowledgedCodes);
        ValidateReview(run, review);
        return Seal(run with { ReviewHistory = run.ReviewHistory.Add(review) });
    }

    public static string ReviewStatus(TakeoffRun run) => run.ReviewHistory.LastOrDefault()?.CalculationSha256 == run.CalculationSha256
        ? run.ReviewHistory[^1].Status : "Draft";
    public static bool IsPartial(TakeoffRun run) => run.Result.Summary.CompleteOpeningAdjustedM3 is null || run.Result.Summary.CompleteSteelKg is null ||
        run.Result.Elements.Any(x => x.Warnings.Any(w => w.Code == "USER_EXCLUDED"));
    public static IEnumerable<string> WarningCodes(TakeoffRun run) => run.Result.Warnings
        .Concat(run.Result.Elements.SelectMany(x => x.Warnings)).Concat(run.Result.Steel.SelectMany(x => x.Warnings)).Select(x => x.Code).Distinct(StringComparer.Ordinal);

    private static void ValidateReview(TakeoffRun run, ReviewEntry review)
    {
        if (run.Snapshot.Policy.ApprovedAt is null || string.IsNullOrWhiteSpace(run.Snapshot.Policy.ApprovedBy))
            throw new ArgumentException("Approve the measurement policy before accepting a report.");
        if (run.Result.Summary.InScope == 0) throw new ArgumentException("An empty scope cannot be accepted.");
        if (string.IsNullOrWhiteSpace(review.Reviewer) || string.IsNullOrWhiteSpace(review.Note) || review.ReviewedAt == default || review.AcknowledgedWarningCodes.IsDefault)
            throw new ArgumentException("Review requires a named reviewer, date, note and warning acknowledgments.");
        if (review.CalculationSha256 != run.CalculationSha256) throw new ArgumentException("Review is bound to a different calculation.");
        if (IsPartial(run) && !review.AcceptPartial) throw new ArgumentException("Explicit partial-scope acceptance is required.");
        if (review.Status != (IsPartial(run) ? "AcceptedPartial" : "Accepted")) throw new ArgumentException("Review completeness label is inconsistent.");
        if (WarningCodes(run).Except(review.AcknowledgedWarningCodes, StringComparer.Ordinal).Any())
            throw new ArgumentException("Acknowledge all current warning codes before acceptance; acknowledgment does not resolve missing quantities.");
    }

    private static TakeoffRun Seal(TakeoffRun run) => run with { PackageSha256 = Hash(run with { PackageSha256 = "" }) };

    public static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(path); var directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, ".quentra-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temp, Serialize(value), new UTF8Encoding(false), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); File.Move(temp, full, false);
        }
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
