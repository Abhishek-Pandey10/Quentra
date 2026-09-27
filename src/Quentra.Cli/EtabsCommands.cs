using System.Globalization;
using System.Text;
using System.Text.Json;
using Quentra.Application;
using Quentra.Core;
using Quentra.Etabs;
using Quentra.Etabs.Api;
using Quentra.Infrastructure;

// quentra etabs status | extract | inspect | build. Exit codes as for the other commands: 0 success,
// 1 ETABS/input/validation failure, 2 usage error, 130 cancelled.
static class EtabsCommands
{
    public const string Usage = """
  quentra etabs status  [--pid <id>]
  quentra etabs extract <new-snapshot.json> [--pid <id>] [--allow-untested-version]
                        [--steel-approved-by <name>] [--policy-approved-by <name>] [--max-station-gap <metres>]
  quentra etabs inspect <object> [--pid <id>] [--raw <capture.etabs-raw.json>]
  quentra etabs build   <capture.etabs-raw.json> <new-snapshot.json> [--steel-approved-by <name>] [--policy-approved-by <name>]
  quentra etabs check   <run.json | snapshot.json> [--pid <id>] [--allow-untested-version]
""";

    public const string Details = """
Reads the model open in a running ETABS 22.7 and writes a Quentra snapshot. ETABS is only read: Quentra never
changes, saves, analyses or designs the model, and does not change its units.

  status    Lists installed and running ETABS versions, connects to the running ETABS 22.7 and shows its model,
            stories and units. Several ETABS 22.7 copies need --pid.
  extract   Writes <new-snapshot.json>, plus beside it <name>.etabs-raw.json (every value exactly as ETABS returned
            it, hashed into the snapshot) and <name>.etabs-log.txt (timings, counts, failed API calls). Then it
            runs the model health check. Calculate the snapshot with 'quentra calculate'.
  inspect   Shows everything Quentra read and derived for one object: raw API values, classification, the snapshot
            record and its calculated quantity. <object> is the Quentra ID (B12@L3), the ETABS unique name, or label@story.
  build     Re-creates a snapshot from a saved raw capture, without ETABS.
  check     Re-reads the model open in ETABS and says whether it still matches the run or snapshot, naming the elements
            that changed. Exit code 0 if it matches, 1 if it changed (the run's results are then out of date).

  --steel-approved-by   Name of the person approving ETABS reinforcement evidence (design demand equivalents and modeled
                        column bars) for this takeoff. Without it that evidence is recorded but not counted.
  --policy-approved-by  Name of the person approving the measurement policy. Without it the run cannot be accepted.
  --max-station-gap     Largest distance between ETABS design stations that is integrated (default 1.0 m).
  --allow-untested-version  Extract from an ETABS other than 22.7. The snapshot and reports say it is untested.

Only ETABS 22.7 is tested. Other versions are listed but never used unless you pass --pid and --allow-untested-version.
""";

