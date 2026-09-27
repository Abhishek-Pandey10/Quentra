using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Quentra.Application;
using Quentra.Core;
using Quentra.Etabs;
using Quentra.Infrastructure;

namespace Quentra.Gui;

// Action is set, exclude or include; field and value/unit are used by set only.
public sealed record OverrideRequest(string RunId, string ObjectId, string Action, string? Field, double? Value, string? Unit, string Reason, string Author);
public sealed record AcceptRequest(string RunId, string Reviewer, string Note, bool Partial, string[] Acknowledge);
public sealed record RunIdRequest(string RunId);
public sealed record SavedRunRequest(string Name);
public sealed record EtabsExtractRequest(int? Pid, bool AllowUntested, string? SteelApprovedBy, string? PolicyApprovedBy, double? MaxStationGapM);
public sealed record EtabsCheckRequest(string RunId, int? Pid, bool AllowUntested);

// Held lists every run the GUI still holds; Dropped names runs just released from memory that were never downloaded.
// SavedPath is where an accepted run was written on disk.
public sealed record RunResponse(string RunId, string FileName, RunView View, HeldRun[] Held, string[] Dropped, string? SavedPath = null);
public sealed record HeldRun(string RunId, string FileName, string Label, string ReviewStatus, bool Saved, bool Pinned);
public sealed record RunView(string ModelId, string Origin, string ReviewStatus, string StatusBanner, string SnapshotSha256, string CalculationSha256,
    bool PolicyApproved, bool Partial, string CompleteLabel, string[] Excluded, Totals Totals, string[] ConcreteFlagged, string[] SteelFlagged, bool DensityFlagged,
    WarningView[] Warnings, ElementView[] Elements, CoverageView[] Steel, OverrideView[] Overrides, ReviewView[] Reviews,
    AcknowledgementView[] Acknowledgements, SourceView? Source, SourceCheckView? SourceCheck, IdentityView Identity);
public sealed record Totals(string KnownGross, string CompleteGross, string KnownOpeningAdjusted, string CompleteOpeningAdjusted,
    string KnownSteel, string CompleteSteel, int MissingSteelComponents, int Found, int InScope, int Quantified, int Unsupported, int Invalid, int OutOfScope);
public sealed record WarningView(string Code, string Subject, string Message);
public sealed record ElementView(string ObjectId, string Category, string Status, string Gross, string OpeningAdjusted, string Formula,
    bool Excluded, FieldView[] Fields, string Source);
public sealed record FieldView(string Field, string Label, string CurrentMm);
public sealed record CoverageView(string ObjectId, string Required, string Missing, string Known, string Complete);
public sealed record OverrideView(string Id, string ObjectId, string Change, string Reason, string Author, string RecordedAt);
public sealed record ReviewView(string Status, string Reviewer, string ReviewedAt, string Note, bool Current, string Account, string Computer, string Version);
public sealed record AcknowledgementView(string Token, string Meaning);
public sealed record SourceView(string Program, string Build, bool Tested, string ModelPath, string ExtractedAt, string Units, bool Locked);
public sealed record SourceCheckView(bool Matches, string CheckedAt, string Message);
// What an acceptance records about who made it: shown before accepting, so the reviewer knows it is recorded, not verified.
public sealed record IdentityView(string Account, string Computer, string QuentraVersion);
public sealed record SavedRunView(string Name, string Modified, long Bytes);
public sealed record ExtractionView(ExtractionCounts Counts, string[] Ok, HealthWarning[] Warnings, string[] Errors, string Status, string SavedTo, string Seconds);
public sealed record ExtractResponse(RunResponse Run, ExtractionView Extraction);

