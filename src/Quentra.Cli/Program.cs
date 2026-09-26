using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Quentra.Application;
using Quentra.Core;
using Quentra.Infrastructure;

if (args.Length == 1 && args[0] is "--help" or "-h" or "help")
{
    Help();
    return 0;
}
if (args.Length == 1 && args[0] is "--version")
{
    var version = typeof(TakeoffJson).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    Console.WriteLine($"Quentra {version} (engines {TakeoffEngine.Version}, {CalculateSnapshot.Version})");
    return 0;
}
if (UsageError(args, out var positional, out var options, out var edits) is { } usageError)
{
    Console.Error.WriteLine($"Quentra: {usageError}");
    Help();
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var reading = positional[0];
try
{
    var inputPath = Path.GetFullPath(positional[0]);
    var outputPath = Path.GetFullPath(positional[^1]);
    foreach (var path in positional[..^1])
        if (string.Equals(Path.GetFullPath(path), outputPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Inputs and output must use different paths.");
    if (File.Exists(outputPath) || Directory.Exists(outputPath))
        throw new IOException("Output already exists. Choose a new path to preserve the previous run.");
    if (args[0] == "template")
    {
        await using var resource = typeof(Program).Assembly.GetManifestResourceStream("Quentra.Templates.snapshot.json")!;
        var template = await new StreamReader(resource).ReadToEndAsync(cancellation.Token);
        await File.WriteAllTextAsync(outputPath, template.ReplaceLineEndings("\n"), new System.Text.UTF8Encoding(false), cancellation.Token);
        Console.WriteLine($"Snapshot template: {outputPath}");
        Console.WriteLine("It is a small synthetic model (beam, columns, slab, wall, steel). Replace its contents with your model;");
        Console.WriteLine("docs/SNAPSHOT_FORMAT.md describes every field. Run 'quentra calculate' on it to check it.");
        return 0;
    }
    var json = await SnapshotJson.ReadAsync(inputPath,
        args[0] == "calculate" ? SnapshotJson.MaximumSnapshotBytes : SnapshotJson.MaximumRunBytes, cancellation.Token);
    var isRun = IsRun(json);
    if (args[0] == "calculate" && isRun)
        throw new ArgumentException($"{reading} is a saved run, not a snapshot. Use 'quentra replay' to recompute a saved run.");
    if (args[0] != "calculate" && !isRun)
        throw new ArgumentException($"{reading} is a snapshot, not a run. Run 'quentra calculate' on it first and pass the run file it writes.");

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
            if (Schema(json) != 2) throw new NotSupportedException("Snapshot schemaVersion must be 1 (frames) or 2 (takeoff).");
            run = TakeoffJson.Calculate(TakeoffJson.Parse<TakeoffSnapshot>(json), cancellationToken: cancellation.Token);
            break;
        case "replay":
            RequireTakeoffRun(json, args[0], reading);
            run = TakeoffJson.Replay(json, cancellation.Token);
            break;
        case "override":
            RequireTakeoffRun(json, args[0], reading);
            var current = TakeoffJson.Replay(json, cancellation.Token);
            ImmutableArray<QuantityOverride> changes;
            if (edits.Count > 0)
            {
                var at = DateTimeOffset.UtcNow;
                var built = ImmutableArray.CreateBuilder<QuantityOverride>();
                foreach (var (option, value) in edits)
                {
                    var (objectId, field, replacement) = ParseEdit(option, value);
                    built.Add(Overrides.Prepare(current.Snapshot, current.SnapshotSha256, current.Overrides.AddRange(built),
                        objectId, field, replacement, options["--reason"], options["--author"], at));
                }
                changes = built.ToImmutable();
            }
            else
            {
                reading = positional[1];
                changes = TakeoffJson.Parse<ImmutableArray<QuantityOverride>>(
                    await SnapshotJson.ReadAsync(Path.GetFullPath(positional[1]), SnapshotJson.MaximumSnapshotBytes, cancellation.Token));
                if (changes.IsDefaultOrEmpty) throw new ArgumentException("The override file must contain at least one override.");
            }
            run = TakeoffJson.AddOverrides(current, changes);
            foreach (var change in changes)
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"Override {change.Id}: {change.ObjectId} {change.Field} {change.OriginalValue:G6} -> {change.ReplacementValue:G6}"));
            break;
        case "accept":
            RequireTakeoffRun(json, args[0], reading);
            run = TakeoffJson.Replay(json, cancellation.Token);
            var acknowledged = options["--acknowledge"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableArray();
            var unacknowledged = TakeoffJson.WarningCodes(run).Except(acknowledged, StringComparer.Ordinal).ToArray();
            if (unacknowledged.Length > 0)
                throw new ArgumentException("Acknowledge every current warning code before acceptance. Missing: " + string.Join(",", unacknowledged));
            run = TakeoffJson.Accept(run, options["--reviewer"], options["--note"], options.ContainsKey("--partial"), acknowledged, DateTimeOffset.UtcNow);
            break;
        default: // export
            RequireTakeoffRun(json, args[0], reading);
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
catch (JsonException e)
{
    Console.Error.WriteLine($"Quentra: {reading}: {DescribeJsonError(e)}");
    return 1;
}
catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
{
    Console.Error.WriteLine($"Quentra: {e.Message}");
    return 1;
}

static string? UsageError(string[] args, out string[] positional, out Dictionary<string, string> options, out List<(string Option, string Value)> edits)
{
    positional = []; options = new(StringComparer.Ordinal); edits = [];
    if (args.Length == 0) return "No command given.";
    if (args[0] is not ("calculate" or "replay" or "override" or "accept" or "export" or "template")) return $"Unknown command '{args[0]}'.";
    string[] allowed = args[0] switch
    {
        "accept" => ["--reviewer", "--note", "--acknowledge", "--partial"],
        "override" => ["--set", "--exclude", "--include", "--reason", "--author"],
        _ => []
    };
    var list = new List<string>();
    for (var i = 1; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal)) { list.Add(args[i]); continue; }
        if (!allowed.Contains(args[i])) return allowed.Length == 0 ? $"'{args[0]}' takes no options; '{args[i]}' is not recognised." : $"Unknown option '{args[i]}' for '{args[0]}'.";
        if (args[i] == "--partial") { if (!options.TryAdd("--partial", "")) return "--partial is given twice."; continue; }
        if (i + 1 >= args.Length) return $"{args[i]} needs a value.";
        if (args[i] is "--set" or "--exclude" or "--include") { edits.Add((args[i], args[++i])); continue; }
        if (!options.TryAdd(args[i], args[++i])) return $"{args[i - 1]} is given twice.";
    }
    positional = [.. list];
    if (args[0] == "template")
    {
        if (positional is not ["snapshot", var target]) return "Usage: quentra template snapshot <new-snapshot.json>";
        positional = [target];
        return null;
    }
    var expected = args[0] == "override" && edits.Count == 0 ? 3 : 2;
    if (positional.Length != expected)
        return $"'{args[0]}' takes {expected} file paths; {positional.Length} given.";
    string[] required = args[0] switch
    {
        "accept" => ["--reviewer", "--note", "--acknowledge"],
        "override" when edits.Count > 0 => ["--reason", "--author"],
        "override" when options.Count > 0 => ["--set, --exclude or --include"],
        _ => []
    };
    var given = options;
    var missing = required.Where(x => !given.ContainsKey(x)).ToArray();
    if (missing.Length > 0)
        return $"{args[0]} is missing {string.Join(", ", missing)}." + (args[0] == "accept" ? " List the warning codes printed by the run in --acknowledge." : "");
    return null;
}

// --set ID.field=value[unit], --exclude ID, --include ID. Values without a unit are metres.
static (string ObjectId, OverrideField Field, double Value) ParseEdit(string option, string text)
{
    if (option == "--exclude") return (text, OverrideField.Excluded, 1);
    if (option == "--include") return (text, OverrideField.Excluded, 0);
    var match = Regex.Match(text, @"^(?<id>.+)\.(?<field>[A-Za-z]+)=(?<value>[-+0-9.eE]+)(?<unit>mm|m|in|ft)?$");
    if (!match.Success) throw new ArgumentException($"--set '{text}' must look like W1.thickness=0.3 (metres) or W1.thickness=300mm.");
    OverrideField field = match.Groups["field"].Value.ToLowerInvariant() switch
    {
        "width" => OverrideField.FrameWidthM, "depth" => OverrideField.FrameDepthM,
        "diameter" => OverrideField.FrameDiameterM, "thickness" => OverrideField.AreaThicknessM,
        var other => throw new ArgumentException($"--set field '{other}' is not one of width, depth, diameter, thickness.")
    };
    if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        throw new ArgumentException($"--set '{text}' has an invalid number.");
    var unit = match.Groups["unit"].Value switch { "mm" => LengthUnit.Millimetre, "in" => LengthUnit.Inch, "ft" => LengthUnit.Foot, _ => LengthUnit.Metre };
    return (match.Groups["id"].Value, field, value * Units.MetresPerUnit(unit));
}

static bool IsRun(string json)
{
    using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
    return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("calculationSha256", out _);
}

// System.Text.Json messages name .NET types; restate them in terms of the JSON document.
static string DescribeJsonError(JsonException e)
{
    var where = e.Path is { } path ? $" at {path}" + (e.LineNumber is { } line ? $" (line {line + 1})" : "") : "";
    var message = e.Message;
    if (Regex.Match(message, "The JSON property '(.+?)' could not be mapped") is { Success: true } unknown)
        return $"unknown property '{unknown.Groups[1].Value}'{where}. Check the spelling and that this is the right kind of file.";
    if (Regex.Match(message, "missing required properties including: (.+?)\\.( |$)") is { Success: true } required)
        return $"missing required properties {required.Groups[1].Value}{where}.";
    if (message.Contains("could not be converted to", StringComparison.Ordinal))
        return $"value has the wrong type{where}.";
    if (e.Path is null && message.Contains("LineNumber:", StringComparison.Ordinal))
        return $"not valid JSON: {message}";
    if (message.Contains("System.", StringComparison.Ordinal) || message.Contains("Quentra.", StringComparison.Ordinal))
        return $"the document does not match the expected format{where}.";
    return message;
}

static int Schema(string json)
{
    try { return TakeoffJson.SchemaVersion(json); }
    catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
    {
        throw new JsonException("Input requires an integer top-level schemaVersion.", e);
    }
}

static void RequireTakeoffRun(string json, string command, string path)
{
    if (Schema(json) == 1 && command != "replay")
        throw new NotSupportedException($"'{command}' needs a schema 2 takeoff run; {path} is a schema 1 frame-concrete run, which supports calculate and replay only.");
    if (Schema(json) != 2) throw new NotSupportedException($"{path} has an unsupported run schemaVersion.");
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
  quentra override  <run.json> <new-run.json> --reason <text> --author <name>
                    [--set ID.width|depth|diameter|thickness=VALUE[mm|m|in|ft]]
                    [--exclude ID] [--include ID]   (options repeatable)
  quentra accept    <run.json> <new-run.json> --reviewer <name> --note <text>
                    --acknowledge <CODE,CODE,...> [--partial]
  quentra export    <run.json> <new-directory>
  quentra template  snapshot <new-snapshot.json>
  quentra --version | --help

calculate/replay detect the schema: 1 = frame concrete only, 2 = takeoff with
slabs, walls, stories and steel. override, accept and export need schema 2 runs.
overrides.json is an array of recorded overrides bound to the run's snapshot SHA256
(fields are listed in the README); --set/--exclude fill in original values and the hash.
'template snapshot' writes a starter snapshot; docs/SNAPSHOT_FORMAT.md describes it.
Acceptance lists every current warning code; --partial is required for partial runs.
Input is a saved snapshot, not an ETABS model file. Live ETABS is not implemented.
Null quantities are unknown, not zero. Existing files are never overwritten.
Exit codes: 0 success, 1 input/IO/validation failure, 2 usage, 130 cancel.
""");