    public static async Task<int> Run(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("status" or "extract" or "inspect" or "build" or "check"))
            return UsageError(args.Length == 0 ? "etabs needs a subcommand." : $"Unknown etabs subcommand '{args[0]}'.");
        var sub = args[0];
        var positional = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] allowed = sub switch
        {
            "status" => ["--pid"],
            "extract" => ["--pid", "--allow-untested-version", "--steel-approved-by", "--policy-approved-by", "--max-station-gap"],
            "inspect" => ["--pid", "--allow-untested-version", "--raw"],
            "check" => ["--pid", "--allow-untested-version"],
            _ => ["--steel-approved-by", "--policy-approved-by", "--max-station-gap"]
        };
        for (var i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) { positional.Add(args[i]); continue; }
            if (!allowed.Contains(args[i])) return UsageError($"Unknown option '{args[i]}' for 'etabs {sub}'.");
            if (args[i] == "--allow-untested-version") { options[args[i]] = ""; continue; }
            if (i + 1 >= args.Length) return UsageError($"{args[i]} needs a value.");
            if (!options.TryAdd(args[i], args[++i])) return UsageError($"{args[i - 1]} is given twice.");
        }
        int expected = sub switch { "status" => 0, "build" => 2, _ => 1 };
        if (positional.Count != expected) return UsageError($"'etabs {sub}' takes {expected} argument{(expected == 1 ? "" : "s")}; {positional.Count} given.");
        int? pid = null;
        if (options.TryGetValue("--pid", out var p))
        {
            if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) return UsageError("--pid needs a process id number.");
            pid = parsed;
        }
        var buildOptions = new EtabsBuildOptions
        {
            SteelApprovedBy = options.GetValueOrDefault("--steel-approved-by"), PolicyApprovedBy = options.GetValueOrDefault("--policy-approved-by"),
            MaximumStationGapM = EtabsBuildOptions.DefaultMaximumStationGapM
        };
        if (options.TryGetValue("--max-station-gap", out var gap))
        {
            if (!double.TryParse(gap.TrimEnd('m'), NumberStyles.Float, CultureInfo.InvariantCulture, out var g) || !double.IsFinite(g) || g <= 0 || g > 10)
                return UsageError("--max-station-gap needs a length in metres between 0 and 10, e.g. 1.0.");
            buildOptions = buildOptions with { MaximumStationGapM = g };
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        try
        {
            return sub switch
            {
                "status" => Status(pid),
                "extract" => await Extract(positional[0], pid, options.ContainsKey("--allow-untested-version"), buildOptions, cancellation.Token),
                "inspect" => await Inspect(positional[0], pid, options.ContainsKey("--allow-untested-version"), options.GetValueOrDefault("--raw"), cancellation.Token),
                "check" => await Check(positional[0], pid, options.ContainsKey("--allow-untested-version"), cancellation.Token),
                _ => await Build(positional[0], positional[1], buildOptions, cancellation.Token)
            };
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled. Nothing further was written."); return 130; }
        catch (EtabsUnavailableException e) { Console.Error.WriteLine($"Quentra: {e.Message}"); return 1; }
        catch (JsonException e) { Console.Error.WriteLine($"Quentra: {JsonErrors.Describe(e)}"); return 1; }
        catch (UnauthorizedAccessException) { Console.Error.WriteLine("Quentra: permission denied. Check that you can write to the output folder."); return 1; }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"Quentra: {e.Message}");
            return 1;
        }
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"Quentra: {message}");
        Console.Error.WriteLine($"Usage:\n{Usage.TrimEnd()}\nRun 'quentra help etabs' for details.");
        return 2;
    }

    private static int Status(int? pid)
    {
        Console.WriteLine("ETABS installations");
        var installed = EtabsDiscovery.Installations();
        if (installed.Count == 0) Console.WriteLine("  none found under Program Files\\Computers and Structures");
        foreach (var x in installed)
            Console.WriteLine($"  {(x.Support == EtabsSupport.TestedSupported ? "*" : " ")} ETABS {x.Version} ({EtabsDiscovery.Describe(x.Support)}) {x.Directory}; API {x.ApiVersion ?? "missing"}");
        Console.WriteLine($"  Quentra target: ETABS {EtabsDiscovery.TestedVersionLabel}. Registered COM API: {LiveEtabs.RegisteredApi ?? "not registered"}");
        var running = EtabsDiscovery.RunningInstances();
        Console.WriteLine("Running ETABS");
        if (running.Count == 0) Console.WriteLine("  none");
        foreach (var x in running) Console.WriteLine($"  {EtabsDiscovery.Line(x)} ({EtabsDiscovery.Describe(x.Support)})");
        if (!LiveEtabs.ApiCompiled)
        {
            Console.WriteLine("This Quentra build has no ETABS API (it was built without ETABS 22.7 installed), so it cannot connect.");
            return 1;
        }
        var instance = EtabsDiscovery.Choose(running, pid, allowUntested: false);
        using var connection = LiveEtabs.Connect(instance);
        var info = connection.Reader.ReadModelInfo();
        var stories = connection.Reader.Stories.Read();
        Console.WriteLine();
        Console.WriteLine($"Connected to ETABS {info.ProgramVersion} (build {info.ProgramBuild}, process {info.ProcessId})");
        Console.WriteLine($"API:     {info.ApiAssembly} {info.ApiAssemblyVersion}");
        var saved = EtabsExtractor.IsSavedModel(info.ModelPath);
        Console.WriteLine($"Model:   {(saved ? info.ModelPath : "none saved (open or save a model in ETABS)")}");
        Console.WriteLine($"Stories: {stories.Stories.Length}{(info.Towers.Length > 1 ? $" (active tower of {info.Towers.Length})" : "")}");
        Console.WriteLine($"Units:   {info.PresentUnits} (present); {info.DatabaseUnits} (database)");
        Console.WriteLine($"Locked:  {(info.ModelLocked ? "yes (analysed; design results can be current)" : "no")}");
        Console.WriteLine($"Status:  {(saved ? "Ready for extraction" : "Open or save a model in ETABS first")}");
        return saved ? 0 : 1;
    }

    private static (string Raw, string Log) Companions(string snapshotPath)
    {
        var full = Path.GetFullPath(snapshotPath);
        var stem = Path.Combine(Path.GetDirectoryName(full)!, Path.GetFileNameWithoutExtension(full));
        return (stem + ".etabs-raw.json", stem + ".etabs-log.txt");
    }

    private static void RequireNew(params string[] paths)
    {
        foreach (var path in paths)
            if (File.Exists(path) || Directory.Exists(path))
                throw new IOException($"{path} already exists. Choose a new snapshot name; existing files are never overwritten.");
    }

    private static async Task<int> Extract(string output, int? pid, bool allowUntested, EtabsBuildOptions options, CancellationToken token)
    {
        var snapshotPath = Path.GetFullPath(output);
        var (rawPath, logPath) = Companions(snapshotPath);
        RequireNew(snapshotPath, rawPath, logPath);
        var instance = EtabsDiscovery.Choose(EtabsDiscovery.RunningInstances(), pid, allowUntested);
        Console.WriteLine($"Connecting to {EtabsDiscovery.Line(instance)}...");
        var started = DateTimeOffset.UtcNow;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        EtabsRawModel raw; IReadOnlyList<StageTiming> timings;
        using (var connection = LiveEtabs.Connect(instance))
        {
            (raw, timings) = EtabsExtractor.Extract(connection.Reader, started, token);
        }
        var extractMs = clock.ElapsedMilliseconds;
        var result = EtabsSnapshotBuilder.Run(raw, options);
        var buildMs = clock.ElapsedMilliseconds - extractMs;
        var health = ModelHealth.Check(result.Snapshot, token);
        var healthMs = clock.ElapsedMilliseconds - extractMs - buildMs;
        await TakeoffJson.WriteAsync(rawPath, raw, token);
        await TakeoffJson.WriteAsync(snapshotPath, health.Blocked ? result.Snapshot : TakeoffJson.Canonical(result.Snapshot), token);
        await File.WriteAllTextAsync(logPath, Log(raw, result, health, timings, extractMs, buildMs, healthMs, started, snapshotPath, rawPath), new UTF8Encoding(false), token);
        PrintSummary(raw, result, health, snapshotPath, rawPath, logPath);
        return health.Blocked ? 1 : 0;
    }

    private static async Task<int> Build(string rawInput, string output, EtabsBuildOptions options, CancellationToken token)
    {
        var snapshotPath = Path.GetFullPath(output);
        RequireNew(snapshotPath);
        var raw = EtabsRawJson.Parse(await SnapshotJson.ReadAsync(Path.GetFullPath(rawInput), SnapshotJson.MaximumSnapshotBytes * 4, token));
        var result = EtabsSnapshotBuilder.Run(raw, options);
        var health = ModelHealth.Check(result.Snapshot, token);
        await TakeoffJson.WriteAsync(snapshotPath, health.Blocked ? result.Snapshot : TakeoffJson.Canonical(result.Snapshot), token);
        PrintSummary(raw, result, health, snapshotPath, Path.GetFullPath(rawInput), null);
        return health.Blocked ? 1 : 0;
    }

    private static void PrintSummary(EtabsRawModel raw, EtabsBuildResult result, ModelHealth health, string snapshotPath, string rawPath, string? logPath)
    {
        var c = result.Counts;
        string N(int n) => n.ToString("N0", CultureInfo.InvariantCulture).PadLeft(8);
        Console.WriteLine();
        Console.WriteLine("ETABS extraction");
        Console.WriteLine($"Model:   {raw.Model.ModelPath}");
        Console.WriteLine($"ETABS:   {raw.Model.ProgramVersion} (build {raw.Model.ProgramBuild}); API {raw.Model.ApiAssemblyVersion} from {raw.Model.ApiAssembly}");
        Console.WriteLine($"Units:   {raw.Model.PresentUnits} converted to metres");
        Console.WriteLine();
        Console.WriteLine($"Beams          {N(c.Beams)}");
        Console.WriteLine($"Columns        {N(c.Columns)}");
        Console.WriteLine($"Other frames   {N(c.OtherFrames)}");
        Console.WriteLine($"Slabs          {N(c.Slabs)}");
        Console.WriteLine($"Walls          {N(c.Walls)}");
        Console.WriteLine($"Other areas    {N(c.OtherAreas)}");
        Console.WriteLine($"Openings       {N(c.Openings)}{(c.OpeningsWithoutHost > 0 ? $" ({c.OpeningsWithoutHost} without a host)" : "")}");
        Console.WriteLine($"Non-concrete   {N(c.NonConcrete)} (out of scope)");
        Console.WriteLine($"Stories        {N(c.Stories)}");
        Console.WriteLine($"Concrete materials: {c.ConcreteMaterials} ({string.Join(", ", c.ConcreteMaterialNames)})");
        Console.WriteLine($"Design results: {(raw.Design.ResultsAvailable ? $"available ({raw.Design.Code}); {raw.Design.Beams.Length} beams, {raw.Design.Columns.Length} columns" : "not available")}; steel inputs {c.SteelInputs}");
        Console.WriteLine($"Failed API calls: {c.ApiIssues}");
        PrintHealth(health);
        Console.WriteLine();
        Console.WriteLine($"Snapshot:    {snapshotPath}");
        Console.WriteLine($"Raw capture: {rawPath}");
        if (logPath is not null) Console.WriteLine($"Log:         {logPath}");
        Console.WriteLine(health.Blocked
            ? "The snapshot cannot be calculated until the errors above are fixed."
            : $"Next: quentra calculate \"{snapshotPath}\" <new-run.json>");
    }

    public static void PrintHealth(ModelHealth health)
    {
        Console.WriteLine();
        Console.WriteLine("MODEL HEALTH");
        if (health.Summary is { } s)
        {
            Console.WriteLine($"  OK    {s.InScope:N0} in-scope elements; {s.Quantified:N0} can be quantified");
            Console.WriteLine($"  OK    story references and snapshot structure valid");
        }
        foreach (var w in health.Warnings) Console.WriteLine($"  WARN  {w.Code} x{w.Count}: {Short(w.Example)}");
        foreach (var e in health.Problems.Take(50)) Console.WriteLine($"  ERROR {e}");
        if (health.InvalidElements.Length > 0) Console.WriteLine($"  ERROR invalid geometry, concrete unknown: {List(health.InvalidElements)}");
        if (health.UnsupportedElements.Length > 0) Console.WriteLine($"  WARN  unsupported, concrete unknown: {List(health.UnsupportedElements)}");
        Console.WriteLine($"Calculation status: {health.Status}");
    }

    private static string Short(string text) => text.Length <= 160 ? text : text[..157] + "...";
    private static string List(IReadOnlyList<string> ids) => ids.Count <= 10 ? string.Join(", ", ids) : string.Join(", ", ids.Take(10)) + $" and {ids.Count - 10} more";

    private static string Log(EtabsRawModel raw, EtabsBuildResult result, ModelHealth health, IReadOnlyList<StageTiming> timings, long extractMs, long buildMs, long healthMs,
        DateTimeOffset started, string snapshotPath, string rawPath)
    {
        var b = new StringBuilder();
        b.AppendLine($"Quentra ETABS extraction log");
        b.AppendLine($"Quentra adapter {raw.ExtractorVersion}; started {started:O}; finished {DateTimeOffset.UtcNow:O}");
        b.AppendLine($"ETABS {raw.Model.ProgramVersion} build {raw.Model.ProgramBuild} at {raw.Model.ProgramPath}, process {raw.Model.ProcessId}");
        b.AppendLine($"API assembly {raw.Model.ApiAssembly} version {raw.Model.ApiAssemblyVersion}; registered COM API {LiveEtabs.RegisteredApi ?? "none"}");
        b.AppendLine($"Model {raw.Model.ModelPath}; file SHA256 {raw.Model.ModelFileSha256 ?? "not available"}; saved {raw.Model.ModelFileModifiedAt:O}; locked {raw.Model.ModelLocked}");
        b.AppendLine($"Units present {raw.Model.PresentUnits}; database {raw.Model.DatabaseUnits}; towers {raw.Model.Towers.Length}");
        b.AppendLine($"Snapshot {snapshotPath}; raw capture {rawPath} (SHA256 {EtabsRawJson.Sha256(raw)})");
        b.AppendLine();
        b.AppendLine("Timings (ms)");
        foreach (var t in timings) b.AppendLine($"  {t.Stage,-22} {t.Milliseconds,8}");
        b.AppendLine($"  {"extraction total",-22} {extractMs,8}");
        b.AppendLine($"  {"normalisation",-22} {buildMs,8}");
        b.AppendLine($"  {"validation",-22} {healthMs,8}");
        b.AppendLine();
        var c = result.Counts;
        b.AppendLine($"Objects: {raw.Frames.Length} frames, {raw.Areas.Length} areas ({c.Openings} openings); {raw.Materials.Length} materials, {raw.FrameSections.Length} frame sections, {raw.AreaProperties.Length} area properties, {raw.Stories.Stories.Length} stories");
        b.AppendLine($"Classified: beams {c.Beams}, columns {c.Columns}, other frames {c.OtherFrames}, slabs {c.Slabs}, walls {c.Walls}, other areas {c.OtherAreas}, non-concrete {c.NonConcrete}");
        b.AppendLine($"Design results available {raw.Design.ResultsAvailable}; code {raw.Design.Code ?? "none"}; beam results {raw.Design.Beams.Length}; column results {raw.Design.Columns.Length}; steel inputs {c.SteelInputs}");
        b.AppendLine();
        b.AppendLine($"Failed API calls ({raw.Issues.Length})");
        foreach (var i in raw.Issues) b.AppendLine($"  {i.Operation} [{i.ObjectName}] {i.Method} -> {(i.ReturnCode is { } code ? code.ToString(CultureInfo.InvariantCulture) : i.Exception)}: {i.Consequence}");
        b.AppendLine();
        b.AppendLine("Source warnings");
        foreach (var w in result.Snapshot.SourceWarnings) b.AppendLine($"  {w.Code} [{w.ObjectId ?? "run"}] {w.Message}");
        b.AppendLine();
        b.AppendLine($"Health: {health.Status}");
        foreach (var e in health.Problems) b.AppendLine($"  ERROR {e}");
        foreach (var w in health.Warnings) b.AppendLine($"  {w.Code} x{w.Count}");
        b.AppendLine($"Unsupported elements: {string.Join(", ", health.UnsupportedElements)}");
        b.AppendLine($"Invalid elements: {string.Join(", ", health.InvalidElements)}");
        return b.ToString();
    }

    private static async Task<int> Inspect(string target, int? pid, bool allowUntested, string? rawInput, CancellationToken token)
    {
        EtabsRawModel raw;
        if (rawInput is not null)
            raw = EtabsRawJson.Parse(await SnapshotJson.ReadAsync(Path.GetFullPath(rawInput), SnapshotJson.MaximumSnapshotBytes * 4, token));
        else
        {
            var instance = EtabsDiscovery.Choose(EtabsDiscovery.RunningInstances(), pid, allowUntested);
            using var connection = LiveEtabs.Connect(instance);
            raw = EtabsExtractor.Extract(connection.Reader, DateTimeOffset.UtcNow, token).Raw;
        }
        var snapshot = EtabsSnapshotBuilder.Run(raw, new EtabsBuildOptions()).Snapshot;
        var frameName = raw.Frames.FirstOrDefault(f => Matches(target, "frame", f.Name, f.Label, f.Story, snapshot))?.Name;
        var areaName = frameName is null ? raw.Areas.FirstOrDefault(a => Matches(target, "area", a.Name, a.Label, a.Story, snapshot))?.Name : null;
        if (frameName is null && areaName is null)
        {
            Console.Error.WriteLine($"Quentra: no ETABS frame or area matches '{target}'. Give the Quentra ID (label@story), the ETABS unique name or frame:/area: plus the unique name.");
            return 1;
        }
        var options = TakeoffJson.Options(true);
        object rawRecord = frameName is not null ? raw.Frames.Single(x => x.Name == frameName) : raw.Areas.Single(x => x.Name == areaName);
        var frame = frameName is null ? null : raw.Frames.Single(x => x.Name == frameName);
        var area = areaName is null ? null : raw.Areas.Single(x => x.Name == areaName);
        var objectId = snapshot.Model.Frames.FirstOrDefault(x => x.SourceReference.StartsWith($"ETABS frame object '{frameName}'", StringComparison.Ordinal))?.ObjectId
            ?? snapshot.Areas.FirstOrDefault(x => x.SourceReference.StartsWith($"ETABS area object '{areaName}'", StringComparison.Ordinal))?.ObjectId;
        Console.WriteLine($"ETABS {(frame is not null ? "frame" : "area")} object '{frameName ?? areaName}' in {raw.Model.ModelPath}");
        Console.WriteLine($"Units: {raw.Model.PresentUnits} (raw values below are in these units)");
        Console.WriteLine("\nRaw API values");
        Console.WriteLine(JsonSerializer.Serialize(rawRecord, rawRecord.GetType(), options));
        if (frame is not null && raw.FrameSections.FirstOrDefault(x => x.Name == frame.Section) is { } section)
            Console.WriteLine("Section: " + JsonSerializer.Serialize(section, options));
        if (area is not null && raw.AreaProperties.FirstOrDefault(x => x.Name == area.Property) is { } prop)
            Console.WriteLine("Property: " + JsonSerializer.Serialize(prop, options));
        var materialName = frame?.MaterialOverwrite ?? raw.FrameSections.FirstOrDefault(x => x.Name == frame?.Section)?.Material ?? area?.MaterialOverwrite ?? raw.AreaProperties.FirstOrDefault(x => x.Name == area?.Property)?.Material;
        if (raw.Materials.FirstOrDefault(x => x.Name == materialName) is { } material) Console.WriteLine("Material: " + JsonSerializer.Serialize(material, options));
        if (area is { IsOpening: true })
        {
            Console.WriteLine("\nThis is an opening object; it is deducted from the slabs or walls it overlaps (see their ETABS_OPENINGS_ASSIGNED warnings).");
            return 0;
        }
        Console.WriteLine($"\nQuentra snapshot record ({objectId})");
        object? record = (object?)snapshot.Model.Frames.FirstOrDefault(x => x.ObjectId == objectId) ?? snapshot.Areas.FirstOrDefault(x => x.ObjectId == objectId);
        if (record is not null) Console.WriteLine(JsonSerializer.Serialize(record, record.GetType(), options));
        Console.WriteLine("Metadata: " + JsonSerializer.Serialize(snapshot.Metadata.Single(x => x.ObjectId == objectId), options));
        foreach (var steel in snapshot.Reinforcement.Where(x => x.ObjectId == objectId))
            Console.WriteLine($"Steel input {steel.Component}: {steel.Method}, evidence {steel.DesignEvidence}, {steel.Stations.Length} stations, source {steel.SourceReference}");
        foreach (var w in snapshot.SourceWarnings.Where(x => x.ObjectId == objectId)) Console.WriteLine($"[{w.Code}] {w.Message}");
        if (TakeoffEngine.Problems(snapshot).Any())
        {
            Console.WriteLine("\nThe snapshot has validation errors, so the element was not calculated. Run 'quentra etabs extract' to see them.");
            return 1;
        }
        var result = TakeoffEngine.Run(snapshot, token);
        var element = result.Elements.Single(x => x.ObjectId == objectId);
        Console.WriteLine($"\nCalculated: {element.Category}, {element.Status}; gross {Q(element.GrossM3)} m³, opening-adjusted {Q(element.OpeningAdjustedM3)} m³" +
            (element.AxisLengthM is { } l ? $", axis {l.ToString("F3", CultureInfo.InvariantCulture)} m" : "") + (element.SurfaceAreaM2 is { } a ? $", surface {a.ToString("F3", CultureInfo.InvariantCulture)} m²" : ""));
        Console.WriteLine($"Formula: {element.Formula}");
        foreach (var story in result.StoryAllocations.Where(x => x.ObjectId == objectId)) Console.WriteLine($"Story {story.StoryId}: {Q(story.GrossM3)} m³ gross, {Q(story.OpeningAdjustedM3)} m³ opening-adjusted");
        foreach (var w in element.Warnings) Console.WriteLine($"[{w.Code}] {w.Message}");
        foreach (var s in result.Steel.Where(x => x.ObjectId == objectId)) Console.WriteLine($"Steel {s.Component}: {(s.MassKg is { } kg ? kg.ToString("F1", CultureInfo.InvariantCulture) + " kg" : "unknown")} ({s.Evidence}); {s.Formula}");
        var coverage = result.SteelCoverage.FirstOrDefault(x => x.ObjectId == objectId);
        if (coverage is not null) Console.WriteLine($"Steel required: {string.Join(", ", coverage.Required)}; missing: {(coverage.Missing.IsEmpty ? "none" : string.Join(", ", coverage.Missing))}");
        return 0;
    }

    private static async Task<int> Check(string input, int? pid, bool allowUntested, CancellationToken token)
    {
        var json = await SnapshotJson.ReadAsync(Path.GetFullPath(input), SnapshotJson.MaximumRunBytes, token);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        var snapshot = document.RootElement.TryGetProperty("calculationSha256", out _)
            ? TakeoffJson.Replay(json, token).Snapshot : TakeoffJson.Parse<TakeoffSnapshot>(json);
        if (snapshot.Source is not { } source)
            throw new ArgumentException($"{input} was not extracted from ETABS (it has no ETABS source), so there is no ETABS model to compare it with.");
        var instance = EtabsDiscovery.Choose(EtabsDiscovery.RunningInstances(), pid, allowUntested);
        EtabsRawModel raw;
        using (var connection = LiveEtabs.Connect(instance)) raw = EtabsExtractor.Extract(connection.Reader, DateTimeOffset.UtcNow, token).Raw;
        if (!string.Equals(Path.GetFullPath(raw.Model.ModelPath), Path.GetFullPath(source.ModelPath), StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"ETABS has {raw.Model.ModelPath} open, not {source.ModelPath}. Open the model the run was extracted from and check again.");
            return 1;
        }
        var comparison = SnapshotFingerprint.Compare(snapshot, EtabsSnapshotBuilder.Run(raw, EtabsBuildOptions.RecoveredFrom(snapshot)).Snapshot);
        Console.WriteLine($"Extracted {ReportExporter.Time(source.ExtractedAt)}; checked {ReportExporter.Time(raw.ExtractedAt)} against {raw.Model.ModelPath}");
        Console.WriteLine(comparison.Describe());
        if (!comparison.Matches) Console.WriteLine("Results from this run are out of date. Extract the model again before accepting or exporting.");
        return comparison.Matches ? 0 : 1;
    }

    private static string Q(double? value) => value is { } v ? v.ToString("F3", CultureInfo.InvariantCulture) : "unknown";

    private static bool Matches(string target, string type, string name, string? label, string? story, TakeoffSnapshot snapshot) =>
        target == name || target == $"{type}:{name}" || (label is not null && story is not null && target == $"{label}@{story}");
}
