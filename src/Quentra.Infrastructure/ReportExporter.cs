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
    public const string RoundingPolicy = "Volumes, areas and lengths to 0.001 (m³, m², m); steel to 0.1 kg; override dimensions to 0.1 mm. run.json keeps full precision.";
    private static Rounded? M3(double? value) => value is { } x ? new Rounded(x, 3) : null;
    private static Rounded? Kg(double? value) => value is { } x ? new Rounded(x, 1) : null;
    // Report timestamps in UTC to the second; run.json keeps the recorded value and offset.
    public static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
    private static object OverrideValue(Application.QuantityOverride x, double value) =>
        x.Field == Application.OverrideField.Excluded ? (value == 1 ? "Excluded" : "Included") : new Rounded(value * 1000, 1);

    // One line per sheet for the workbook's Contents sheet.
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
    {
        ["Summary"] = "Report status, model and ETABS source, hashes, methodology and headline totals",
        ["Elements"] = "Concrete per element, with the overrides that changed it",
        ["Story Allocations"] = "Each element's concrete in each story",
        ["Steel Components"] = "Each supplied reinforcement component: evidence, mass, formula, approval",
        ["Steel Coverage"] = "Required and missing steel components per element",
        ["Policy"] = "Measurement policy and calculation conventions",
        ["Overrides"] = "Hand overrides: what changed, who, when and why",
        ["Review History"] = "Acceptances and the calculation each applies to",
        ["Source Index"] = "Where each element's source data is kept",
        ["Warnings"] = "Every warning, with its scope and consequence",
        ["By Category"] = "Concrete by element category",
        ["By Material"] = "Concrete by material",
        ["By Story"] = "Concrete by story",
        ["By Section"] = "Concrete by section",
        ["Steel By Category"] = "Steel by element category",
        ["Steel By Material"] = "Steel by material",
        ["Steel By Story"] = "Steel split by each element's concrete in each story",
        ["Steel By Evidence"] = "Steel by evidence type, including required components not supplied",
        ["Exclusions"] = "Elements not quantified, and why"
    };

    // The workbook's file name carries the review status, so a draft cannot be passed on as a reviewed report by name alone.
    public static string WorkbookName(TakeoffRun run) => TakeoffJson.ReviewStatus(run) switch
    {
        "Accepted" => "report-ACCEPTED.xlsx",
        "AcceptedPartial" => "report-ACCEPTED-PARTIAL.xlsx",
        _ => "report-DRAFT.xlsx"
    };

    // Plain names for the evidence types stored in each steel row.
    public const string EvidenceTerms = "DesignDemandEquivalent = design reinforcement equivalent: required area from the design (for example ETABS concrete frame design) integrated along the member, not installed bars. " +
        "ModelAssignedEquivalent = bars assigned in the model (for example ETABS column bars to be checked, or supplied bars) as straight lengths. Estimated = kg/m³ or volume-fraction estimate. " +
        "Not supplied = required component with no source: unknown, not zero.";

    public static IReadOnlyList<ReportTable> Tables(TakeoffRun run)
    {
        var r = run.Result; var s = r.Summary;
        // "Complete gross in declared scope", or "... in scope reduced by override (excludes W1 (o1))".
        var scope = TakeoffJson.CompleteLabel(run)["complete ".Length..];
        var source = run.Snapshot.Source;
        List<object?[]> provenance = source is null ? [] :
        [
            ["Source program", $"{source.Program} {source.ProgramVersion} (build {source.ProgramBuild})", ""],
            ["ETABS support", source.ProgramBuild.StartsWith("22.7.", StringComparison.Ordinal) ? "Tested version (ETABS 22.7)" : "UNTESTED ETABS version", ""],
            ["API assembly", $"{source.ApiAssembly} ({source.ApiAssemblyVersion})", ""],
            ["Model file", source.ModelPath, ""],
            ["Model file SHA256 (last saved file)", source.ModelFileSha256 ?? "not available", ""],
            ["Model file saved at", source.ModelFileModifiedAt is { } saved ? Time(saved) : "not available", ""],
            ["Model locked (analysed) at extraction", source.ModelLocked ? "Yes" : "No", ""],
            ["ETABS present units at extraction", source.PresentUnits + " (all values converted to metres)", ""],
            ["Extracted at", Time(source.ExtractedAt), ""],
            ["Extractor", $"{source.Extractor} {source.ExtractorVersion}", ""],
            ["Raw capture SHA256", source.RawCaptureSha256, ""]
        ];
        var evidenceUsed = r.Steel.Select(x => x.Evidence).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var tables = new List<ReportTable>
        {
            new("Summary", ["Field", "Value", "Unit"],
            [
                ["REPORT STATUS", TakeoffJson.StatusBanner(run), ""],
                ["Model", run.Snapshot.Model.ModelId, ""], ["Origin", run.Snapshot.Model.Origin.ToString(), ""],
                ["Source", run.Snapshot.Model.SourceDescription, ""], ["Captured at", Time(run.Snapshot.Model.CapturedAt), ""],
                .. provenance,
                ["Quentra version", TakeoffJson.QuentraVersion, ""],
                ["Calculation version", r.CalculationVersion, ""], ["Snapshot SHA256", run.SnapshotSha256, ""],
                ["Calculation SHA256", run.CalculationSha256, ""], ["Package seal SHA256 (run content, not a file hash)", run.PackageSha256, ""],
                ["Review status", TakeoffJson.ReviewStatus(run), ""], ["Intended use", run.Snapshot.Policy.IntendedUse, ""],
                ["Basis", "Modeled volumes; intersections retained; net/BOQ unavailable", ""],
                ["Concrete methodology", "Gross modeled: section area × 3D axis length (frames), true planar area × uniform thickness (slabs, walls). Opening-adjusted: gross less the union of openings clipped to each host.", ""],
                ["Steel evidence in this run", evidenceUsed.Length == 0 ? "None supplied" : string.Join("; ", evidenceUsed), ""],
                ["Steel evidence terms", EvidenceTerms, ""],
                ["Found", s.Found, "count"], ["In scope", s.InScope, "count"], ["Quantified", s.Quantified, "count"],
                ["Unsupported", s.Unsupported, "count"], ["Invalid", s.Invalid, "count"], ["Out of scope", s.OutOfScope, "count"],
                ["Known gross", M3(s.KnownGrossM3), "m³"], ["Known opening adjusted", M3(s.KnownOpeningAdjustedM3), "m³"],
                ["Excluded by override", TakeoffJson.Exclusions(run) is { Count: > 0 } excluded ? string.Join(";", excluded) : "None", ""],
                [$"Complete gross {scope}", M3(s.CompleteGrossM3), "m³"],
                [$"Complete opening adjusted {scope}", M3(s.CompleteOpeningAdjustedM3), "m³"],
                ["Known steel", Kg(s.KnownSteelKg), "kg"], [$"Complete steel {scope}", Kg(s.CompleteSteelKg), "kg"],
                ["Elements with implausible values (included in totals)", TakeoffJson.FlaggedElements(run, TakeoffJson.PerElementCodes) is { Count: > 0 } flagged ? string.Join(";", flagged) : "None", ""],
                ["Missing steel components", s.MissingSteelComponents, "count"], ["Null quantities", "Unknown, not zero", ""],
                ["Steel basis", "Demand equivalents, model-assigned equivalents and estimates remain separate in component rows", ""],
                ["Rounding", RoundingPolicy, ""]
            ]),
            new("Elements", ["Object ID", "Category", "Material", "Section", "Status", "Gross (m³)", "Opening deduction (m³)", "Opening-adjusted (m³)", "Surface area (m²)", "Axis length (m)", "Formula", "Source", "Overridden by"],
                r.Elements.Select(x => new object?[] { x.ObjectId, x.Category, x.MaterialName, x.SectionName, x.Status.ToString(), M3(x.GrossM3), M3(x.OpeningDeductionM3), M3(x.OpeningAdjustedM3), M3(x.SurfaceAreaM2), M3(x.AxisLengthM), x.Formula, x.SourceReference,
                    string.Join(";", run.Overrides.Where(o => o.ObjectId == x.ObjectId).Select(o => o.Id)) }).ToList()),
            new("Story Allocations", ["Object ID", "Story", "Gross (m³)", "Opening-adjusted (m³)"],
                r.StoryAllocations.Select(x => new object?[] { x.ObjectId, x.StoryId, M3(x.GrossM3), M3(x.OpeningAdjustedM3) }).ToList()),
            new("Steel Components", ["Object ID", "Component", "Evidence", "Mass (kg)", "Covers", "Source", "Formula", "Assumption", "Approved by", "Approved at"],
                r.Steel.Select(x => new object?[] { x.ObjectId, x.Component, x.Evidence, Kg(x.MassKg), string.Join(";", x.CoversComponents), x.SourceReference, x.Formula, x.Assumption, x.ApprovedBy, Time(x.ApprovedAt) }).ToList()),
            new("Steel Coverage", ["Object ID", "Required", "Missing", "Known mass (kg)", "Complete mass (kg)"],
                r.SteelCoverage.Select(x => new object?[] { x.ObjectId, x.Required.IsEmpty ? "None (declared)" : string.Join(";", x.Required), string.Join(";", x.Missing), Kg(x.KnownMassKg), Kg(x.CompleteMassKg) }).ToList()),
            new("Policy", ["Field", "Value"],
            [
                ["Policy ID", run.Snapshot.Policy.Id], ["Intended use", run.Snapshot.Policy.IntendedUse],
                ["Approved by", run.Snapshot.Policy.ApprovedBy], ["Approved at", run.Snapshot.Policy.ApprovedAt is { } approved ? Time(approved) : null],
                ["Steel density (kg/m³)", run.Snapshot.Policy.SteelDensityKgM3], ["Linear tolerance (m)", run.Snapshot.Policy.LinearToleranceM],
                ["Planarity tolerance (m)", run.Snapshot.Policy.PlanarityToleranceM], ["Polygon clipping precision", "1 micrometre in local metres"],
                ["Longitudinal integration", "Interval endpoint maxima; no automatic extrapolation"],
                ["Story allocation", "Columns, sloped beams and walls split in proportion to their length or area within each story band; a level beam goes to the story whose top it sits on (lower < z ≤ upper) unless assignedStoryId is given; slabs use assignedStoryId"],
                ["Steel by story", "Each element's known steel split in proportion to its opening-adjusted volume in each story"],
                ["Default ratios", "None"], ["Laps, anchorage, waste", "Not inferred; only explicitly supplied components are reported"],
                ["Rounding", RoundingPolicy]
            ]),
            new("Overrides", ["ID", "Object ID", "Field", "Original value", "Replacement value", "Unit", "Reason", "Author", "Recorded at", "Snapshot SHA256"],
                run.Overrides.Select(x => new object?[] { x.Id, x.ObjectId, Application.Overrides.Label(x.Field), OverrideValue(x, x.OriginalValue), OverrideValue(x, x.ReplacementValue),
                    x.Field == Application.OverrideField.Excluded ? "" : "mm", x.Reason, x.Author, Time(x.RecordedAt), x.SnapshotSha256 }).ToList()),
            // Identity columns are what the reviewer typed and what the computer reported: recorded, not verified or signed.
            new("Review History", ["Status", "Reviewer (as entered)", "Reviewed at", "Calculation SHA256", "Current", "Note", "Acknowledged warnings",
                    "Windows account (recorded, not verified)", "Computer", "Quentra version"],
                run.ReviewHistory.Select(x => new object?[] { x.Status, x.Reviewer, Time(x.ReviewedAt), x.CalculationSha256, x.CalculationSha256 == run.CalculationSha256 ? "Yes" : "No", x.Note, string.Join(";", x.AcknowledgedWarningCodes),
                    x.RecordedByAccount ?? "not recorded", x.RecordedOnComputer ?? "not recorded", x.RecordedWithVersion ?? "not recorded" }).ToList()),
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
        // Bottom story first, as the building is read; Unallocated last.
        var storyOrder = run.Snapshot.Stories.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        tables.Add(new("By Story", ["Story", "Gross (m³)", "Opening-adjusted (m³)"],
            r.StoryAllocations.GroupBy(x => x.StoryId).OrderBy(x => storyOrder.GetValueOrDefault(x.Key, int.MaxValue)).ThenBy(x => x.Key, StringComparer.Ordinal)
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
        // Required components with no reinforcement entry at all are unknown too; count them in their own row.
        var supplied = r.Steel.SelectMany(x => x.CoversComponents.Add(x.Component).Select(c => (x.ObjectId, c))).ToHashSet();
        var notSupplied = r.SteelCoverage.Sum(x => x.Missing.Count(c => !supplied.Contains((x.ObjectId, c))));
        var evidence = r.Steel.Where(x => elements[x.ObjectId].Status != Core.QuantityStatus.OutOfScope).GroupBy(x => x.Evidence).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new object?[] { g.Key, g.Count(), Kg(g.Sum(x => x.MassKg ?? 0)), g.Count(x => x.MassKg is null) }).ToList();
        if (notSupplied > 0) evidence.Add(["Not supplied", notSupplied, null, notSupplied]);
        yield return new("Steel By Evidence", ["Evidence", "Components", "Known steel (kg)", "Unknown components"], evidence);
    }

    public static async Task ExportAsync(TakeoffRun run, string newDirectory, CancellationToken cancellationToken = default)
    {
        // Verify the run before making a report projection.
        run = TakeoffJson.Replay(TakeoffJson.Serialize(run), cancellationToken);
        var destination = Path.GetFullPath(newDirectory);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Export destination must not exist.");
        var parent = Path.GetDirectoryName(destination)!;
        try { Directory.CreateDirectory(parent); }
        catch (IOException e) { throw SnapshotJson.FolderFailed(parent, e); }
        var staging = Path.Combine(parent, ".quentra-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            var tables = Tables(run);
            await TakeoffJson.WriteAsync(Path.Combine(staging, "run.json"), run, cancellationToken);
            foreach (var table in tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await File.WriteAllTextAsync(Path.Combine(staging, table.Name.Replace(' ', '_') + ".csv"), Csv(table), new UTF8Encoding(true), cancellationToken);
            }
            WriteWorkbook(Path.Combine(staging, WorkbookName(run)), tables, run.Snapshot.Model.CapturedAt, TakeoffJson.StatusBanner(run), cancellationToken);
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
        catch (IOException e) { throw SnapshotJson.WriteFailed(destination, e); }
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

    private static void WriteWorkbook(string path, IReadOnlyList<ReportTable> tables, DateTimeOffset created, string banner, CancellationToken token)
    {
        using var workbook = new XLWorkbook();
        var contents = workbook.AddWorksheet("Contents");
        // The status banner is the first thing anyone opening the workbook reads.
        contents.Cell(1, 1).Value = banner;
        contents.Range(1, 1, 1, 3).Merge().Style.Font.SetBold().Font.SetFontSize(14)
            .Fill.SetBackgroundColor(banner.StartsWith("DRAFT", StringComparison.Ordinal) ? XLColor.LightSalmon : XLColor.LightGreen);
        const int header = 3;
        contents.Cell(header, 1).Value = "Sheet"; contents.Cell(header, 2).Value = "Contents"; contents.Cell(header, 3).Value = "Rows";
        for (var i = 0; i < tables.Count; i++)
        {
            contents.Cell(header + i + 1, 1).Value = tables[i].Name;
            contents.Cell(header + i + 1, 2).Value = Descriptions.GetValueOrDefault(tables[i].Name, "");
            contents.Cell(header + i + 1, 3).Value = tables[i].Rows.Count;
        }
        contents.Cell(header + tables.Count + 2, 1).Value = "Null cells are unknown, not zero. " + RoundingPolicy;
        contents.Row(header).Style.Font.Bold = true;
        contents.Column(1).Width = 22; contents.Column(2).Width = 70; contents.Column(3).Width = 10;
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
                sheet.SheetView.FreezeRows(1);
                SizeColumns(sheet, table.Headers, chunks[part]);
                sheet.Range(1, 1, Math.Max(1, chunks[part].Length + 1), table.Headers.Length).SetAutoFilter();
            }
        }
        using var buffer = new MemoryStream();
        workbook.SaveAs(buffer);
        WriteReproducible(buffer, path, created);
    }

    // Width from the header and the first 500 rows, so large sheets stay fast; long text is capped and wraps in the cell.
    private static void SizeColumns(IXLWorksheet sheet, string[] headers, object?[][] rows)
    {
        for (var c = 0; c < headers.Length; c++)
        {
            var widest = rows.Take(500).Select(row => row[c] switch { null => 0, Rounded q => q.ToString().Length + 2, var v => v.ToString()!.Length })
                .Append(headers[c].Length + 3).Max();
            sheet.Column(c + 1).Width = Math.Clamp(widest + 2, 10, 60);
        }
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