// The GUI's operations over the same run, override, review and export code as the CLI.
// Runs are held here, not in the page: a large run is hundreds of MB, more than a browser or a single JSON
// string can carry. A run file opened from disk is replayed once; runs this session produced are trusted.
// With a data folder, every accepted run is also written to <data>/runs as soon as it is accepted, and every ETABS
// extraction to <data>/extractions, so nothing reviewed exists only in memory.
public sealed partial class GuiSession(string? dataDirectory = null, IEtabsGateway? etabs = null)
{
    // Recent runs, so the page can still download or export earlier ones after further changes. A large run is
    // hundreds of MB, so only a few are kept; an accepted run is kept until it is downloaded or saved (up to MaxHeld in all),
    // because an acceptance is the result of a review and must not disappear because the user kept working.
    public const int Capacity = 4;
    public const int MaxHeld = 12;
    private readonly Lock gate = new();
    private readonly List<Held> runs = [];
    private int sequence;

    public string? RunsDirectory => dataDirectory is null ? null : Path.Combine(dataDirectory, "runs");
    public string? ExtractionsDirectory => dataDirectory is null ? null : Path.Combine(dataDirectory, "extractions");

    private sealed class Held(string id, int number, TakeoffRun run, bool saved)
    {
        public string Id { get; } = id;
        public int Number { get; } = number;
        public TakeoffRun Run { get; } = run;
        public bool Saved { get; set; } = saved;
        public SourceCheckView? SourceCheck { get; set; }
        public bool Pinned => !Saved && TakeoffJson.ReviewStatus(Run) != "Draft";
        public string Label => $"#{Number} {TakeoffJson.ReviewStatus(Run)}, {Run.Overrides.Length} override{(Run.Overrides.Length == 1 ? "" : "s")}" +
            (SourceCheck is { Matches: false } ? ", ETABS model changed" : "");
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
        return new(held.Id, FileStem(held.Run) + "-run.json", View(held.Run, held.SourceCheck), HeldRuns(), []);
    }

    public HeldRun[] HeldRuns()
    {
        lock (gate)
            return [.. runs.Select(x => new HeldRun(x.Id, FileStem(x.Run) + "-run.json", x.Label, TakeoffJson.ReviewStatus(x.Run), x.Saved, x.Pinned))];
    }

    public RunResponse Override(OverrideRequest request, CancellationToken token = default)
    {
        var current = GetHeld(request.RunId);
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
        var change = Overrides.Prepare(current.Run.Snapshot, current.Run.SnapshotSha256, current.Run.Overrides, request.ObjectId, field, value,
            request.Reason.Trim(), request.Author.Trim(), DateTimeOffset.UtcNow);
        token.ThrowIfCancellationRequested();
        // The new run has the same snapshot, so what is known about its ETABS model carries over.
        return Store(TakeoffJson.AddOverrides(current.Run, [change]), sourceCheck: current.SourceCheck);
    }

