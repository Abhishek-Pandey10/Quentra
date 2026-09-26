using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Quentra.Core;

namespace Quentra.Infrastructure;

public sealed record ReportTable(string Name, string[] Headers, List<object?[]> Rows);
// A report quantity shown to a fixed number of decimals; run.json keeps full precision.
public readonly record struct Rounded(double Value, int Decimals)
{
    public double Display => Math.Round(Value, Decimals, MidpointRounding.AwayFromZero) + 0.0; // + 0.0 turns -0 into 0
    public override string ToString() => Display.ToString("F" + Decimals, CultureInfo.InvariantCulture);
}
public sealed record ExportManifest(int SchemaVersion, string CalculationSha256, string PackageSha256,
    string ReviewStatus, string ConcreteBasis, string SteelBasis, Dictionary<string, string> FilesSha256);

public static class ReportExporter
{
    public const string RoundingPolicy = "Volumes, areas and lengths to 0.001 (m³, m², m); steel to 0.1 kg. run.json keeps full precision.";
    private static Rounded? M3(double? value) => value is { } x ? new Rounded(x, 3) : null;
    private static Rounded? Kg(double? value) => value is { } x ? new Rounded(x, 1) : null;

    public static IReadOnlyList<ReportTable> Tables(TakeoffRun run)
    {
        var r = run.Result; var s = r.Summary;
        var tables = new List<ReportTable>
        {
            new("Summary", ["Field", "Value", "Unit"],
            [
                ["Model", run.Snapshot.Model.ModelId, ""], ["Origin", run.Snapshot.Model.Origin.ToString(), ""],
                ["Source", run.Snapshot.Model.SourceDescription, ""], ["Captured at", run.Snapshot.Model.CapturedAt.ToString("O"), ""],
                ["Calculation version", r.CalculationVersion, ""], ["Snapshot SHA256", run.SnapshotSha256, ""],
                ["Calculation SHA256", run.CalculationSha256, ""], ["Package seal SHA256 (run content, not a file hash)", run.PackageSha256, ""],
                ["Review status", TakeoffJson.ReviewStatus(run), ""], ["Intended use", run.Snapshot.Policy.IntendedUse, ""],
                ["Basis", "Modeled volumes; intersections retained; net/BOQ unavailable", ""],
                ["Found", s.Found, "count"], ["In scope", s.InScope, "count"], ["Quantified", s.Quantified, "count"],
                ["Unsupported", s.Unsupported, "count"], ["Invalid", s.Invalid, "count"], ["Out of scope", s.OutOfScope, "count"],
                ["Known gross", M3(s.KnownGrossM3), "m³"], ["Known opening adjusted", M3(s.KnownOpeningAdjustedM3), "m³"],
                ["Complete gross in declared scope", M3(s.CompleteGrossM3), "m³"], ["Complete opening adjusted in declared scope", M3(s.CompleteOpeningAdjustedM3), "m³"],
                ["Known steel", Kg(s.KnownSteelKg), "kg"], ["Complete steel in declared component scope", Kg(s.CompleteSteelKg), "kg"],
                ["Missing steel components", s.MissingSteelComponents, "count"], ["Null quantities", "Unknown, not zero", ""],
                ["Steel basis", "Demand equivalents, model-assigned equivalents and estimates remain separate in component rows", ""],
                ["Rounding", RoundingPolicy, ""]
            ]),
            new("Elements", ["Object ID", "Category", "Material", "Section", "Status", "Gross (m³)", "Opening deduction (m³)", "Opening-adjusted (m³)", "Surface area (m²)", "Axis length (m)", "Formula", "Source"],
                r.Elements.Select(x => new object?[] { x.ObjectId, x.Category, x.MaterialName, x.SectionName, x.Status.ToString(), M3(x.GrossM3), M3(x.OpeningDeductionM3), M3(x.OpeningAdjustedM3), M3(x.SurfaceAreaM2), M3(x.AxisLengthM), x.Formula, x.SourceReference }).ToList()),
            new("Story Allocations", ["Object ID", "Story", "Gross (m³)", "Opening-adjusted (m³)"],
                r.StoryAllocations.Select(x => new object?[] { x.ObjectId, x.StoryId, M3(x.GrossM3), M3(x.OpeningAdjustedM3) }).ToList()),
            new("Steel Components", ["Object ID", "Component", "Evidence", "Mass (kg)", "Covers", "Source", "Formula", "Assumption", "Approved by", "Approved at"],
                r.Steel.Select(x => new object?[] { x.ObjectId, x.Component, x.Evidence, Kg(x.MassKg), string.Join(";", x.CoversComponents), x.SourceReference, x.Formula, x.Assumption, x.ApprovedBy, x.ApprovedAt.ToString("O") }).ToList()),
            new("Steel Coverage", ["Object ID", "Required", "Missing", "Known mass (kg)", "Complete mass (kg)"],
                r.SteelCoverage.Select(x => new object?[] { x.ObjectId, string.Join(";", x.Required), string.Join(";", x.Missing), Kg(x.KnownMassKg), Kg(x.CompleteMassKg) }).ToList()),
            new("Policy", ["Field", "Value"],
            [
                ["Policy ID", run.Snapshot.Policy.Id], ["Intended use", run.Snapshot.Policy.IntendedUse],
                ["Approved by", run.Snapshot.Policy.ApprovedBy], ["Approved at", run.Snapshot.Policy.ApprovedAt?.ToString("O")],
                ["Steel density (kg/m³)", run.Snapshot.Policy.SteelDensityKgM3], ["Linear tolerance (m)", run.Snapshot.Policy.LinearToleranceM],
                ["Planarity tolerance (m)", run.Snapshot.Policy.PlanarityToleranceM], ["Polygon clipping precision", "1 micrometre in local metres"],
                ["Longitudinal integration", "Interval endpoint maxima; no automatic extrapolation"],
                ["Story allocation", "Vertical members clipped by modeled elevation; slabs use assigned story"],
                ["Steel by story", "Each element's known steel split in proportion to its opening-adjusted volume in each story"],
                ["Default ratios", "None"], ["Laps, anchorage, waste", "Not inferred; only explicitly supplied components are reported"],
                ["Rounding", RoundingPolicy]
            ]),
            new("Overrides", ["ID", "Object ID", "Field", "Original value", "Replacement value", "Unit", "Reason", "Author", "Recorded at", "Snapshot SHA256"],
                run.Overrides.Select(x => new object?[] { x.Id, x.ObjectId, x.Field.ToString(), x.OriginalValue, x.ReplacementValue, x.Field == Application.OverrideField.Excluded ? "boolean 0/1" : "m", x.Reason, x.Author, x.RecordedAt.ToString("O"), x.SnapshotSha256 }).ToList()),
            new("Review History", ["Status", "Reviewer", "Reviewed at", "Calculation SHA256", "Current", "Note", "Acknowledged warnings"],
                run.ReviewHistory.Select(x => new object?[] { x.Status, x.Reviewer, x.ReviewedAt.ToString("O"), x.CalculationSha256, x.CalculationSha256 == run.CalculationSha256 ? "Yes" : "No", x.Note, string.Join(";", x.AcknowledgedWarningCodes) }).ToList()),
            new("Source Index", ["Object ID", "Source", "Original values"],
                run.Snapshot.Model.Frames.Select(x => new object?[] { x.ObjectId, x.SourceReference, "run.json → snapshot.model.frames; original coordinates and section units retained" })
                    .Concat(run.Snapshot.Areas.Select(x => new object?[] { x.ObjectId, x.SourceReference, "run.json → snapshot.areas; original polygon and thickness retained" })).ToList())
        };
        var warningRows = r.Warnings.Select(x => new object?[] { "Run", x.Code, x.Message })
            .Concat(r.Elements.SelectMany(e => e.Warnings.Select(w => new object?[] { e.ObjectId, w.Code, w.Message })))
            .Concat(r.Steel.SelectMany(e => e.Warnings.Select(w => new object?[] { e.ObjectId + "/" + e.Component, w.Code, w.Message }))).ToList();
        tables.Add(new("Warnings", ["Scope", "Code", "Consequence"], warningRows));
        foreach (var (name, groups) in new[] { ("By Category", r.ByCategory), ("By Material", r.ByMaterial) })
            tables.Add(new(name, ["Group", "Count", "Quantified", "Known gross (m³)", "Known opening-adjusted (m³)", "Complete opening-adjusted (m³)"],
                groups.Select(x => new object?[] { x.Group, x.Count, x.QuantifiedCount, M3(x.KnownGrossM3), M3(x.KnownOpeningAdjustedM3), M3(x.CompleteOpeningAdjustedM3) }).ToList()));
        tables.Add(new("By Story", ["Story", "Gross (m³)", "Opening-adjusted (m³)"],
            r.StoryAllocations.GroupBy(x => x.StoryId).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(g => new object?[] { g.Key, M3(g.Sum(x => x.GrossM3)), M3(g.Sum(x => x.OpeningAdjustedM3)) }).ToList()));
        tables.Add(new("By Section", ["Section", "Category", "Count", "Known gross (m³)", "Known opening-adjusted (m³)", "Unquantified"],
            r.Elements.Where(x => x.Status != Core.QuantityStatus.OutOfScope).GroupBy(x => (x.SectionName, x.Category))
                .OrderBy(x => x.Key.SectionName, StringComparer.Ordinal).ThenBy(x => x.Key.Category, StringComparer.Ordinal)
                .Select(g => new object?[] { g.Key.SectionName, g.Key.Category, g.Count(), M3(g.Sum(x => x.GrossM3 ?? 0)), M3(g.Sum(x => x.OpeningAdjustedM3 ?? 0)), g.Count(x => x.OpeningAdjustedM3 is null) }).ToList()));
        tables.AddRange(SteelTables(r));
        tables.Add(new("Exclusions", ["Object ID", "Status", "Reason"], r.Elements.Where(x => x.Status != Core.QuantityStatus.Quantified)
            .Select(x => new object?[] { x.ObjectId, x.Status.ToString(), string.Join("; ", x.Warnings.Select(w => w.Message)) }).ToList()));
        return tables;
    }

