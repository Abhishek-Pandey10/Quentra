using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Quentra.Application;
using Quentra.Core;
using Quentra.Infrastructure;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Help();
    return 0;
}
if (!ValidUsage(args, out var positional, out var options))
{
    Help();
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var inputPath = Path.GetFullPath(positional[0]);
    var outputPath = Path.GetFullPath(positional[^1]);
    foreach (var path in positional[..^1])
        if (string.Equals(Path.GetFullPath(path), outputPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Inputs and output must use different paths.");
    if (File.Exists(outputPath) || Directory.Exists(outputPath))
        throw new IOException("Output already exists. Choose a new path to preserve the previous run.");
    var json = await SnapshotJson.ReadAsync(inputPath, cancellation.Token);

    if (args[0] is "calculate" or "replay" && Schema(json) == 1)
    {
        var package = args[0] == "calculate"
            ? SnapshotJson.Calculate(SnapshotJson.ParseSnapshot(json))
            : SnapshotJson.Replay(json);
        await SnapshotJson.WriteAsync(outputPath, package, cancellation.Token);
        PrintFrameRun(package, outputPath);
        return 0;
    }

    TakeoffRun run;
    switch (args[0])
    {
        case "calculate":
            if (Schema(json) != 2) throw new NotSupportedException("Snapshot schema must be 1 (frames) or 2 (takeoff).");
            run = TakeoffJson.Calculate(TakeoffJson.Parse<TakeoffSnapshot>(json), cancellationToken: cancellation.Token);
            break;
        case "replay":
            RequireTakeoffRun(json);
            run = TakeoffJson.Replay(json, cancellation.Token);
            break;
        case "override":
            RequireTakeoffRun(json);
            var changes = TakeoffJson.Parse<ImmutableArray<QuantityOverride>>(
                await SnapshotJson.ReadAsync(Path.GetFullPath(positional[1]), cancellation.Token));
            if (changes.IsDefaultOrEmpty) throw new ArgumentException("The override file must contain at least one override.");
            run = TakeoffJson.AddOverrides(TakeoffJson.Replay(json, cancellation.Token), changes);
            break;
        case "accept":
            RequireTakeoffRun(json);
            run = TakeoffJson.Replay(json, cancellation.Token);
            var acknowledged = options["--acknowledge"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableArray();
            var unacknowledged = TakeoffJson.WarningCodes(run).Except(acknowledged, StringComparer.Ordinal).ToArray();
            if (unacknowledged.Length > 0)
                throw new ArgumentException("Acknowledge every current warning code before acceptance. Missing: " + string.Join(",", unacknowledged));
            run = TakeoffJson.Accept(run, options["--reviewer"], options["--note"], options.ContainsKey("--partial"), acknowledged, DateTimeOffset.UtcNow);
            break;
        default: // export
            RequireTakeoffRun(json);
            run = TakeoffJson.Replay(json, cancellation.Token);
            await ReportExporter.ExportAsync(run, outputPath, cancellation.Token);
            PrintTakeoffRun(run, "Report package", outputPath);
            return 0;
    }
    await TakeoffJson.WriteAsync(outputPath, run, cancellation.Token);
    PrintTakeoffRun(run, "Run package", outputPath);
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled. No completed report was published.");
    return 130;
}
catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or NotSupportedException)
{
    Console.Error.WriteLine($"Quentra: {e.Message}");
    return 1;
}

static bool ValidUsage(string[] args, out string[] positional, out Dictionary<string, string> options)
{
    positional = []; options = new(StringComparer.Ordinal);
    if (args.Length == 0) return false;
    var list = new List<string>();
    for (var i = 1; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal)) { list.Add(args[i]); continue; }
        if (args[i] == "--partial") { if (!options.TryAdd("--partial", "")) return false; continue; }
        if (args[i] is not ("--reviewer" or "--note" or "--acknowledge") || i + 1 >= args.Length || !options.TryAdd(args[i], args[++i])) return false;
    }
    positional = [.. list];
    return args[0] switch
    {
        "calculate" or "replay" or "export" => positional.Length == 2 && options.Count == 0,
        "override" => positional.Length == 3 && options.Count == 0,
        "accept" => positional.Length == 2 && options.ContainsKey("--reviewer") && options.ContainsKey("--note") && options.ContainsKey("--acknowledge"),
        _ => false
    };
}

static int Schema(string json)
{
    try { return TakeoffJson.SchemaVersion(json); }
    catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
    {
        throw new JsonException("Input requires an integer top-level schemaVersion.", e);
    }
}

