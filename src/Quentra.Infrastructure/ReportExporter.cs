using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;

namespace Quentra.Infrastructure;

public sealed record ReportTable(string Name, string[] Headers, List<object?[]> Rows);
public sealed record ExportManifest(int SchemaVersion, string CalculationSha256, string PackageSha256,
    string ReviewStatus, string ConcreteBasis, string SteelBasis, Dictionary<string, string> FilesSha256);

public static class ReportExporter
{
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
                ["Known gross", s.KnownGrossM3, "m3"], ["Known opening adjusted", s.KnownOpeningAdjustedM3, "m3"],
                ["Complete gross in declared scope", s.CompleteGrossM3, "m3"], ["Complete opening adjusted in declared scope", s.CompleteOpeningAdjustedM3, "m3"],
                ["Known steel", s.KnownSteelKg, "kg"], ["Complete steel in declared component scope", s.CompleteSteelKg, "kg"],
                ["Missing steel components", s.MissingSteelComponents, "count"], ["Null quantities", "Unknown, not zero", ""],
                ["Steel basis", "Demand equivalents, model-assigned equivalents and estimates remain separate in component rows", ""]
            ]),
            new("Elements", ["ObjectId", "Category", "Material", "Section", "Status", "GrossM3", "OpeningDeductionM3", "OpeningAdjustedM3", "SurfaceAreaM2", "AxisLengthM", "Formula", "Source"],
                r.Elements.Select(x => new object?[] { x.ObjectId, x.Category, x.MaterialName, x.SectionName, x.Status.ToString(), x.GrossM3, x.OpeningDeductionM3, x.OpeningAdjustedM3, x.SurfaceAreaM2, x.AxisLengthM, x.Formula, x.SourceReference }).ToList()),
            new("Story Allocations", ["ObjectId", "Story", "GrossM3", "OpeningAdjustedM3"],
                r.StoryAllocations.Select(x => new object?[] { x.ObjectId, x.StoryId, x.GrossM3, x.OpeningAdjustedM3 }).ToList()),
            new("Steel Components", ["ObjectId", "Component", "Evidence", "MassKg", "Covers", "Source", "Formula", "Assumption", "ApprovedBy", "ApprovedAt"],
                r.Steel.Select(x => new object?[] { x.ObjectId, x.Component, x.Evidence, x.MassKg, string.Join(";", x.CoversComponents), x.SourceReference, x.Formula, x.Assumption, x.ApprovedBy, x.ApprovedAt.ToString("O") }).ToList()),
            new("Steel Coverage", ["ObjectId", "Required", "Missing", "KnownMassKg", "CompleteMassKg"],
                r.SteelCoverage.Select(x => new object?[] { x.ObjectId, string.Join(";", x.Required), string.Join(";", x.Missing), x.KnownMassKg, x.CompleteMassKg }).ToList()),
            new("Policy", ["Field", "Value"],
            [
                ["PolicyId", run.Snapshot.Policy.Id], ["IntendedUse", run.Snapshot.Policy.IntendedUse],
                ["ApprovedBy", run.Snapshot.Policy.ApprovedBy], ["ApprovedAt", run.Snapshot.Policy.ApprovedAt?.ToString("O")],
                ["SteelDensityKgM3", run.Snapshot.Policy.SteelDensityKgM3], ["LinearToleranceM", run.Snapshot.Policy.LinearToleranceM],
                ["PlanarityToleranceM", run.Snapshot.Policy.PlanarityToleranceM], ["Polygon clipping precision", "1 micrometre in local metres"],
                ["Longitudinal integration", "Interval endpoint maxima; no automatic extrapolation"],
                ["Story allocation", "Vertical members clipped by modeled elevation; slabs use assigned story"],
                ["Default ratios", "None"], ["Laps, anchorage, waste", "Not inferred; only explicitly supplied components are reported"]
            ]),
            new("Overrides", ["Id", "ObjectId", "Field", "Original", "Replacement", "Unit", "Reason", "Author", "At", "SnapshotSha256"],
                run.Overrides.Select(x => new object?[] { x.Id, x.ObjectId, x.Field.ToString(), x.OriginalValue, x.ReplacementValue, x.Field == Application.OverrideField.Excluded ? "boolean 0/1" : "m", x.Reason, x.Author, x.RecordedAt.ToString("O"), x.SnapshotSha256 }).ToList()),
            new("Review History", ["Status", "Reviewer", "At", "CalculationSha256", "Current", "Note", "AcknowledgedWarnings"],
                run.ReviewHistory.Select(x => new object?[] { x.Status, x.Reviewer, x.ReviewedAt.ToString("O"), x.CalculationSha256, x.CalculationSha256 == run.CalculationSha256 ? "Yes" : "No", x.Note, string.Join(";", x.AcknowledgedWarningCodes) }).ToList()),
            new("Source Index", ["ObjectId", "Source", "OriginalValues"],
                run.Snapshot.Model.Frames.Select(x => new object?[] { x.ObjectId, x.SourceReference, "run.json → snapshot.model.frames; original coordinates and section units retained" })
                    .Concat(run.Snapshot.Areas.Select(x => new object?[] { x.ObjectId, x.SourceReference, "run.json → snapshot.areas; original polygon and thickness retained" })).ToList())
        };
        var warningRows = r.Warnings.Select(x => new object?[] { "Run", x.Code, x.Message })
            .Concat(r.Elements.SelectMany(e => e.Warnings.Select(w => new object?[] { e.ObjectId, w.Code, w.Message })))
            .Concat(r.Steel.SelectMany(e => e.Warnings.Select(w => new object?[] { e.ObjectId + "/" + e.Component, w.Code, w.Message }))).ToList();
        tables.Add(new("Warnings", ["Scope", "Code", "Consequence"], warningRows));
        foreach (var (name, groups) in new[] { ("By Category", r.ByCategory), ("By Material", r.ByMaterial) })
            tables.Add(new(name, ["Group", "Count", "Quantified", "KnownGrossM3", "KnownOpeningAdjustedM3", "CompleteOpeningAdjustedM3"],
                groups.Select(x => new object?[] { x.Group, x.Count, x.QuantifiedCount, x.KnownGrossM3, x.KnownOpeningAdjustedM3, x.CompleteOpeningAdjustedM3 }).ToList()));
        tables.Add(new("By Story", ["Story", "GrossM3", "OpeningAdjustedM3"],
            r.StoryAllocations.GroupBy(x => x.StoryId).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(g => new object?[] { g.Key, g.Sum(x => x.GrossM3), g.Sum(x => x.OpeningAdjustedM3) }).ToList()));
        tables.Add(new("By Section", ["Section", "Category", "Count", "KnownGrossM3", "KnownOpeningAdjustedM3", "Unquantified"],
            r.Elements.Where(x => x.Status != Core.QuantityStatus.OutOfScope).GroupBy(x => (x.SectionName, x.Category))
                .OrderBy(x => x.Key.SectionName, StringComparer.Ordinal).ThenBy(x => x.Key.Category, StringComparer.Ordinal)
                .Select(g => new object?[] { g.Key.SectionName, g.Key.Category, g.Count(), g.Sum(x => x.GrossM3 ?? 0), g.Sum(x => x.OpeningAdjustedM3 ?? 0), g.Count(x => x.OpeningAdjustedM3 is null) }).ToList()));
        tables.Add(new("Exclusions", ["ObjectId", "Status", "Reason"], r.Elements.Where(x => x.Status != Core.QuantityStatus.Quantified)
            .Select(x => new object?[] { x.ObjectId, x.Status.ToString(), string.Join("; ", x.Warnings.Select(w => w.Message)) }).ToList()));
        return tables;
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
            WriteWorkbook(Path.Combine(staging, "report.xlsx"), tables, cancellationToken);
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
                null => "", string text => SafeText(text), IFormattable value => value.ToString(null, CultureInfo.InvariantCulture), _ => SafeText(x.ToString() ?? "")
            })))).Append("\r\n");
        return builder.ToString();
    }

    public static string SafeText(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.Length > 0 && ("=+-@".Contains(trimmed[0]) || text[0] is '\t' or '\r' or '\n') ? "'" + text : text;
    }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static void WriteWorkbook(string path, IReadOnlyList<ReportTable> tables, CancellationToken token)
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
                        if (value is double number) { cell.Value = number; cell.Style.NumberFormat.Format = "0.000000"; }
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
        workbook.SaveAs(path);
    }
}