    private static IEnumerable<ReportTable> SteelTables(TakeoffResult r)
    {
        // Totals cover in-scope elements; a group is complete only when every element in it has complete steel.
        var elements = r.Elements.ToDictionary(x => x.ObjectId, StringComparer.Ordinal);
        string[] headers = ["Group", "Elements", "Known steel (kg)", "Complete steel (kg)", "Missing components"];
        ReportTable Group(string name, IEnumerable<(string Key, SteelCoverage Coverage, double Share)> rows) => new(name, headers,
            rows.GroupBy(x => x.Key).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new object?[]
            {
                g.Key, g.Select(x => x.Coverage.ObjectId).Distinct().Count(), Kg(g.Sum(x => x.Coverage.KnownMassKg * x.Share)),
                g.All(x => x.Coverage.CompleteMassKg is not null) ? Kg(g.Sum(x => x.Coverage.CompleteMassKg!.Value * x.Share)) : null,
                g.DistinctBy(x => x.Coverage.ObjectId).Sum(x => x.Coverage.Missing.Length)
            }).ToList());
        yield return Group("Steel By Category", r.SteelCoverage.Select(x => (elements[x.ObjectId].Category, x, 1.0)));
        yield return Group("Steel By Material", r.SteelCoverage.Select(x => (elements[x.ObjectId].MaterialName, x, 1.0)));
        var allocations = r.StoryAllocations.ToLookup(x => x.ObjectId);
        yield return Group("Steel By Story", r.SteelCoverage.SelectMany(x =>
        {
            var rows = allocations[x.ObjectId].ToArray();
            var total = rows.Sum(a => a.OpeningAdjustedM3);
            return rows.Length == 0 || total <= 0
                ? [("Unallocated", x, 1.0)]
                : rows.Select(a => (a.StoryId, x, a.OpeningAdjustedM3 / total));
        }));
        yield return new("Steel By Evidence", ["Evidence", "Components", "Known steel (kg)", "Unknown components"],
            r.Steel.GroupBy(x => x.Evidence).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new object?[] { g.Key, g.Count(), Kg(g.Sum(x => x.MassKg ?? 0)), g.Count(x => x.MassKg is null) }).ToList());
    }

    public static async Task ExportAsync(TakeoffRun run, string newDirectory, CancellationToken cancellationToken = default)
    {
        // Verify the run before making a report projection.
        run = TakeoffJson.Replay(TakeoffJson.Serialize(run), cancellationToken);
        var destination = Path.GetFullPath(newDirectory);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Export destination must not exist.");
        var parent = Path.GetDirectoryName(destination)!; Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".quentra-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var tables = Tables(run);
            await TakeoffJson.WriteAsync(Path.Combine(staging, "run.json"), run, cancellationToken);
            foreach (var table in tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await File.WriteAllTextAsync(Path.Combine(staging, table.Name.Replace(' ', '_') + ".csv"), Csv(table), new UTF8Encoding(true), cancellationToken);
            }
            WriteWorkbook(Path.Combine(staging, "report.xlsx"), tables, run.Snapshot.Model.CapturedAt, cancellationToken);
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(staging).Order(StringComparer.Ordinal))
            {
                await using var stream = File.OpenRead(file);
                hashes.Add(Path.GetFileName(file), Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant());
            }
            await TakeoffJson.WriteAsync(Path.Combine(staging, "manifest.json"), new ExportManifest(1, run.CalculationSha256,
                run.PackageSha256, TakeoffJson.ReviewStatus(run), "Gross and opening-adjusted modeled volumes, m3", "Component evidence and kg; null is unknown", hashes), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); Directory.Move(staging, destination);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    public static string Csv(ReportTable table)
    {
        var builder = new StringBuilder();
        // RFC 4180 CRLF regardless of platform, so export hashes match across operating systems.
        builder.Append(string.Join(",", table.Headers.Select(x => Quote(SafeText(x))))).Append("\r\n");
        foreach (var row in table.Rows)
            builder.Append(string.Join(",", row.Select(x => Quote(x switch
            {
                null => "", string text => SafeText(text), Rounded value => value.ToString(), IFormattable value => value.ToString(null, CultureInfo.InvariantCulture), _ => SafeText(x.ToString() ?? "")
            })))).Append("\r\n");
        return builder.ToString();
    }

    public static string SafeText(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.Length > 0 && ("=+-@".Contains(trimmed[0]) || text[0] is '\t' or '\r' or '\n') ? "'" + text : text;
    }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static void WriteWorkbook(string path, IReadOnlyList<ReportTable> tables, DateTimeOffset created, CancellationToken token)
    {
        using var workbook = new XLWorkbook();
        foreach (var table in tables)
        {
            // Excel permits 1,048,576 rows; leave one header row per sheet.
            var chunks = table.Rows.Chunk(1_048_575).ToArray();
            if (chunks.Length == 0) chunks = [[]];
            for (var part = 0; part < chunks.Length; part++)
            {
                token.ThrowIfCancellationRequested();
                var sheet = workbook.AddWorksheet(chunks.Length == 1 ? table.Name : table.Name + " " + (part + 1));
                for (var c = 0; c < table.Headers.Length; c++) sheet.Cell(1, c + 1).Value = table.Headers[c];
                for (var row = 0; row < chunks[part].Length; row++)
                {
                    if (row % 1000 == 0) token.ThrowIfCancellationRequested();
                    for (var c = 0; c < chunks[part][row].Length; c++)
                    {
                        var cell = sheet.Cell(row + 2, c + 1); var value = chunks[part][row][c];
                        if (value is Rounded quantity) { cell.Value = quantity.Display; cell.Style.NumberFormat.Format = quantity.Decimals == 1 ? "#,##0.0" : "#,##0.000"; }
                        else if (value is double number) cell.Value = number;
                        else if (value is int integer) cell.Value = integer;
                        else if (value is not null)
                        {
                            var text = SafeText(value.ToString()!);
                            if (text.Length > 32767) throw new ArgumentException("A report field exceeds Excel's cell text limit; shorten it before export.");
                            cell.Value = text;
                        }
                    }
                }
                sheet.Row(1).Style.Font.Bold = true;
                sheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                sheet.SheetView.FreezeRows(1); sheet.Columns(1, table.Headers.Length).Width = 24;
                sheet.Range(1, 1, Math.Max(1, chunks[part].Length + 1), table.Headers.Length).SetAutoFilter();
            }
        }
        using var buffer = new MemoryStream();
        workbook.SaveAs(buffer);
        WriteReproducible(buffer, path, created);
    }

    // System.IO.Packaging gives the core-properties part and its relationship random names and stamps the
    // current time on it and on every zip entry. Fixing these makes repeated exports of a run byte-identical.
    private static void WriteReproducible(MemoryStream source, string path, DateTimeOffset created)
    {
        const string corePart = "package/services/metadata/core-properties/core.psmdcp";
        var entryTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var stamp = created.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        source.Position = 0;
        using var input = new ZipArchive(source, ZipArchiveMode.Read);
        var randomCore = input.Entries.Single(x => x.FullName.EndsWith(".psmdcp", StringComparison.Ordinal)).FullName;
        using var file = new FileStream(path, FileMode.CreateNew);
        using var output = new ZipArchive(file, ZipArchiveMode.Create);
        foreach (var entry in input.Entries)
        {
            using var reader = entry.Open();
            using var content = new MemoryStream();
            reader.CopyTo(content);
            var bytes = content.ToArray();
            if (entry.FullName == randomCore)
                bytes = Encoding.UTF8.GetBytes(Regex.Replace(Encoding.UTF8.GetString(bytes), @"(<dcterms:(created|modified)[^>]*>)[^<]*", "${1}" + stamp));
            else if (entry.FullName == "_rels/.rels")
            {
                var ids = 0;
                var rels = Encoding.UTF8.GetString(bytes).Replace("/" + randomCore, "/" + corePart, StringComparison.Ordinal);
                bytes = Encoding.UTF8.GetBytes(Regex.Replace(rels, "Id=\"R[0-9a-f]{16}\"", _ => $"Id=\"rPkg{++ids}\""));
            }
            var target = output.CreateEntry(entry.FullName == randomCore ? corePart : entry.FullName, CompressionLevel.Optimal);
            target.LastWriteTime = entryTime;
            using var writer = target.Open();
            writer.Write(bytes);
        }
    }
}
