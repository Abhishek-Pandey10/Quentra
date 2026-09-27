#if ETABS_API
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ETABSv1;
using Quentra.Application;
using Quentra.Core;
using Quentra.Etabs;
using Quentra.Etabs.Api;
using Quentra.EtabsFixtures;
using Quentra.Infrastructure;

// quentra-etabs-fixtures generate <fixtures/etabs/v22.7>   builds the controlled models and raw captures
// quentra-etabs-fixtures verify   <fixtures/etabs/v22.7>   ETABS integration suite: re-extracts every model and checks it
const string Etabs22 = @"C:\Program Files\Computers and Structures\ETABS 22\ETABS.exe";
if (args is ["perf", var storiesText, var baysText, var outDir])
{
    LiveEtabs.LoadApi(Path.GetDirectoryName(Etabs22)!);
    return await Fixtures.Performance(int.Parse(storiesText, CultureInfo.InvariantCulture), int.Parse(baysText, CultureInfo.InvariantCulture), Path.GetFullPath(outDir), Etabs22);
}
if (args is ["profile", var modelPath])
{
    LiveEtabs.LoadApi(Path.GetDirectoryName(Etabs22)!);
    return Fixtures.Profile(Path.GetFullPath(modelPath), Etabs22);
}
if (args is ["tables", var pidText, .. var tableKeys])
{
    LiveEtabs.LoadApi(Path.GetDirectoryName(Etabs22)!);
    return Fixtures.Tables(int.Parse(pidText, CultureInfo.InvariantCulture), tableKeys);
}
if (args is not [("generate" or "verify" or "experiment-wall" or "experiment-design" or "experiment-blank") and var mode, var dir])
{
    Console.Error.WriteLine("Usage: Quentra.EtabsFixtures generate|verify|experiment-wall <fixtures/etabs/v22.7>");
    return 2;
}
LiveEtabs.LoadApi(Path.GetDirectoryName(Etabs22)!);
return mode switch
{
    "generate" => Fixtures.Generate(Path.GetFullPath(dir), Etabs22),
    "verify" => Fixtures.Verify(Path.GetFullPath(dir), Etabs22),
    "experiment-design" => Fixtures.DesignExperiment(Etabs22),
    "experiment-blank" => Fixtures.BlankExperiment(Path.GetFullPath(dir), Etabs22),
    _ => Fixtures.WallExperiment(Path.GetFullPath(dir), Etabs22)
};

static class Fixtures
{
    public static readonly (string Name, eUnits Units)[] UnitSystems =
        [("kN-m", eUnits.kN_m_C), ("N-mm", eUnits.N_mm_C), ("kgf-m", eUnits.kgf_m_C), ("kip-ft", eUnits.kip_ft_F), ("lb-in", eUnits.lb_in_F)];

    private sealed class Owned : IDisposable
    {
        public cOAPI Api { get; }
        public EtabsInstance Instance { get; }
        public cSapModel Model => Api.SapModel;
        public Owned(string exe)
        {
            var before = Process.GetProcessesByName("ETABS").Select(p => p.Id).ToHashSet();
            Api = ((cHelper)new Helper()).CreateObject(exe);
            Models.Check(Api.ApplicationStart(), "ETABS start");
            var pid = Process.GetProcessesByName("ETABS").Select(p => p.Id).Single(id => !before.Contains(id));
            Instance = new(pid, exe, FileVersionInfo.GetVersionInfo(exe).FileVersion, "fixture");
            Api.Hide();
        }
        public void Dispose()
        {
            try { Api.ApplicationExit(false); } catch (Exception) { /* already closed */ }
        }
    }

    private static void NewModel(cSapModel m, Models.Spec spec)
    {
        Models.Check(m.InitializeNewModel(eUnits.kN_m_C), "new model");
        Models.Check(m.File.NewGridOnly(spec.Stories, spec.TypicalHeight, spec.BottomHeight, 2, 2, 6, 6), "grid");
        Models.Properties(m);
        spec.Build(m);
    }

    private static EtabsRawModel Extract(EtabsInstance instance)
    {
        using var connection = LiveEtabs.Connect(instance);
        return EtabsExtractor.Extract(connection.Reader, DateTimeOffset.UtcNow).Raw;
    }

