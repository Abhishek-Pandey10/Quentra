using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Quentra.Application;
using Quentra.Core;
using Quentra.Infrastructure;

namespace Quentra.Gui;

// Action is set, exclude or include; field and value/unit are used by set only.
public sealed record OverrideRequest(string RunId, string ObjectId, string Action, string? Field, double? Value, string? Unit, string Reason, string Author);
public sealed record AcceptRequest(string RunId, string Reviewer, string Note, bool Partial, string[] Acknowledge);
public sealed record RunIdRequest(string RunId);

// Held lists every run the GUI still holds; Dropped names runs just released from memory that were never downloaded.
public sealed record RunResponse(string RunId, string FileName, RunView View, HeldRun[] Held, string[] Dropped);
public sealed record HeldRun(string RunId, string FileName, string Label, string ReviewStatus, bool Saved, bool Pinned);
public sealed record RunView(string ModelId, string Origin, string ReviewStatus, string SnapshotSha256, string CalculationSha256,
    bool PolicyApproved, bool Partial, string CompleteLabel, string[] Excluded, Totals Totals, string[] ConcreteFlagged, string[] SteelFlagged, bool DensityFlagged,
    WarningView[] Warnings, ElementView[] Elements, CoverageView[] Steel, OverrideView[] Overrides, ReviewView[] Reviews,
    AcknowledgementView[] Acknowledgements);
public sealed record Totals(string KnownGross, string CompleteGross, string KnownOpeningAdjusted, string CompleteOpeningAdjusted,
    string KnownSteel, string CompleteSteel, int MissingSteelComponents, int Found, int InScope, int Quantified, int Unsupported, int Invalid, int OutOfScope);
public sealed record WarningView(string Code, string Subject, string Message);
public sealed record ElementView(string ObjectId, string Category, string Status, string Gross, string OpeningAdjusted, string Formula,
    bool Excluded, FieldView[] Fields);
public sealed record FieldView(string Field, string Label, string CurrentMm);
public sealed record CoverageView(string ObjectId, string Required, string Missing, string Known, string Complete);
public sealed record OverrideView(string Id, string ObjectId, string Change, string Reason, string Author, string RecordedAt);
public sealed record ReviewView(string Status, string Reviewer, string ReviewedAt, string Note, bool Current);
public sealed record AcknowledgementView(string Token, string Meaning);

// The GUI's operations over the same run, override, review and export code as the CLI.
// Runs are held here, not in the page: a large run is hundreds of MB, more than a browser or a single JSON
// string can carry. A run file opened from disk is replayed once; runs this session produced are trusted.
public sealed partial class GuiSession
{
    // Recent runs, so the page can still download or export earlier ones after further changes. A large run is
    // hundreds of MB, so only a few are kept; an accepted run is kept until it is downloaded (up to MaxHeld in all),
    // because an acceptance is the result of a review and must not disappear because the user kept working.
    public const int Capacity = 4;
    public const int MaxHeld = 12;
    private readonly Lock gate = new();
    private readonly List<Held> runs = [];
    private int sequence;

    private sealed class Held(string id, int number, TakeoffRun run, bool saved)
    {
        public string Id { get; } = id;
        public int Number { get; } = number;
        public TakeoffRun Run { get; } = run;
        public bool Saved { get; set; } = saved;
        public bool Pinned => !Saved && TakeoffJson.ReviewStatus(Run) != "Draft";
        public string Label => $"#{Number} {TakeoffJson.ReviewStatus(Run)}, {Run.Overrides.Length} override{(Run.Overrides.Length == 1 ? "" : "s")}";
    }

    public static string Template()
    {
        using var stream = typeof(GuiSession).Assembly.GetManifestResourceStream("Quentra.Templates.snapshot.json")!;
        return new StreamReader(stream).ReadToEnd().ReplaceLineEndings("\n");
    }