    public RunResponse Accept(AcceptRequest request)
    {
        var held = GetHeld(request.RunId);
        RequireCurrentSource(held, "accepted");
        var accepted = TakeoffJson.Accept(held.Run, (request.Reviewer ?? "").Trim(), (request.Note ?? "").Trim(), request.Partial,
            (request.Acknowledge ?? []).Distinct(StringComparer.Ordinal).ToImmutableArray(), DateTimeOffset.UtcNow, ReviewerContext.Current());
        // Written to disk at once, so an acceptance never exists only in this process's memory.
        var path = SaveAccepted(accepted);
        return Store(accepted, saved: path is not null, sourceCheck: held.SourceCheck) with { SavedPath = path };
    }

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
        var held = GetHeld(runId);
        RequireCurrentSource(held, "exported");
        var run = held.Run;
        var root = Path.Combine(Path.GetTempPath(), "quentra-gui-" + Guid.NewGuid().ToString("N"));
        try
        {
            var directory = Path.Combine(root, "report");
            await ReportExporter.ExportAsync(run, directory, token);
            using var zip = new MemoryStream();
            await ZipFile.CreateFromDirectoryAsync(directory, zip, CompressionLevel.Optimal, includeBaseDirectory: false, token);
            return (zip.ToArray(), $"{FileStem(run)}-report-{StatusMarker(run)}.zip");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    public static string StatusMarker(TakeoffRun run) => TakeoffJson.ReviewStatus(run) switch
    {
        "Accepted" => "ACCEPTED", "AcceptedPartial" => "ACCEPTED-PARTIAL", _ => "DRAFT"
    };

    // ---------- saved runs ----------
    private string? SaveAccepted(TakeoffRun run)
    {
        if (RunsDirectory is not { } dir) return null;
        Directory.CreateDirectory(dir);
        var stamp = run.ReviewHistory[^1].ReviewedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(dir, $"{FileStem(run)}-{StatusMarker(run)}-{stamp}-{run.CalculationSha256[..8]}.run.json");
        for (var n = 2; File.Exists(path); n++) path = Path.Combine(dir, $"{FileStem(run)}-{StatusMarker(run)}-{stamp}-{run.CalculationSha256[..8]}-{n}.run.json");
        TakeoffJson.WriteAsync(path, run).GetAwaiter().GetResult();
        return path;
    }

    public SavedRunView[] SavedRuns()
    {
        if (RunsDirectory is not { } dir || !Directory.Exists(dir)) return [];
        return [.. new DirectoryInfo(dir).GetFiles("*.run.json").OrderByDescending(x => x.LastWriteTimeUtc)
            .Select(x => new SavedRunView(x.Name, ReportExporter.Time(new DateTimeOffset(x.LastWriteTimeUtc, TimeSpan.Zero)), x.Length))];
    }

    public RunResponse OpenSaved(string name, CancellationToken token = default)
    {
        if (RunsDirectory is not { } dir) throw new ArgumentException("This session has no data folder, so it keeps no saved runs.");
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || !name.EndsWith(".run.json", StringComparison.Ordinal))
            throw new ArgumentException("Choose a saved run from the list.");
        var path = Path.Combine(dir, name);
        if (!File.Exists(path)) throw new ArgumentException($"{name} is no longer in {dir}.");
        return Open(SnapshotJson.ReadAsync(path, SnapshotJson.MaximumRunBytes, token).GetAwaiter().GetResult(), token);
    }

    // ---------- ETABS ----------
    private IEtabsGateway Etabs => etabs ?? throw new ArgumentException("ETABS is not available in this session.");

    public EtabsStatusView EtabsStatus(int? pid) => Etabs.Status(pid);