    private static (int FrameObjects, int AreaObjects, int LineElements, int AreaElements) Counts(cSapModel m) =>
        (m.FrameObj.Count(), m.AreaObj.Count(), m.LineElm.Count(), m.AreaElm.Count());

    public static int Generate(string dir, string exe)
    {
        Directory.CreateDirectory(Path.Combine(dir, "models"));
        using var etabs = new Owned(exe);
        var m = etabs.Model;
        var notes = new StringBuilder();
        foreach (var spec in Models.All)
        {
            Console.WriteLine($"Building {spec.Name}: {spec.Description}");
            NewModel(m, spec);
            var path = Path.Combine(dir, "models", spec.Name + ".edb");
            Models.Check(m.File.Save(path), "save " + spec.Name);
            if (spec.Design)
            {
                // Verified: the blank template's default code (Chinese 2010) gives no results here, and Save(path) discards
                // analysis and design results even for the same path; Save() with no name keeps them.
                Models.Check(m.Analyze.RunAnalysis(), "analysis");
                Models.Check(m.DesignConcrete.SetCode("ACI 318-19"), "design code");
                Models.Check(m.DesignConcrete.StartDesign(), "concrete design");
                Models.Check(m.File.Save(), "save designed " + spec.Name);
            }
            Write(dir, spec.Name, Extract(etabs.Instance));
            if (spec.Name == "08-one-story-frame")
                foreach (var (name, units) in UnitSystems)
                {
                    Models.Check(m.SetPresentUnits(units), "units " + name);
                    Write(dir, "11-units." + name, Extract(etabs.Instance));
                }
            if (spec.Name == "10-meshed")
            {
                // Not saved afterwards: creating the analysis model rewrites walls that contain openings (see fixtures/etabs/README.md).
                var before = Counts(m);
                Models.Check(m.Analyze.CreateAnalysisModel(), "analysis model");
                var after = Counts(m);
                Write(dir, spec.Name + ".after-mesh", Extract(etabs.Instance));
                notes.AppendLine($"10-meshed (frame objects, area objects, line elements, area elements) before meshing: {before}; after CreateAnalysisModel: {after}");
                Console.WriteLine(notes);
                continue;
            }
            if (spec.Name == "08-one-story-frame")
            {
                Models.Check(m.SetPresentUnits(eUnits.kN_m_C), "restore units");
                Models.Check(m.File.Save(path), "save " + spec.Name);
            }
        }
        File.WriteAllText(Path.Combine(dir, "mesh-counts.txt"), notes.ToString());
        return 0;
    }