static void RequireTakeoffRun(string json)
{
    if (Schema(json) != 2) throw new NotSupportedException("This command requires a schema 2 takeoff run produced by 'calculate'.");
}

static string Number(double? value, string unit) =>
    value is { } x ? x.ToString("0.######", CultureInfo.InvariantCulture) + " " + unit : "unknown";

static void PrintFrameRun(RunPackage package, string outputPath)
{
    var summary = package.Result.Summary;
    Console.WriteLine("Quentra — draft frame concrete calculation");
    Console.WriteLine($"Source: {package.Snapshot.Origin} — {package.Snapshot.ModelId}");
    Console.WriteLine($"Known gross modeled concrete: {summary.KnownGrossModeledVolume.CubicMetres.ToString("G12", CultureInfo.InvariantCulture)} m³");
    Console.WriteLine($"Quantified: {summary.QuantifiedCount}/{summary.InScopeCount} in scope; out of scope: {summary.OutOfScopeCount}");
    Console.WriteLine("Steel: unknown (not calculated). Member intersections remain in gross concrete.");
    foreach (var warning in package.Result.Warnings)
        Console.WriteLine($"[{warning.Code}] {warning.Message}");
    foreach (var frame in package.Result.Frames.Where(x => x.GrossModeledVolume is null))
        foreach (var warning in frame.Warnings)
            Console.WriteLine($"[{warning.Code}] {frame.ObjectId}: {warning.Message}");
    Console.WriteLine($"Run package: {outputPath}");
}

static void PrintTakeoffRun(TakeoffRun run, string label, string outputPath)
{
    var s = run.Result.Summary;
    Console.WriteLine("Quentra — concrete and steel takeoff");
    Console.WriteLine($"Source: {run.Snapshot.Model.Origin} — {run.Snapshot.Model.ModelId}");
    Console.WriteLine($"Review status: {TakeoffJson.ReviewStatus(run)}; overrides: {run.Overrides.Length}");
    Console.WriteLine($"Snapshot SHA256: {run.SnapshotSha256}");
    Console.WriteLine($"Quantified: {s.Quantified}/{s.InScope} in scope; unsupported: {s.Unsupported}; invalid: {s.Invalid}; out of scope: {s.OutOfScope}");
    Console.WriteLine($"Known gross modeled concrete: {Number(s.KnownGrossM3, "m³")}; complete: {Number(s.CompleteGrossM3, "m³")}");
    Console.WriteLine($"Known opening-adjusted concrete: {Number(s.KnownOpeningAdjustedM3, "m³")}; complete: {Number(s.CompleteOpeningAdjustedM3, "m³")}");
    Console.WriteLine($"Known steel: {Number(s.KnownSteelKg, "kg")}; complete: {Number(s.CompleteSteelKg, "kg")}; missing components: {s.MissingSteelComponents}");
    foreach (var warning in run.Result.Warnings)
        Console.WriteLine($"[{warning.Code}] {warning.Message}");
    foreach (var element in run.Result.Elements)
        foreach (var warning in element.Warnings)
            Console.WriteLine($"[{warning.Code}] {element.ObjectId}: {warning.Message}");
    foreach (var steel in run.Result.Steel)
        foreach (var warning in steel.Warnings)
            Console.WriteLine($"[{warning.Code}] {steel.ObjectId}/{steel.Component}: {warning.Message}");
    Console.WriteLine($"Warning codes: {string.Join(",", TakeoffJson.WarningCodes(run))}");
    Console.WriteLine($"{label}: {outputPath}");
}

static void Help() => Console.WriteLine("""
Quentra — concrete and steel takeoff development CLI
  quentra calculate <snapshot.json> <new-run.json>
  quentra replay    <saved-run.json> <new-run.json>
  quentra override  <run.json> <overrides.json> <new-run.json>
  quentra accept    <run.json> <new-run.json> --reviewer <name> --note <text>
                    --acknowledge <CODE,CODE,...> [--partial]
  quentra export    <run.json> <new-directory>

calculate/replay detect the schema: 1 = frame concrete only, 2 = takeoff with
slabs, walls, stories and steel. override, accept and export need schema 2 runs.
overrides.json is an array of recorded overrides bound to the run's snapshot SHA256.
Acceptance lists every current warning code; --partial is required for partial runs.
Input is a saved snapshot, not an ETABS model file. Live ETABS is not implemented.
Null quantities are unknown, not zero. Existing files are never overwritten.
Exit codes: 0 success, 1 input/IO/validation failure, 2 usage, 130 cancel.
""");
