using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Quentra.Application;
using Quentra.Core;
using Quentra.Infrastructure;

if (args.Length is 1 or 2 && args[0] is "--help" or "-h" or "help")
{
    if (args.Length == 1) { Help(); return 0; }
    if (args[1] == "format")
    {
        await using var reference = typeof(Program).Assembly.GetManifestResourceStream("Quentra.Docs.SNAPSHOT_FORMAT.md")!;
        Console.WriteLine((await new StreamReader(reference).ReadToEndAsync()).ReplaceLineEndings("\n").TrimEnd());
        return 0;
    }
    if (!Commands.ContainsKey(args[1])) { Console.Error.WriteLine($"Quentra: unknown help topic '{args[1]}'. Topics: {string.Join(", ", Commands.Keys)}, format."); return 2; }
    Console.WriteLine(Commands[args[1]].Usage);
    Console.WriteLine();
    Console.WriteLine(Commands[args[1]].Details);
    return 0;
}
if (args.Length == 1 && args[0] == "codes")
{
    foreach (var (code, meaning) in WarningCatalogue.Entries) Console.WriteLine($"{code}\n    {meaning}");
    Console.WriteLine();
    Console.WriteLine(WarningCatalogue.Terms);
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
    if (args.Length > 0 && Commands.TryGetValue(args[0], out var command))
        Console.Error.WriteLine($"Usage:\n{command.Usage}\nRun 'quentra help {args[0]}' for details.");
    else
        Console.Error.WriteLine($"Commands: {string.Join(", ", Commands.Keys)}. Run 'quentra --help' for usage.");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var reading = positional[0];
try
{
    var inputPath = Path.GetFullPath(positional[0]);
    var validating = args[0] == "validate";
    var outputPath = validating ? "" : Path.GetFullPath(positional[^1]);
    if (!validating)
    {
        foreach (var path in positional[..^1])
            if (string.Equals(Path.GetFullPath(path), outputPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Inputs and output must use different paths.");
        if (File.Exists(outputPath) || Directory.Exists(outputPath))
            throw new IOException($"{positional[^1]} already exists. Choose a new path; existing runs and reports are never overwritten.");
    }
    if (args[0] == "template")
    {
        await using var resource = typeof(Program).Assembly.GetManifestResourceStream("Quentra.Templates.snapshot.json")!;
        var template = await new StreamReader(resource).ReadToEndAsync(cancellation.Token);
        await File.WriteAllTextAsync(outputPath, template.ReplaceLineEndings("\n"), new System.Text.UTF8Encoding(false), cancellation.Token);
        Console.WriteLine($"Snapshot template: {outputPath}");
        Console.WriteLine("It is a small synthetic model (beam, columns, slab, wall, steel). Replace its contents with your model;");
        Console.WriteLine("'quentra help format' describes every field. Run 'quentra validate' on it to check it.");
        return 0;
    }
    var takesSnapshot = args[0] is "calculate" or "validate";
    var json = await ReadInput(inputPath, reading, takesSnapshot ? SnapshotJson.MaximumSnapshotBytes : SnapshotJson.MaximumRunBytes, cancellation.Token);
    var isRun = IsRun(json);
    if (takesSnapshot && isRun)
        throw new ArgumentException($"{reading} is a saved run, not a snapshot. Use 'quentra replay' to recompute a saved run.");
    if (!takesSnapshot && !isRun)
        throw new ArgumentException($"{reading} is a snapshot, not a run. Run 'quentra calculate' on it first and pass the run file it writes.");

    if (args[0] is "calculate" or "replay" or "validate" && Schema(json) == 1)
    {
        var package = args[0] == "replay"
            ? SnapshotJson.Replay(json)
            : SnapshotJson.Calculate(SnapshotJson.ParseSnapshot(json));
        if (!validating) await SnapshotJson.WriteAsync(outputPath, package, cancellation.Token);
        PrintFrameRun(package, validating ? null : outputPath);
        return 0;
    }

    TakeoffRun run;
    switch (args[0])
    {
        case "calculate" or "validate":
            if (Schema(json) != 2) throw new NotSupportedException("Snapshot schemaVersion must be 1 (frames) or 2 (takeoff).");
            run = TakeoffJson.Calculate(TakeoffJson.Parse<TakeoffSnapshot>(json), cancellationToken: cancellation.Token);
            if (!validating) break;
            PrintTakeoffRun(run, "Checked", null);
            Console.WriteLine("The snapshot is readable and was calculated. Nothing was written; review the warnings above.");
            return 0;
        case "replay":
            RequireTakeoffRun(json, args[0], reading);
            run = TakeoffJson.Replay(json, cancellation.Token);
            Console.WriteLine("Reproduced: every saved quantity and hash matches a fresh calculation.");
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
                    await ReadInput(Path.GetFullPath(positional[1]), reading, SnapshotJson.MaximumSnapshotBytes, cancellation.Token));
                if (changes.IsDefaultOrEmpty) throw new ArgumentException("The override file must contain at least one override.");
            }
            run = TakeoffJson.AddOverrides(current, changes);
            foreach (var change in changes)
                Console.WriteLine(change.Field == OverrideField.Excluded
                    ? $"Override {change.Id}: {change.ObjectId} {(change.ReplacementValue == 1 ? "excluded" : "included again")}"
                    : $"Override {change.Id}: {change.ObjectId} {Overrides.Label(change.Field)} {Overrides.Mm(change.OriginalValue)} -> {Overrides.Mm(change.ReplacementValue)}");
            break;
        case "accept":
            RequireTakeoffRun(json, args[0], reading);
            run = TakeoffJson.Replay(json, cancellation.Token);
            // Codes are upper case; accept any case so 'modeled_basis' is not reported as missing.
            // Object IDs after ':' are case-sensitive and kept as typed.
            var acknowledged = options["--acknowledge"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.IndexOf(':') is var colon and >= 0 ? x[..colon].ToUpperInvariant() + x[colon..] : x.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal).ToImmutableArray();
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
    Console.Error.WriteLine($"Quentra: {reading}: {JsonErrors.Describe(e)}");
    return 1;
}
catch (UnauthorizedAccessException)
{
    Console.Error.WriteLine("Quentra: permission denied. Check that you can read the input files and write to the output folder.");
    return 1;
}
catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or NotSupportedException)
{
    Console.Error.WriteLine($"Quentra: {e.Message}");
    return 1;
}

static async Task<string> ReadInput(string fullPath, string shown, long maximumBytes, CancellationToken token)
{
    if (Directory.Exists(fullPath)) throw new ArgumentException($"{shown} is a folder. Give the path of a JSON file.");
    if (!File.Exists(fullPath)) throw new FileNotFoundException($"{shown} was not found.");
    return await SnapshotJson.ReadAsync(fullPath, maximumBytes, token);
}

static string? UsageError(string[] args, out string[] positional, out Dictionary<string, string> options, out List<(string Option, string Value)> edits)
{
    positional = []; options = new(StringComparer.Ordinal); edits = [];
    if (args.Length == 0) return "No command given.";
    if (!Commands.ContainsKey(args[0])) return $"Unknown command '{args[0]}'.";
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
    var expected = args[0] == "validate" ? 1 : args[0] == "override" && edits.Count == 0 ? 3 : 2;
    if (positional.Length != expected)
        return $"'{args[0]}' takes {expected} file path{(expected == 1 ? "" : "s")}; {positional.Length} given.";
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

// --set ID.field=value+unit, --exclude ID, --include ID. The unit is required: a bare 700 could be mm or m.
static (string ObjectId, OverrideField Field, double Value) ParseEdit(string option, string text)
{
    if (option == "--exclude") return (text, OverrideField.Excluded, 1);
    if (option == "--include") return (text, OverrideField.Excluded, 0);
    if (Regex.Match(text, @"^.+\.[A-Za-z]+=(?<value>[-+0-9.eE]+)$") is { Success: true } bare)
        throw new ArgumentException($"--set '{text}' needs a unit. Write, for example, {bare.Groups["value"].Value}mm or {bare.Groups["value"].Value}m (mm, m, in and ft are accepted).");
    var match = Regex.Match(text, @"^(?<id>.+)\.(?<field>[A-Za-z]+)=(?<value>[-+0-9.eE]+)(?<unit>mm|m|in|ft)$");
    if (!match.Success) throw new ArgumentException($"--set '{text}' must look like W1.thickness=300mm (units: mm, m, in, ft).");
    OverrideField field = match.Groups["field"].Value.ToLowerInvariant() switch
    {
        "width" => OverrideField.FrameWidthM, "depth" => OverrideField.FrameDepthM,
        "diameter" => OverrideField.FrameDiameterM, "thickness" => OverrideField.AreaThicknessM,
        var other => throw new ArgumentException($"--set field '{other}' is not one of width, depth, diameter, thickness.")
    };
    if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        throw new ArgumentException($"--set '{text}' has an invalid number.");
    if (!double.IsFinite(value)) throw new ArgumentException($"--set '{text}' has an invalid number.");
    var unit = match.Groups["unit"].Value switch { "mm" => LengthUnit.Millimetre, "in" => LengthUnit.Inch, "ft" => LengthUnit.Foot, _ => LengthUnit.Metre };
    return (match.Groups["id"].Value, field, value * Units.MetresPerUnit(unit));
}

static bool IsRun(string json)
{
    using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
    return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("calculationSha256", out _);
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

// Same rounding as the export: m³ to 0.001, kg to 0.1. run.json keeps full precision.
static string Number(double? value, string unit) =>
    value is { } x ? (Math.Round(x, unit == "kg" ? 1 : 3, MidpointRounding.AwayFromZero) + 0.0).ToString(unit == "kg" ? "F1" : "F3", CultureInfo.InvariantCulture) + " " + unit : "unknown";

static void PrintFrameRun(RunPackage package, string? outputPath)
{
    var summary = package.Result.Summary;
    Console.WriteLine("Quentra — draft frame concrete calculation");
    Console.WriteLine($"Source: {package.Snapshot.Origin} — {package.Snapshot.ModelId}");
    Console.WriteLine($"Known gross modeled concrete: {Number(summary.KnownGrossModeledVolume.CubicMetres, "m³")}");
    Console.WriteLine($"Quantified: {summary.QuantifiedCount}/{summary.InScopeCount} in scope; out of scope: {summary.OutOfScopeCount}");
    Console.WriteLine("Steel: unknown (not calculated). Member intersections remain in gross concrete.");
    foreach (var warning in package.Result.Warnings)
        Console.WriteLine($"[{warning.Code}] {warning.Message}");
    foreach (var frame in package.Result.Frames.Where(x => x.GrossModeledVolume is null))
        foreach (var warning in frame.Warnings)
            Console.WriteLine($"[{warning.Code}] {frame.ObjectId}: {warning.Message}");
    Console.WriteLine(outputPath is null ? "Checked. Nothing was written." : $"Run package: {outputPath}");
}

// Totals are still shown, but a total that contains a probable unit slip must say so where it is read.
static string Implausible(IReadOnlyList<string> ids) => ids.Count == 0 ? "" :
    $" (includes {ids.Count} element{(ids.Count == 1 ? "" : "s")} with implausible values: {string.Join(", ", ids.Take(10))}{(ids.Count > 10 ? $" and {ids.Count - 10} more" : "")})";

static void PrintTakeoffRun(TakeoffRun run, string label, string? outputPath)
{
    var s = run.Result.Summary;
    Console.WriteLine("Quentra — concrete and steel takeoff");
    Console.WriteLine($"Source: {run.Snapshot.Model.Origin} — {run.Snapshot.Model.ModelId}");
    Console.WriteLine($"Review status: {TakeoffJson.ReviewStatus(run)}; overrides: {run.Overrides.Length}");
    Console.WriteLine($"Snapshot SHA256: {run.SnapshotSha256}");
    Console.WriteLine($"Quantified: {s.Quantified}/{s.InScope} in scope; unsupported: {s.Unsupported}; invalid: {s.Invalid}; out of scope: {s.OutOfScope}");
    var concreteFlag = Implausible(TakeoffJson.FlaggedElements(run, "IMPLAUSIBLE_DIMENSION"));
    var steelFlag = Implausible(TakeoffJson.FlaggedElements(run, TakeoffJson.PerElementCodes));
    if (run.Result.Warnings.Any(x => x.Code == "IMPLAUSIBLE_POLICY")) steelFlag += " (steel density outside its review band)";
    var complete = TakeoffJson.CompleteLabel(run);
    Console.WriteLine($"Known gross modeled concrete: {Number(s.KnownGrossM3, "m³")}; {complete}: {Number(s.CompleteGrossM3, "m³")}{concreteFlag}");
    Console.WriteLine($"Known opening-adjusted concrete: {Number(s.KnownOpeningAdjustedM3, "m³")}; {complete}: {Number(s.CompleteOpeningAdjustedM3, "m³")}{concreteFlag}");
    Console.WriteLine($"Known steel: {Number(s.KnownSteelKg, "kg")}; {complete}: {Number(s.CompleteSteelKg, "kg")}{steelFlag}; missing components: {s.MissingSteelComponents}");
    foreach (var warning in run.Result.Warnings)
        Console.WriteLine($"[{warning.Code}] {warning.Message}");
    foreach (var element in run.Result.Elements)
        foreach (var warning in element.Warnings)
            Console.WriteLine($"[{warning.Code}] {element.ObjectId}: {warning.Message}");
    foreach (var steel in run.Result.Steel)
        foreach (var warning in steel.Warnings)
            Console.WriteLine($"[{warning.Code}] {steel.ObjectId}/{steel.Component}: {warning.Message}");
    Console.WriteLine($"Warning codes: {string.Join(",", TakeoffJson.RequiredAcknowledgements(run))}  ('quentra codes' explains each)");
    Console.WriteLine("Quantities are rounded to 0.001 m³ and 0.1 kg; the run file keeps full precision.");
    if (outputPath is not null) Console.WriteLine($"{label}: {outputPath}");
}

static void Help()
{
    Console.WriteLine("Quentra — concrete and steel takeoff from a structural model snapshot (development CLI)");
    Console.WriteLine();
    foreach (var (_, (usage, _)) in Commands) Console.WriteLine(usage);
    Console.WriteLine("""
  quentra help <command>   details of one command
  quentra help format      the snapshot field reference
  quentra codes            what each warning code and term means
  quentra --version

Typical order: template -> edit the JSON -> validate -> calculate -> override (optional) -> accept -> export.
Input is a saved JSON snapshot, not an ETABS model file; a live ETABS connection is not implemented.
Unknown quantities are shown as 'unknown', never zero. Existing files are never overwritten.
Exit codes: 0 success, 1 input/IO/validation failure, 2 usage, 130 cancelled.
""");
}

partial class Program
{
    static readonly Dictionary<string, (string Usage, string Details)> Commands = new(StringComparer.Ordinal)
    {
        ["template"] = ("  quentra template  snapshot <new-snapshot.json>", """
Writes a small synthetic example snapshot (beam, columns, slab, wall and their steel) to edit into your model.
'quentra help format' prints the field reference for every snapshot field.
"""),
        ["validate"] = ("  quentra validate  <snapshot.json>", """
Reads and calculates a snapshot without writing anything, and prints every problem with the element it belongs to.
A JSON syntax error or a field of the wrong type (for example an unknown enum value) is reported alone, with its
line; fix it and run again to see the remaining problems. Use it while editing a snapshot.
Exit code 1 means the snapshot cannot be read; warnings do not fail it.
"""),
        ["calculate"] = ("  quentra calculate <snapshot.json> <new-run.json>", """
Calculates a snapshot and saves a run file: the snapshot, results, warnings and hashes.
Schema 2 snapshots give concrete and steel for frames, slabs and walls. Schema 1 (legacy, frame concrete only)
can be calculated and replayed but not overridden, accepted or exported.
"""),
        ["replay"] = ("  quentra replay    <saved-run.json> <new-run.json>", """
Recalculates a saved run and checks that every saved quantity and hash reproduces. Fails if the run was edited.
"""),
        ["override"] = ("""
  quentra override  <run.json> <new-run.json> --reason <text> --author <name>
                    [--set ID.width|depth|diameter|thickness=VALUE<mm|m|in|ft>] [--exclude ID] [--include ID]
  quentra override  <run.json> <overrides.json> <new-run.json>
""".Trim('\n'), """
Records hand changes in a new run. Options can be repeated; each becomes one override (o1, o2, ...).
  --set B1.depth=700mm   A unit is required. Values far outside the review bands are rejected.
  --exclude W1           Takes W1 out of scope (the run becomes partial, and totals are labelled
                         as reduced scope). --include W1 reverses it.
An override that would change nothing is rejected. Changing a frame's section stops its design-demand steel
(DemandEquivalent) being counted, because the demand belongs to the old section; the warning names the override.
The previous acceptance no longer applies to the new run.

An overrides.json file is an array of overrides; --set/--exclude fill these fields in for you:
  [ { "id": "o1", "objectId": "B1", "field": "FrameDepthM", "originalValue": 0.6, "replacementValue": 0.7,
      "reason": "Revised drawing S-201 rev C", "author": "A. Engineer", "recordedAt": "2026-09-27T10:00:00Z",
      "snapshotSha256": "<Snapshot SHA256 printed for the run>" } ]
  field: FrameWidthM, FrameDepthM, FrameDiameterM, AreaThicknessM (values in metres), or Excluded (0 included, 1 excluded).
  originalValue must equal the element's current value.
"""),
        ["accept"] = ("""
  quentra accept    <run.json> <new-run.json> --reviewer <name> --note <text>
                    --acknowledge <CODE,CODE,...> [--partial]
""".Trim('\n'), """
Records a reviewer's acceptance of this exact calculation. List every warning code the run printed
(any letter case); 'quentra codes' explains them. Implausible values are acknowledged per element, exactly as
printed, for example IMPLAUSIBLE_DIMENSION:B1, so each suspected unit slip is looked at. --partial is required when concrete or steel is incomplete
or an element was excluded. The measurement policy must be approved first. Any later override clears acceptance.
"""),
        ["export"] = ("  quentra export    <run.json> <new-directory>", """
Writes CSV files, report.xlsx (with a Contents sheet) and manifest.json with a SHA256 for each file.
Quantities are rounded (m³ to 0.001, kg to 0.1, override dimensions to 0.1 mm); run.json keeps full precision.
"""),
    };
}