    // Extracts the open model, saves the capture and snapshot, checks model health and calculates. A snapshot with
    // blocking validation errors is saved for inspection but not calculated.
    public ExtractResponse EtabsExtract(EtabsExtractRequest request, CancellationToken token = default)
    {
        var clock = Stopwatch.StartNew();
        EtabsRawModel raw;
        try { raw = Etabs.Extract(request.Pid, request.AllowUntested, token); }
        catch (EtabsUnavailableException e) { throw new ArgumentException(e.Message, e); }
        var options = new EtabsBuildOptions
        {
            SteelApprovedBy = Blank(request.SteelApprovedBy), PolicyApprovedBy = Blank(request.PolicyApprovedBy),
            MaximumStationGapM = request.MaxStationGapM is > 0 and <= 10 ? request.MaxStationGapM.Value : EtabsBuildOptions.DefaultMaximumStationGapM
        };
        var built = EtabsSnapshotBuilder.Run(raw, options);
        var health = ModelHealth.Check(built.Snapshot, token);
        var savedTo = SaveExtraction(raw, built.Snapshot, health.Blocked);
        if (health.Blocked)
            throw new ArgumentException($"The extracted snapshot has {health.Problems.Length} blocking error(s), so it was not calculated:\n  - " +
                string.Join("\n  - ", health.Problems.Take(20)) + (savedTo is null ? "" : $"\nThe capture and snapshot were saved in {savedTo}."));
        var run = TakeoffJson.Calculate(built.Snapshot, cancellationToken: token);
        var s = health.Summary!;
        string[] ok =
        [
            $"Connected to ETABS {raw.Model.ProgramVersion} (build {raw.Model.ProgramBuild}); API {raw.Model.ApiAssemblyVersion}",
            $"Units identified: {raw.Model.PresentUnits}, converted to metres",
            $"{s.InScope:N0} in-scope elements; {s.Quantified:N0} can be quantified",
            "Story references and snapshot structure valid",
            $"Concrete materials resolved: {string.Join(", ", built.Counts.ConcreteMaterialNames)}"
        ];
        string[] errors = [.. health.InvalidElements.Select(x => $"{x}: invalid geometry, concrete unknown")];
        var view = new ExtractionView(built.Counts, ok, [.. health.Warnings], errors, health.Status, savedTo ?? "not saved (no data folder)",
            clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture));
        return new(Store(run), view);
    }

    // Re-reads the model open in ETABS and records whether it still matches the run's snapshot. A run whose model has
    // changed is marked stale: it can no longer be accepted or exported.
    public RunResponse EtabsCheck(EtabsCheckRequest request, CancellationToken token = default)
    {
        var held = GetHeld(request.RunId);
        if (held.Run.Snapshot.Source is not { } source) throw new ArgumentException("This run was not extracted from ETABS, so there is no ETABS model to check it against.");
        EtabsRawModel raw;
        try { raw = Etabs.Extract(request.Pid, request.AllowUntested, token); }
        catch (EtabsUnavailableException e) { throw new ArgumentException(e.Message, e); }
        if (!string.Equals(raw.Model.ModelPath, source.ModelPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"ETABS has {raw.Model.ModelPath} open, not {source.ModelPath}. Open the model this run came from and check again.");
        var comparison = SnapshotFingerprint.Compare(held.Run.Snapshot, EtabsSnapshotBuilder.Run(raw, EtabsBuildOptions.RecoveredFrom(held.Run.Snapshot)).Snapshot);
        var check = new SourceCheckView(comparison.Matches, ReportExporter.Time(raw.ExtractedAt), comparison.Describe());
        lock (gate)
            foreach (var h in runs.Where(x => x.Run.SnapshotSha256 == held.Run.SnapshotSha256)) h.SourceCheck = check;
        return Show(held.Id);
    }

    private static void RequireCurrentSource(Held held, string action)
    {
        if (held.SourceCheck is { Matches: false } check)
            throw new ArgumentException($"This run cannot be {action}: {check.Message} Extract the model again and review the new run.");
    }

    private string? SaveExtraction(EtabsRawModel raw, TakeoffSnapshot snapshot, bool blocked)
    {
        if (ExtractionsDirectory is not { } root) return null;
        var stamp = raw.ExtractedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var name = SafeName().Replace(EtabsSnapshotBuilder.ModelId(raw.Model.ModelPath), "-").Trim('-') is { Length: > 0 } s ? s : "etabs-model";
        var dir = Path.Combine(root, $"{name}-{stamp}");
        for (var n = 2; Directory.Exists(dir); n++) dir = Path.Combine(root, $"{name}-{stamp}-{n}");
        Directory.CreateDirectory(dir);
        TakeoffJson.WriteAsync(Path.Combine(dir, name + ".etabs-raw.json"), raw).GetAwaiter().GetResult();
        TakeoffJson.WriteAsync(Path.Combine(dir, name + ".snapshot.json"), blocked ? snapshot : TakeoffJson.Canonical(snapshot)).GetAwaiter().GetResult();
        return dir;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ---------- holding runs ----------
    private RunResponse Store(TakeoffRun run, bool saved = false, SourceCheckView? sourceCheck = null)
    {
        var dropped = new List<string>();
        Held added;
        lock (gate)
        {
            added = new(Guid.NewGuid().ToString("N"), ++sequence, run, saved) { SourceCheck = sourceCheck };
            runs.Add(added);
            while (runs.Count > Capacity)
            {
                // Oldest first; accepted runs not yet downloaded or saved only once MaxHeld is reached.
                var victim = runs.FirstOrDefault(x => x != added && !x.Pinned) ?? (runs.Count > MaxHeld ? runs.First(x => x != added) : null);
                if (victim is null) break;
                runs.Remove(victim);
                if (!victim.Saved) dropped.Add($"{FileStem(victim.Run)} {victim.Label}");
            }
        }
        return new(added.Id, FileStem(run) + "-run.json", View(run, sourceCheck), HeldRuns(), [.. dropped]);
    }

    private Held GetHeld(string runId)
    {
        lock (gate) return Find(runId);
    }

    private Held Find(string runId) => runs.FirstOrDefault(x => x.Id == runId)
        ?? throw new ArgumentException($"This run is no longer held by the GUI (it was restarted, or more than {Capacity} newer runs were made). Open the run file you downloaded" +
            (RunsDirectory is null ? "." : ", or a saved run from the list."));

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

    public static RunView View(TakeoffRun run, SourceCheckView? sourceCheck = null)
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
        var source = run.Snapshot.Source is { } src
            ? new SourceView(src.Program + " " + src.ProgramVersion, src.ProgramBuild, EtabsDiscovery.Classify(src.ProgramBuild) == EtabsSupport.TestedSupported,
                src.ModelPath, ReportExporter.Time(src.ExtractedAt), src.PresentUnits, src.ModelLocked)
            : null;
        var me = ReviewerContext.Current();
        return new RunView(run.Snapshot.Model.ModelId, run.Snapshot.Model.Origin.ToString(), TakeoffJson.ReviewStatus(run), TakeoffJson.StatusBanner(run),
            run.SnapshotSha256, run.CalculationSha256, policy.ApprovedAt is not null && !string.IsNullOrWhiteSpace(policy.ApprovedBy),
            TakeoffJson.IsPartial(run), TakeoffJson.CompleteLabel(run), [.. TakeoffJson.Exclusions(run)],
            new Totals(M3(s.KnownGrossM3), M3(s.CompleteGrossM3), M3(s.KnownOpeningAdjustedM3), M3(s.CompleteOpeningAdjustedM3),
                Kg(s.KnownSteelKg), Kg(s.CompleteSteelKg), s.MissingSteelComponents, s.Found, s.InScope, s.Quantified, s.Unsupported, s.Invalid, s.OutOfScope),
            [.. TakeoffJson.FlaggedElements(run, "IMPLAUSIBLE_DIMENSION")], [.. TakeoffJson.FlaggedElements(run, TakeoffJson.PerElementCodes)],
            run.Result.Warnings.Any(x => x.Code == "IMPLAUSIBLE_POLICY"),
            warnings,
            run.Result.Elements.Select(e => new ElementView(e.ObjectId, e.Category, e.Status.ToString(), M3(e.GrossM3), M3(e.OpeningAdjustedM3), e.Formula,
                excluded.Contains(e.ObjectId), Fields(frames.GetValueOrDefault(e.ObjectId), areas.GetValueOrDefault(e.ObjectId)), e.SourceReference)).ToArray(),
            run.Result.SteelCoverage.Select(c => new CoverageView(c.ObjectId, c.Required.IsEmpty ? "None (declared)" : string.Join(", ", c.Required), string.Join(", ", c.Missing),
                Kg(c.KnownMassKg), Kg(c.CompleteMassKg))).ToArray(),
            run.Overrides.Select(o => new OverrideView(o.Id, o.ObjectId, o.Field == OverrideField.Excluded
                    ? (o.ReplacementValue == 1 ? "excluded" : "included again")
                    : $"{Overrides.Label(o.Field)} {Overrides.Mm(o.OriginalValue)} → {Overrides.Mm(o.ReplacementValue)}",
                o.Reason, o.Author, ReportExporter.Time(o.RecordedAt))).ToArray(),
            run.ReviewHistory.Select(r => new ReviewView(r.Status, r.Reviewer, ReportExporter.Time(r.ReviewedAt), r.Note,
                r.CalculationSha256 == run.CalculationSha256, r.RecordedByAccount ?? "not recorded", r.RecordedOnComputer ?? "not recorded", r.RecordedWithVersion ?? "not recorded")).ToArray(),
            Acknowledgements(run, warnings), source, sourceCheck, new IdentityView(me.Account, me.Computer, me.QuentraVersion));
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