    public RunResponse Calculate(string snapshot, CancellationToken token = default)
    {
        Limit(snapshot, SnapshotJson.MaximumSnapshotBytes, "The snapshot");
        return Store(Read("Snapshot", () =>
        {
            if (IsRun(snapshot)) throw new ArgumentException("This is a saved run, not a snapshot. Use Open run instead.");
            RequireSchema(snapshot, "snapshot");
            return TakeoffJson.Calculate(TakeoffJson.Parse<TakeoffSnapshot>(snapshot), cancellationToken: token);
        }));
    }

    public RunResponse Open(string run, CancellationToken token = default)
    {
        Limit(run, SnapshotJson.MaximumRunBytes, "The run file");
        return Store(Read("Run file", () =>
        {
            if (!IsRun(run)) throw new ArgumentException("This is a snapshot, not a saved run. Use Open snapshot instead.");
            RequireSchema(run, "run");
            return TakeoffJson.Replay(run, token);
        }), saved: true);
    }

    // Shows a run the GUI still holds, for example an accepted run made before later overrides.
    public RunResponse Show(string runId)
    {
        Held held;
        lock (gate) held = Find(runId);
        return new(held.Id, FileStem(held.Run) + "-run.json", View(held.Run), HeldRuns(), []);
    }

    public HeldRun[] HeldRuns()
    {
        lock (gate)
            return [.. runs.Select(x => new HeldRun(x.Id, FileStem(x.Run) + "-run.json", x.Label, TakeoffJson.ReviewStatus(x.Run), x.Saved, x.Pinned))];
    }