    // Does creating the analysis model change the wall objects permanently? Counts area objects and opening flags at each step.
    public static int WallExperiment(string dir, string exe)
    {
        using var etabs = new Owned(exe);
        var m = etabs.Model;
        var spec = Models.All.Single(x => x.Name == "07-wall-opening");
        NewModel(m, spec);
        var path = Path.Combine(Path.GetTempPath(), "quentra-wall-experiment.edb");
        Models.Check(m.File.Save(path), "save");
        void Report(string step)
        {
            var raw = Extract(etabs.Instance);
            var a = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(raw, new()).Snapshot).Result.Summary;
            Console.WriteLine($"{step,-40} locked={m.GetModelIsLocked(),-5} areaObj={m.AreaObj.Count(),2} areaElm={m.AreaElm.Count(),3} walls={raw.Areas.Count(x => !x.IsOpening),2} openings={raw.Areas.Count(x => x.IsOpening)} " +
                $"labels={string.Join(",", raw.Areas.Select(x => x.Label))} gross={a.KnownGrossM3:F4} adjusted={a.KnownOpeningAdjustedM3:F4}");
        }
        Report("as drawn");
        Models.Check(m.Analyze.CreateAnalysisModel(), "analysis model"); Report("after CreateAnalysisModel");
        Models.Check(m.SetModelIsLocked(false), "unlock"); Report("after unlock");
        Models.Check(m.File.Save(path), "save"); Models.Check(m.File.OpenFile(path), "reopen"); Report("after save and reopen");
        Models.Check(m.Analyze.RunAnalysis(), "analysis"); Report("after RunAnalysis");
        Models.Check(m.SetModelIsLocked(false), "unlock"); Report("after unlock");
        return 0;
    }

    // Builds a synthetic tower (stories x bays x bays of 6 m bays: columns, two-way beams, slabs and a 4-wall core per story)
    // in its own ETABS, then times each Quentra stage on it. Run once per size so peak memory is per size.
    public static async Task<int> Performance(int stories, int bays, string outDir, string exe)
    {
        Directory.CreateDirectory(outDir);
        using var etabs = new Owned(exe);
        var m = etabs.Model;
        var clock = Stopwatch.StartNew();
        var spec = new Models.Spec("perf", "", stories, 3.5, 3.2, _ => { });
        NewModel(m, spec);
        string name = "";
        m.View.RefreshView(0, false);
        for (var s = 0; s < stories; s++)
        {
            var bottom = s == 0 ? 0 : 3.5 + (s - 1) * 3.2; var top = 3.5 + s * 3.2;
            for (var i = 0; i <= bays; i++)
                for (var j = 0; j <= bays; j++)
                {
                    Models.Check(m.FrameObj.AddByCoord(i * 6, j * 6, bottom, i * 6, j * 6, top, ref name, "C400x400"), "column");
                    if (i < bays) Models.Check(m.FrameObj.AddByCoord(i * 6, j * 6, top, (i + 1) * 6, j * 6, top, ref name, "B300x500"), "beam x");
                    if (j < bays) Models.Check(m.FrameObj.AddByCoord(i * 6, j * 6, top, i * 6, (j + 1) * 6, top, ref name, "B300x500"), "beam y");
                    if (i < bays && j < bays)
                    {
                        double[] x = [i * 6, i * 6 + 6, i * 6 + 6, i * 6], y = [j * 6, j * 6, j * 6 + 6, j * 6 + 6], z = [top, top, top, top];
                        Models.Check(m.AreaObj.AddByCoord(4, ref x, ref y, ref z, ref name, "S200"), "slab");
                    }
                }
            foreach (var (x1, y1, x2, y2) in new[] { (0.0, 0.0, 6.0, 0.0), (6.0, 0.0, 6.0, 6.0), (6.0, 6.0, 0.0, 6.0), (0.0, 6.0, 0.0, 0.0) })
            {
                double[] x = [x1, x2, x2, x1], y = [y1, y2, y2, y1], z = [bottom, bottom, top, top];
                Models.Check(m.AreaObj.AddByCoord(4, ref x, ref y, ref z, ref name, "W250"), "wall");
            }
        }
        var path = Path.Combine(outDir, $"tower-{stories}x{bays}.edb");
        Models.Check(m.File.Save(path), "save");
        var buildSeconds = clock.Elapsed.TotalSeconds;
        var objects = m.FrameObj.Count() + m.AreaObj.Count();

        var before = GC.GetTotalMemory(true);
        clock.Restart();
        EtabsRawModel raw; IReadOnlyList<StageTiming> stages;
        using (var connection = LiveEtabs.Connect(etabs.Instance)) (raw, stages) = EtabsExtractor.Extract(connection.Reader, DateTimeOffset.UtcNow);
        var extract = clock.Elapsed.TotalSeconds; clock.Restart();
        var built = EtabsSnapshotBuilder.Run(raw, new());
        var normalise = clock.Elapsed.TotalSeconds; clock.Restart();
        var health = ModelHealth.Check(built.Snapshot);
        var validate = clock.Elapsed.TotalSeconds; clock.Restart();
        var run = TakeoffJson.Calculate(built.Snapshot);
        var calculate = clock.Elapsed.TotalSeconds; clock.Restart();
        var snapshotPath = Path.Combine(outDir, $"tower-{stories}x{bays}.snapshot.json");
        if (File.Exists(snapshotPath)) File.Delete(snapshotPath);
        await TakeoffJson.WriteAsync(snapshotPath, TakeoffJson.Canonical(built.Snapshot));
        var report = Path.Combine(outDir, $"report-{stories}x{bays}-{Guid.NewGuid():N}");
        await ReportExporter.ExportAsync(run, report);
        var export = clock.Elapsed.TotalSeconds;
        var peak = Process.GetCurrentProcess().PeakWorkingSet64;
        var etabsMemory = Process.GetProcessById(etabs.Instance.ProcessId).WorkingSet64;
        var line = string.Create(CultureInfo.InvariantCulture,
            $"| {stories} x {bays}x{bays} | {objects:N0} | {extract:F1} | {normalise:F1} | {validate:F1} | {calculate:F1} | {export:F1} | {peak / 1048576.0:N0} MB | {new FileInfo(snapshotPath).Length / 1048576.0:F1} MB | {run.Result.Summary.KnownGrossM3:F3} | {buildSeconds:F0} |");
        Console.WriteLine("| Model | Objects | Extract s | Normalise s | Validate s | Calculate s | Export s | Quentra peak | Snapshot | Gross m³ | Build s |");
        Console.WriteLine(line);
        Console.WriteLine("Extraction stages: " + string.Join(", ", stages.Select(x => $"{x.Stage} {x.Milliseconds} ms")));
        Console.WriteLine($"Health: {health.Status}; ETABS working set {etabsMemory / 1048576.0:N0} MB; memory before extraction {before / 1048576.0:N0} MB");
        File.AppendAllText(Path.Combine(outDir, "performance.md"), line + "\n");
        return 0;
    }

    // Times each per-object API method over every frame and area of a model, to find the ones whose cost grows with model size.
    public static int Profile(string modelPath, string exe)
    {
        using var etabs = new Owned(exe);
        var m = etabs.Model;
        Open(m, modelPath);
        int n = 0; string[]? frames = null, areas = null, labels = null, stories = null;
        m.FrameObj.GetNameList(ref n, ref frames);
        m.AreaObj.GetNameList(ref n, ref areas);
        void Time(string what, string[] names, Action<string> call)
        {
            var clock = Stopwatch.StartNew();
            foreach (var name in names) call(name);
            Console.WriteLine($"{what,-40} {names.Length,7} objects {clock.ElapsedMilliseconds,7} ms  {clock.Elapsed.TotalMilliseconds * 1000 / names.Length,8:F1} us each");
        }
        string s1 = "", s2 = ""; int i1 = 0; bool b1 = false; double d1 = 0; double[]? a1 = null, a2 = null, a3 = null; int[]? ia = null, ib = null;
        eFrameDesignOrientation fo = 0;
        Time("FrameObj.GetLabelFromName", frames!, x => m.FrameObj.GetLabelFromName(x, ref s1, ref s2));
        Time("FrameObj.GetGUID", frames!, x => m.FrameObj.GetGUID(x, ref s1));
        Time("FrameObj.GetDesignOrientation", frames!, x => m.FrameObj.GetDesignOrientation(x, ref fo));
        Time("FrameObj.GetDesignProcedure", frames!, x => m.FrameObj.GetDesignProcedure(x, ref i1));
        Time("FrameObj.GetMaterialOverwrite", frames!, x => m.FrameObj.GetMaterialOverwrite(x, ref s1));
        Time("FrameObj.GetCurved_2", frames!, x => m.FrameObj.GetCurved_2(x, ref i1, ref d1, ref n, ref a1, ref a2, ref a3));
        Time("AreaObj.GetLabelFromName", areas!, x => m.AreaObj.GetLabelFromName(x, ref s1, ref s2));
        Time("AreaObj.GetGUID", areas!, x => m.AreaObj.GetGUID(x, ref s1));
        Time("AreaObj.GetProperty", areas!, x => m.AreaObj.GetProperty(x, ref s1));
        Time("AreaObj.GetOpening", areas!, x => m.AreaObj.GetOpening(x, ref b1));
        Time("AreaObj.GetMaterialOverwrite", areas!, x => m.AreaObj.GetMaterialOverwrite(x, ref s1));
        Time("AreaObj.GetCurvedEdges", areas!, x => m.AreaObj.GetCurvedEdges(x, ref i1, ref ia, ref a1, ref ib, ref a2, ref a3, ref a3));
        var clock = Stopwatch.StartNew();
        m.FrameObj.GetLabelNameList(ref n, ref frames, ref labels, ref stories);
        Console.WriteLine($"FrameObj.GetLabelNameList (all at once)   {n,7} objects {clock.ElapsedMilliseconds,7} ms");
        clock.Restart();
        m.AreaObj.GetLabelNameList(ref n, ref areas, ref labels, ref stories);
        Console.WriteLine($"AreaObj.GetLabelNameList (all at once)    {n,7} objects {clock.ElapsedMilliseconds,7} ms");
        return 0;
    }

    // READ-ONLY: attaches to a running ETABS by process id and prints database tables (lists them when no key is given).
    // Used to reconcile Quentra with ETABS's own material takeoff on a real model.
    public static int Tables(int pid, string[] keys)
    {
        var api = ((cHelper)new Helper()).GetObjectProcess("CSI.ETABS.API.ETABSObject", pid);
        var db = api.SapModel.DatabaseTables;
        if (keys.Length == 0)
        {
            int n = 0; string[]? tableKeys = null, names = null; int[]? importType = null; bool[]? empty = null;
            Models.Check(db.GetAllTables(ref n, ref tableKeys, ref names, ref importType, ref empty), "list tables");
            for (var i = 0; i < n; i++)
                if (!empty![i] && (tableKeys![i].Contains("Material", StringComparison.OrdinalIgnoreCase) || tableKeys[i].Contains("Quantit", StringComparison.OrdinalIgnoreCase) ||
                    tableKeys[i].Contains("Concrete", StringComparison.OrdinalIgnoreCase) || tableKeys[i].Contains("Connectivity", StringComparison.OrdinalIgnoreCase) ||
                    tableKeys[i].Contains("Assignments - Summary", StringComparison.OrdinalIgnoreCase)))
                {
                    int f = 0, version = 0; string[]? keysOut = null, names2 = null, descriptions = null, units = null; bool[]? importable = null;
                    db.GetAllFieldsInTable(tableKeys[i], ref version, ref f, ref keysOut, ref names2, ref descriptions, ref units, ref importable);
                    Console.WriteLine($"{tableKeys[i]}: {string.Join(", ", keysOut ?? [])}");
                }
            return 0;
        }
        foreach (var key in keys)
        {
            string[]? fields = [""]; int version = 0, records = 0; string[]? included = null, data = null;
            var ret = db.GetTableForDisplayArray(key, ref fields, "All", ref version, ref included, ref records, ref data);
            Console.WriteLine($"== {key} (ret {ret}, version {version}, {records} records)");
            if (ret != 0 || included is null) continue;
            Console.WriteLine(string.Join(" | ", included));
            for (var r = 0; r < records; r++) Console.WriteLine(string.Join(" | ", data!.Skip(r * included.Length).Take(included.Length)));
        }
        return 0;
    }

    // What does ETABS report as the model file when no model has been opened, or after a new blank model replaces one?
    public static int BlankExperiment(string dir, string exe)
    {
        using var etabs = new Owned(exe);
        var m = etabs.Model;
        void Report(string step) => Console.WriteLine($"{step,-36} filename='{m.GetModelFilename(true)}' path='{m.GetModelFilepath()}' frames={m.FrameObj.Count()}");
        Report("fresh ETABS");
        Models.Check(m.InitializeNewModel(eUnits.kN_m_C), "init"); Report("InitializeNewModel");
        Models.Check(m.File.NewBlank(), "blank"); Report("NewBlank");
        Models.Check(m.File.OpenFile(Path.Combine(dir, "models", "01-single-beam.edb")), "open"); Report("opened 01-single-beam");
        Models.Check(m.InitializeNewModel(eUnits.kN_m_C), "init"); Report("InitializeNewModel after open");
        Models.Check(m.File.NewBlank(), "blank"); Report("NewBlank after open");
        return 0;
    }

    // Where do concrete design results appear and survive? Prints lock state and result availability after each step.
    public static int DesignExperiment(string exe)
    {
        using var etabs = new Owned(exe);
        var m = etabs.Model;
        NewModel(m, Models.All.Single(x => x.Design));
        var path = Path.Combine(Path.GetTempPath(), "quentra-design-experiment", "design.edb");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string code = "";
        void Report(string step, int ret)
        {
            m.DesignConcrete.GetCode(ref code);
            int n = 0, cases = 0; string[]? names = null; int[]? status = null;
            m.Analyze.GetCaseStatus(ref cases, ref names, ref status);
            Console.WriteLine($"{step,-34} ret={ret,-3} locked={m.GetModelIsLocked(),-5} designResults={m.DesignConcrete.GetResultsAvailable(),-5} code='{code}' cases={string.Join(",", Enumerable.Range(0, cases).Select(i => $"{names![i]}:{status![i]}"))}");
            _ = n;
        }
        Report("built", 0);
        Report("save", m.File.Save(path));
        Report("RunAnalysis", m.Analyze.RunAnalysis());
        Report("SetCode ACI 318-19", m.DesignConcrete.SetCode("ACI 318-19"));
        Report("StartDesign", m.DesignConcrete.StartDesign());
        Report("save (no file name)", m.File.Save());
        Report("reopen", m.File.OpenFile(path));
        Report("RunAnalysis", m.Analyze.RunAnalysis());
        Report("StartDesign", m.DesignConcrete.StartDesign());
        Report("save (same file name)", m.File.Save(path));
        Report("reopen", m.File.OpenFile(path));
        return 0;
    }

    private static void Write(string dir, string name, EtabsRawModel raw) =>
        File.WriteAllText(Path.Combine(dir, name + ".etabs-raw.json"), EtabsRawJson.Serialize(raw), new UTF8Encoding(false));

    // ---------------- integration suite ----------------
    private static readonly List<(string Test, bool Passed, string Detail)> Results = [];
    private static DateTime lastProgress = DateTime.UtcNow;
    private static void Record(string test, bool passed, string detail)
    {
        lastProgress = DateTime.UtcNow;
        Results.Add((test, passed, detail));
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {test}: {detail}");
    }

    // A hidden ETABS can wait on a dialog nobody sees. If nothing progresses for three minutes, end that (test-owned)
    // ETABS so the blocked call fails and the suite reports it, instead of hanging.
    private static void Watch(int pid) => _ = Task.Run(async () =>
    {
        while (true)
        {
            await Task.Delay(10_000);
            if (DateTime.UtcNow - lastProgress < TimeSpan.FromMinutes(3)) continue;
            Console.WriteLine($"WATCHDOG  no progress for 3 minutes; ending test-owned ETABS process {pid}");
            try { Process.GetProcessById(pid).Kill(); } catch (ArgumentException) { }
            return;
        }
    });

    // Opening a model while the current one has unsaved changes (e.g. changed present units) makes ETABS ask to save,
    // which blocks a hidden ETABS. Discard the current model first.
    private static void Open(cSapModel m, string path)
    {
        Models.Check(m.InitializeNewModel(eUnits.kN_m_C), "discard current model");
        Models.Check(m.File.OpenFile(path), "open " + Path.GetFileName(path));
        lastProgress = DateTime.UtcNow;
    }

    public static int Verify(string dir, string exe)
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "golden.json"))).RootElement;
        var tolerance = golden.GetProperty("tolerance").GetDouble();
        var clock = Stopwatch.StartNew();
        using (var etabs = new Owned(exe))
        {
            var m = etabs.Model;
            Watch(etabs.Instance.ProcessId);
            foreach (var spec in Models.All)
            {
                var path = Path.Combine(dir, "models", spec.Name + ".edb");
                Open(m, path);
                var hashBefore = Hash(path);
                var lockedBefore = m.GetModelIsLocked();
                var unitsBefore = m.GetPresentUnits();
                var raw = Extract(etabs.Instance);
                Record($"{spec.Name} read-only", hashBefore == Hash(path) && lockedBefore == m.GetModelIsLocked() && unitsBefore == m.GetPresentUnits(),
                    "model file, lock state and present units unchanged by extraction");
                Check(spec.Name, golden.GetProperty("models").GetProperty(spec.Name), raw, tolerance);
                if (spec.Name == "08-one-story-frame") Units(etabs, golden.GetProperty("models").GetProperty("11-units"), tolerance);
                if (spec.Name == "10-meshed") Mesh(etabs, raw, tolerance);
            }
            NoModel(etabs);
        }
        ClosedDuringExtraction(exe);
        var failed = Results.Count(x => !x.Passed);
        Console.WriteLine($"\n{Results.Count - failed} passed, {failed} failed in {clock.Elapsed.TotalSeconds:F1} s");
        var report = new StringBuilder($"# ETABS integration results\n\nETABS {FileVersionInfo.GetVersionInfo(exe).FileVersion}; API {LiveEtabs.LoadedApiAssembly}; run {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC; {Results.Count - failed} passed, {failed} failed.\n\n| Result | Test | Detail |\n|---|---|---|\n");
        foreach (var (test, passed, detail) in Results) report.Append($"| {(passed ? "PASS" : "**FAIL**")} | {test} | {detail.Replace("|", "\\|")} |\n");
        File.WriteAllText(Path.Combine(dir, "integration-results.md"), report.ToString());
        return failed == 0 ? 0 : 1;
    }

    private static string Hash(string path)
    {
        using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(s));
    }

    public static void Check(string name, JsonElement expected, EtabsRawModel raw, double tolerance)
    {
        var snapshot = EtabsSnapshotBuilder.Run(raw, GoldenCheck.Options(expected)).Snapshot;
        var run = TakeoffJson.Calculate(snapshot);
        foreach (var (test, ok, detail) in GoldenCheck.Compare(expected, run, tolerance)) Record($"{name} {test}", ok, detail);
    }

    private static void Units(IDisposable owner, JsonElement expected, double tolerance)
    {
        var etabs = (Owned)owner;
        TakeoffResult? baseline = null;
        foreach (var (name, units) in UnitSystems)
        {
            Models.Check(etabs.Model.SetPresentUnits(units), "units " + name);
            var raw = Extract(etabs.Instance);
            Check("11-units." + name, expected, raw, tolerance);
            var result = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(raw, new()).Snapshot).Result;
            baseline ??= result;
            var worst = result.Elements.Zip(baseline.Elements).Max(p => Math.Abs((p.First.GrossM3 ?? 0) - (p.Second.GrossM3 ?? 0)) / Math.Max(1e-12, p.Second.GrossM3 ?? 1));
            Record($"11-units.{name} matches kN-m per element", worst <= tolerance && result.Elements.Select(x => x.ObjectId).SequenceEqual(baseline.Elements.Select(x => x.ObjectId)),
                $"largest relative difference from kN-m {worst:E2}");
        }
        Models.Check(etabs.Model.SetPresentUnits(eUnits.kN_m_C), "restore units");
    }

    private static void Mesh(IDisposable owner, EtabsRawModel before, double tolerance)
    {
        var etabs = (Owned)owner;
        var objects = (etabs.Model.FrameObj.Count(), etabs.Model.AreaObj.Count());
        Models.Check(etabs.Model.Analyze.CreateAnalysisModel(), "analysis model");
        var elements = (etabs.Model.LineElm.Count(), etabs.Model.AreaElm.Count());
        var after = Extract(etabs.Instance);
        var a = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(before, new()).Snapshot).Result;
        var b = TakeoffJson.Calculate(EtabsSnapshotBuilder.Run(after, new()).Snapshot).Result;
        Record("10-meshed analysis mesh is finer than the objects", elements.Item1 > objects.Item1 && elements.Item2 > objects.Item2,
            $"frame objects {objects.Item1} -> line elements {elements.Item1}; area objects {objects.Item2} -> area elements {elements.Item2}");
        foreach (var (test, ok, detail) in GoldenCheck.MeshInvariance(a, b, tolerance)) Record("10-meshed " + test, ok, detail);
    }

    private static void NoModel(IDisposable owner)
    {
        var etabs = (Owned)owner;
        Models.Check(etabs.Model.InitializeNewModel(eUnits.kN_m_C), "new model");
        Models.Check(etabs.Model.File.NewBlank(), "blank");
        try { Extract(etabs.Instance); Record("ETABS with no saved model", false, "extraction did not refuse"); }
        catch (EtabsUnavailableException e) { Record("ETABS with no saved model", e.Message.Contains("no saved model", StringComparison.Ordinal), e.Message); }
    }

    // Starts a separate ETABS, opens the largest fixture, and ends the process while extraction is running.
    private static void ClosedDuringExtraction(string exe)
    {
        var etabs = new Owned(exe);
        Watch(etabs.Instance.ProcessId);
        Open(etabs.Model, Path.GetFullPath(Path.Combine("fixtures", "etabs", "v22.7", "models", "09-multi-story.edb")));
        using var connection = LiveEtabs.Connect(etabs.Instance);
        var killer = Task.Run(async () => { await Task.Delay(30); Process.GetProcessById(etabs.Instance.ProcessId).Kill(); });
        try
        {
            for (var i = 0; i < 200; i++) EtabsExtractor.Extract(connection.Reader, DateTimeOffset.UtcNow);
            Record("ETABS closed during extraction", false, "extraction kept succeeding after ETABS was ended");
        }
        catch (EtabsUnavailableException e) { Record("ETABS closed during extraction", e.Message.Contains("closed or stopped responding", StringComparison.Ordinal), e.Message); }
        killer.Wait();
    }
}
#else
Console.Error.WriteLine("This tool needs ETABS 22.7 installed at build time.");
return 1;
#endif