    public RunResponse Override(OverrideRequest request, CancellationToken token = default)
    {
        var current = Get(request.RunId);
        if (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.Author))
            throw new ArgumentException("An override needs a reason and an author.");
        var (field, value) = request.Action switch
        {
            "exclude" => (OverrideField.Excluded, 1.0),
            "include" => (OverrideField.Excluded, 0.0),
            "set" => (Enum.TryParse<OverrideField>(request.Field, out var f) && f != OverrideField.Excluded
                    ? f : throw new ArgumentException("Choose the dimension to change."),
                request.Value is { } v && double.IsFinite(v) ? v * Units.MetresPerUnit(Unit(request.Unit)) : throw new ArgumentException("Enter the new value as a number.")),
            _ => throw new ArgumentException("Choose set, exclude or include.")
        };
        var change = Overrides.Prepare(current.Snapshot, current.SnapshotSha256, current.Overrides, request.ObjectId, field, value,
            request.Reason.Trim(), request.Author.Trim(), DateTimeOffset.UtcNow);
        token.ThrowIfCancellationRequested();
        return Store(TakeoffJson.AddOverrides(current, [change]));
    }

    public RunResponse Accept(AcceptRequest request) =>
        Store(TakeoffJson.Accept(Get(request.RunId), (request.Reviewer ?? "").Trim(), (request.Note ?? "").Trim(), request.Partial,
            (request.Acknowledge ?? []).Distinct(StringComparer.Ordinal).ToImmutableArray(), DateTimeOffset.UtcNow));

    public (byte[] Json, string FileName) Download(string runId)
    {
        Held held;
        lock (gate) held = Find(runId);
        var json = new UTF8Encoding(false).GetBytes(TakeoffJson.Serialize(held.Run));
        lock (gate) held.Saved = true;
        return (json, FileStem(held.Run) + "-run.json");
    }

    public async Task<(byte[] Zip, string FileName)> Export(string runId, CancellationToken token = default)
    {
        var run = Get(runId);
        var root = Path.Combine(Path.GetTempPath(), "quentra-gui-" + Guid.NewGuid().ToString("N"));
        try
        {
            var directory = Path.Combine(root, "report");
            await ReportExporter.ExportAsync(run, directory, token);
            using var zip = new MemoryStream();
            await ZipFile.CreateFromDirectoryAsync(directory, zip, CompressionLevel.Optimal, includeBaseDirectory: false, token);
            return (zip.ToArray(), FileStem(run) + "-report.zip");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private RunResponse Store(TakeoffRun run, bool saved = false)
    {
        var dropped = new List<string>();
        Held added;
        lock (gate)
        {
            added = new(Guid.NewGuid().ToString("N"), ++sequence, run, saved);
            runs.Add(added);
            while (runs.Count > Capacity)
            {
                // Oldest first; accepted runs not yet downloaded only once MaxHeld is reached.
                var victim = runs.FirstOrDefault(x => x != added && !x.Pinned) ?? (runs.Count > MaxHeld ? runs.First(x => x != added) : null);
                if (victim is null) break;
                runs.Remove(victim);
                if (!victim.Saved) dropped.Add($"{FileStem(victim.Run)} {victim.Label}");
            }
        }
        return new(added.Id, FileStem(run) + "-run.json", View(run), HeldRuns(), [.. dropped]);
    }

    private TakeoffRun Get(string runId)
    {
        lock (gate) return Find(runId).Run;
    }

    private Held Find(string runId) => runs.FirstOrDefault(x => x.Id == runId)
        ?? throw new ArgumentException($"This run is no longer held by the GUI (it was restarted, or more than {Capacity} newer runs were made). Open the run file you downloaded.");

    // JSON errors are restated in terms of the document, exactly as the CLI prints them.
    private static T Read<T>(string what, Func<T> read)
    {
        try { return read(); }
        catch (JsonException e) { throw new ArgumentException($"{what}: {JsonErrors.Describe(e)}", e); }
    }

    private static void RequireSchema(string json, string kind)
    {
        int version;
        try { version = TakeoffJson.SchemaVersion(json); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new ArgumentException($"The {kind} needs an integer top-level schemaVersion.");
        }
        if (version == 1) throw new ArgumentException($"This is a schema 1 (frames-only) {kind}; use the command-line tool for it. The GUI works with schema 2 takeoffs.");
        if (version != 2) throw new ArgumentException($"Unsupported {kind} schemaVersion {version}.");
    }

    private static bool IsRun(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("calculationSha256", out _);
    }

    private static void Limit(string text, long maximumBytes, string what)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException($"{what} is empty.");
        if (Encoding.UTF8.GetByteCount(text) > maximumBytes)
            throw new ArgumentException($"{what} is larger than the {maximumBytes / (1024 * 1024)} MiB limit.");
    }

    private static LengthUnit Unit(string? unit) => unit switch
    {
        "mm" => LengthUnit.Millimetre, "m" => LengthUnit.Metre, "in" => LengthUnit.Inch, "ft" => LengthUnit.Foot,
        _ => throw new ArgumentException("Choose a unit: mm, m, in or ft.")
    };

    private static string FileStem(TakeoffRun run) => SafeName().Replace(run.Snapshot.Model.ModelId, "-").Trim('-') is { Length: > 0 } stem ? stem : "quentra";

    [GeneratedRegex("[^A-Za-z0-9._-]+")]
    private static partial Regex SafeName();

    public static RunView View(TakeoffRun run)
    {
        var s = run.Result.Summary;
        var (effective, excluded, _) = Overrides.Apply(run.Snapshot, run.Overrides, run.SnapshotSha256);
        var frames = effective.Model.Frames.ToDictionary(x => x.ObjectId, StringComparer.Ordinal);
        var areas = effective.Areas.ToDictionary(x => x.ObjectId, StringComparer.Ordinal);
        var warnings = run.Result.Warnings.Select(w => new WarningView(w.Code, "Run", w.Message))
            .Concat(run.Result.Elements.SelectMany(e => e.Warnings.Select(w => new WarningView(w.Code, e.ObjectId, w.Message))))
            .Concat(run.Result.Steel.SelectMany(x => x.Warnings.Select(w => new WarningView(w.Code, $"{x.ObjectId}/{x.Component}", w.Message))))
            .ToArray();
        var policy = run.Snapshot.Policy;
        return new RunView(run.Snapshot.Model.ModelId, run.Snapshot.Model.Origin.ToString(), TakeoffJson.ReviewStatus(run),
            run.SnapshotSha256, run.CalculationSha256, policy.ApprovedAt is not null && !string.IsNullOrWhiteSpace(policy.ApprovedBy),
            TakeoffJson.IsPartial(run), TakeoffJson.CompleteLabel(run), [.. TakeoffJson.Exclusions(run)],
            new Totals(M3(s.KnownGrossM3), M3(s.CompleteGrossM3), M3(s.KnownOpeningAdjustedM3), M3(s.CompleteOpeningAdjustedM3),
                Kg(s.KnownSteelKg), Kg(s.CompleteSteelKg), s.MissingSteelComponents, s.Found, s.InScope, s.Quantified, s.Unsupported, s.Invalid, s.OutOfScope),
            [.. TakeoffJson.FlaggedElements(run, "IMPLAUSIBLE_DIMENSION")], [.. TakeoffJson.FlaggedElements(run, TakeoffJson.PerElementCodes)],
            run.Result.Warnings.Any(x => x.Code == "IMPLAUSIBLE_POLICY"),
            warnings,
            run.Result.Elements.Select(e => new ElementView(e.ObjectId, e.Category, e.Status.ToString(), M3(e.GrossM3), M3(e.OpeningAdjustedM3), e.Formula,
                excluded.Contains(e.ObjectId), Fields(frames.GetValueOrDefault(e.ObjectId), areas.GetValueOrDefault(e.ObjectId)))).ToArray(),
            run.Result.SteelCoverage.Select(c => new CoverageView(c.ObjectId, c.Required.IsEmpty ? "None (declared)" : string.Join(", ", c.Required), string.Join(", ", c.Missing),
                Kg(c.KnownMassKg), Kg(c.CompleteMassKg))).ToArray(),
            run.Overrides.Select(o => new OverrideView(o.Id, o.ObjectId, o.Field == OverrideField.Excluded
                    ? (o.ReplacementValue == 1 ? "excluded" : "included again")
                    : $"{Overrides.Label(o.Field)} {Overrides.Mm(o.OriginalValue)} → {Overrides.Mm(o.ReplacementValue)}",
                o.Reason, o.Author, ReportExporter.Time(o.RecordedAt))).ToArray(),
            run.ReviewHistory.Select(r => new ReviewView(r.Status, r.Reviewer, ReportExporter.Time(r.ReviewedAt), r.Note,
                r.CalculationSha256 == run.CalculationSha256)).ToArray(),
            Acknowledgements(run, warnings));
    }

    // What the reviewer is agreeing to: the element's own message for a per-element token, else the catalogue entry.
    private static AcknowledgementView[] Acknowledgements(TakeoffRun run, WarningView[] warnings)
    {
        var byCode = warnings.GroupBy(w => w.Code).ToDictionary(g => g.Key, g => (Count: g.Count(), First: g.First().Message));
        var perElement = warnings.Where(w => TakeoffJson.PerElementCodes.Contains(w.Code))
            .GroupBy(w => w.Code + ":" + w.Subject).ToDictionary(g => g.Key, g => g.First().Message);
        return TakeoffJson.RequiredAcknowledgements(run).Select(token =>
        {
            if (token.Contains(':')) return new AcknowledgementView(token, perElement.GetValueOrDefault(token, ""));
            var (count, first) = byCode[token];
            var catalogue = WarningCatalogue.Entries.FirstOrDefault(x => x.Code == token).Meaning;
            return new AcknowledgementView(token, (catalogue ?? first) + (count > 1 ? $" ({count} occurrences)" : ""));
        }).ToArray();
    }

    private static FieldView[] Fields(FrameSnapshot? frame, AreaSnapshot? area)
    {
        var fields = new List<FieldView>();
        void Add(OverrideField field, SourceLength? current)
        {
            if (current is not null) fields.Add(new(field.ToString(), Overrides.Label(field), Overrides.Mm(current.Metres)));
        }
        Add(OverrideField.FrameWidthM, frame?.Section?.Width);
        Add(OverrideField.FrameDepthM, frame?.Section?.Depth);
        Add(OverrideField.FrameDiameterM, frame?.Section?.Diameter);
        Add(OverrideField.AreaThicknessM, area?.Thickness);
        return [.. fields];
    }

    // Same rounding as the export and the CLI.
    private static string M3(double? value) => value is { } x ? new Rounded(x, 3) + " m³" : "unknown";
    private static string Kg(double? value) => value is { } x ? new Rounded(x, 1) + " kg" : "unknown";
}
