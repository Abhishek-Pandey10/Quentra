#if ETABS_API
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ETABSv1;

namespace Quentra.Etabs.Api;

// A connection to one ETABS process. Every call is a read (Get*); Quentra never calls a Set*, Save, Analyze or
// design method on a model it attached to. Only a connection that launched its own ETABS closes it.
internal sealed class LiveConnection : IEtabsConnection
{
    private cOAPI? api;
    private readonly bool owned;

    private LiveConnection(cOAPI api, EtabsInstance instance, bool owned)
    {
        this.api = api; this.owned = owned; Instance = instance;
        Reader = new LiveReader(api.SapModel, instance);
    }

    public EtabsInstance Instance { get; }
    public IEtabsModelReader Reader { get; }
    public cSapModel Model => api?.SapModel ?? throw new ObjectDisposedException(nameof(LiveConnection));

    public static LiveConnection Attach(EtabsInstance instance)
    {
        cOAPI? api;
        try { api = ((cHelper)new Helper()).GetObjectProcess("CSI.ETABS.API.ETABSObject", instance.ProcessId); }
        catch (COMException e) { throw new EtabsUnavailableException($"Could not attach to ETABS process {instance.ProcessId}: {e.Message} Make sure ETABS has finished starting and no dialog box is open.", e); }
        if (api is null)
            throw new EtabsUnavailableException($"ETABS process {instance.ProcessId} did not provide its API object. Wait until ETABS has finished starting and no dialog box is open, then try again.");
        return new(api, instance, owned: false);
    }

    public static LiveConnection Launch(string exePath)
    {
        var before = Process.GetProcessesByName("ETABS").Select(p => p.Id).ToHashSet();
        var api = ((cHelper)new Helper()).CreateObject(exePath) ?? throw new EtabsUnavailableException($"Could not start {exePath}.");
        var ret = api.ApplicationStart();
        if (ret != 0) throw new EtabsUnavailableException($"{exePath} did not start (ApplicationStart returned {ret}).");
        var pid = Process.GetProcessesByName("ETABS").Select(p => p.Id).Where(id => !before.Contains(id)).DefaultIfEmpty(0).Max();
        return new(api, new EtabsInstance(pid, exePath, FileVersionInfo.GetVersionInfo(exePath).FileVersion, ""), owned: true);
    }

    public void Dispose()
    {
        var held = api; api = null;
        if (held is null) return;
        try { if (owned) held.ApplicationExit(false); }
        catch (COMException) { /* ETABS already closed. */ }
        // CSI's .NET helper can return a managed wrapper rather than a runtime COM object; only the latter is released here.
        finally { if (OperatingSystem.IsWindows() && Marshal.IsComObject(held)) Marshal.FinalReleaseComObject(held); }
    }
}

// Wraps every API call: records non-zero return codes with their consequence, and turns a lost connection
// (ETABS closed or stopped responding) into EtabsUnavailableException so that nothing half-read is used.
internal sealed class Calls(EtabsInstance instance)
{
    public readonly List<ApiIssue> Issues = [];

    public bool Ok(Func<int> call, string operation, string? name, string method, string consequence)
    {
        var ret = Guard(call);
        if (ret == 0) return true;
        Issues.Add(new(operation, name, method, ret, null, consequence));
        return false;
    }

    public T Guard<T>(Func<T> call)
    {
        try { return call(); }
        // When ETABS ends during a call, CSI's managed wrappers throw NullReferenceException from inside ETABSv1 (verified
        // by ending ETABS mid-extraction); a COM failure appears as COMException. Neither means anything but a lost ETABS.
        catch (Exception e) when (e is COMException or InvalidComObjectException or InvalidCastException ||
                                  (e is NullReferenceException && e.TargetSite?.DeclaringType?.Assembly.GetName().Name == "ETABSv1"))
        {
            throw new EtabsUnavailableException($"ETABS (process {instance.ProcessId}) closed or stopped responding during extraction ({e.GetType().Name}: {e.Message.Trim()}). Nothing was written; reopen the model and extract again.", e);
        }
    }
}

internal sealed class LiveReader : IEtabsModelReader, IEtabsStoryReader, IEtabsMaterialReader, IEtabsSectionReader, IEtabsFrameReader, IEtabsAreaReader, IEtabsDesignResultReader
{
    private readonly cSapModel m;
    private readonly EtabsInstance instance;
    private readonly Calls c;

    public LiveReader(cSapModel model, EtabsInstance instance) { m = model; this.instance = instance; c = new Calls(instance); }

    public IEtabsStoryReader Stories => this;
    public IEtabsMaterialReader Materials => this;
    public IEtabsSectionReader Sections => this;
    public IEtabsFrameReader Frames => this;
    public IEtabsAreaReader Areas => this;
    public IEtabsDesignResultReader Design => this;
    public IReadOnlyList<ApiIssue> Issues => c.Issues;

    public RawUnits ReadPresentUnits()
    {
        eForce f = 0; eLength l = 0; eTemperature t = 0;
        if (!c.Ok(() => m.GetPresentUnits_2(ref f, ref l, ref t), "Read present units", null, "SapModel.GetPresentUnits_2", "Values cannot be converted to metres, so extraction stops."))
            throw new EtabsUnavailableException("ETABS did not report its present units, so values cannot be converted. Nothing was written.");
        return new(f.ToString(), l.ToString(), t.ToString());
    }

    public RawModelInfo ReadModelInfo()
    {
        string version = ""; double number = 0;
        c.Ok(() => m.GetVersion(ref version, ref number), "Read ETABS version", null, "SapModel.GetVersion", "The ETABS version is recorded from ETABS.exe instead.");
        var path = c.Guard(() => m.GetModelFilename(true)) ?? "";
        var locked = c.Guard(() => m.GetModelIsLocked());
        eForce f = 0; eLength l = 0; eTemperature t = 0;
        c.Ok(() => m.GetDatabaseUnits_2(ref f, ref l, ref t), "Read database units", null, "SapModel.GetDatabaseUnits_2", "Database units are not recorded; values use present units.");
        var database = new RawUnits(f.ToString(), l.ToString(), t.ToString());
        int towerCount = 0; string[]? towers = null;
        // Returns non-zero when multiple towers are not enabled; that is a single-tower model.
        if (c.Guard(() => m.Tower.GetNameList(ref towerCount, ref towers)) != 0) towers = [];
        var (api, apiVersion) = EtabsApiLoader.Describe();
        string? hash = null; DateTimeOffset? modified = null;
        if (path.Length > 0 && File.Exists(path))
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                modified = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            }
            catch (IOException e) { c.Issues.Add(new("Hash the saved model file", null, "File read", null, e.Message, "The model file hash is not recorded.")); }
            catch (UnauthorizedAccessException e) { c.Issues.Add(new("Hash the saved model file", null, "File read", null, e.Message, "The model file hash is not recorded.")); }
        }
        return new RawModelInfo
        {
            Program = "ETABS", ProgramVersion = version.Length > 0 ? version : instance.Version ?? "unknown",
            ProgramBuild = instance.Version ?? "unknown", ProgramPath = instance.ExePath ?? "", ApiAssembly = api, ApiAssemblyVersion = apiVersion,
            ProcessId = instance.ProcessId, ModelPath = path, ModelFileSha256 = hash, ModelFileModifiedAt = modified, ModelLocked = locked,
            PresentUnits = ReadPresentUnits(), DatabaseUnits = database, Towers = [.. (towers ?? []).Take(towerCount)]
        };
    }

    RawStories IEtabsStoryReader.Read()
    {
        double baseElevation = 0; int n = 0; string[]? names = null, similar = null; double[]? elevations = null, heights = null, spliceHeight = null;
        bool[]? master = null, splice = null; int[]? color = null;
        if (!c.Ok(() => m.Story.GetStories_2(ref baseElevation, ref n, ref names, ref elevations, ref heights, ref master, ref similar, ref splice, ref spliceHeight, ref color),
                "Read stories", null, "Story.GetStories_2", "No story bands; every object is reported as Unallocated."))
            return new(0, []);
        return new(baseElevation, [.. Enumerable.Range(0, n).Select(i => new RawStory(names![i], elevations![i], heights![i], master![i], similar![i] ?? ""))]);
    }

    ImmutableArray<RawMaterial> IEtabsMaterialReader.Read()
    {
        int n = 0; string[]? names = null;
        if (!c.Ok(() => m.PropMaterial.GetNameList(ref n, ref names), "List materials", null, "PropMaterial.GetNameList", "Materials are unknown, so no element can be classified as concrete."))
            return [];
        var list = new List<RawMaterial>();
        foreach (var name in names!.Take(n))
        {
            eMatType type = 0; int sym = 0;
            if (!c.Ok(() => m.PropMaterial.GetTypeOAPI(name, ref type, ref sym), "Read material type", name, "PropMaterial.GetTypeOAPI", "Elements using this material have unknown material and are not quantified."))
                continue;
            double? fc = null, fy = null, w = null, mass = null;
            if (type == eMatType.Concrete)
            {
                double v = 0, fcs = 0, s1 = 0, s2 = 0, slope = 0, fa = 0, da = 0; bool light = false; int ss = 0, hys = 0;
                if (c.Ok(() => m.PropMaterial.GetOConcrete_1(name, ref v, ref light, ref fcs, ref ss, ref hys, ref s1, ref s2, ref slope, ref fa, ref da), "Read concrete strength", name, "PropMaterial.GetOConcrete_1", "The concrete grade is not recorded; quantities are unaffected."))
                    fc = v;
            }
            else if (type == eMatType.Rebar)
            {
                double y = 0, u = 0, ey = 0, eu = 0, sh = 0, su = 0, slope = 0; bool cal = false; int ss = 0, hys = 0;
                if (c.Ok(() => m.PropMaterial.GetORebar_1(name, ref y, ref u, ref ey, ref eu, ref ss, ref hys, ref sh, ref su, ref slope, ref cal), "Read rebar strength", name, "PropMaterial.GetORebar_1", "The rebar grade is not recorded; quantities are unaffected."))
                    fy = y;
            }
            double wv = 0, mv = 0;
            if (c.Ok(() => m.PropMaterial.GetWeightAndMass(name, ref wv, ref mv), "Read material density", name, "PropMaterial.GetWeightAndMass", "The material density is not recorded; quantities are unaffected."))
            { w = wv; mass = mv; }
            list.Add(new(name, type.ToString(), fc, fy, w, mass));
        }
        return [.. list];
    }

    public ImmutableArray<RawFrameSection> ReadFrameSections()
    {
        int n = 0; string[]? names = null;
        if (!c.Ok(() => m.PropFrame.GetNameList(ref n, ref names), "List frame sections", null, "PropFrame.GetNameList", "Frame sections are unknown, so no frame can be quantified."))
            return [];
        var list = new List<RawFrameSection>();
        foreach (var name in names!.Take(n))
        {
            eFramePropType type = 0;
            if (!c.Ok(() => m.PropFrame.GetTypeOAPI(name, ref type), "Read frame section type", name, "PropFrame.GetTypeOAPI", "Frames using this section have unknown dimensions."))
                continue;
            string material = "", file = "", notes = "", guid = ""; int color = 0;
            double t3 = 0, t2 = 0; double? depth = null, width = null, diameter = null, area = null;
            if (type == eFramePropType.Rectangular)
            {
                if (c.Ok(() => m.PropFrame.GetRectangle(name, ref file, ref material, ref t3, ref t2, ref color, ref notes, ref guid), "Read rectangular section", name, "PropFrame.GetRectangle", "Frames using this section have unknown dimensions."))
                { depth = t3; width = t2; }
            }
            else if (type == eFramePropType.Circle)
            {
                if (c.Ok(() => m.PropFrame.GetCircle(name, ref file, ref material, ref t3, ref color, ref notes, ref guid), "Read circular section", name, "PropFrame.GetCircle", "Frames using this section have unknown dimensions."))
                    diameter = t3;
            }
            else if (type == eFramePropType.Variable)
            {
                // A non-prismatic section has no material of its own (GetMaterial fails, verified); it takes its start section's.
                int items = 0, np = 0; string[]? starts = null, ends = null; double[]? lengths = null; int[]? types = null, e33 = null, e22 = null;
                if (c.Ok(() => m.PropFrame.GetNonPrismatic(name, ref items, ref starts, ref ends, ref lengths, ref types, ref e33, ref e22, ref np, ref notes, ref guid),
                        "Read non-prismatic section", name, "PropFrame.GetNonPrismatic", "Frames using this section have unknown material.") && items > 0)
                    c.Ok(() => m.PropFrame.GetMaterial(starts![0], ref material), "Read section material", starts![0], "PropFrame.GetMaterial", "Frames using this section have unknown material.");
            }
            else c.Ok(() => m.PropFrame.GetMaterial(name, ref material), "Read section material", name, "PropFrame.GetMaterial", "Frames using this section have unknown material.");
            double a = 0, as2 = 0, as3 = 0, j = 0, i22 = 0, i33 = 0, s22 = 0, s33 = 0, z22 = 0, z33 = 0, r22 = 0, r33 = 0;
            if (c.Ok(() => m.PropFrame.GetSectProps(name, ref a, ref as2, ref as3, ref j, ref i22, ref i33, ref s22, ref s33, ref z22, ref z33, ref r22, ref r33), "Read section area", name, "PropFrame.GetSectProps", "The section-area cross-check is skipped."))
                area = a;
            list.Add(new(name, type.ToString(), material.Length > 0 ? material : null, depth, width, diameter, area,
                type is eFramePropType.Rectangular or eFramePropType.Circle ? ColumnRebar(name) : null));
        }
        return [.. list];
    }

    // ETABS keeps default column rebar data on every concrete section (verified), so it is read only for sections whose
    // rebar design type is column (GetTypeRebar = 1); a beam-type section's column data is meaningless.
    private RawColumnRebar? ColumnRebar(string section)
    {
        int rebarType = 0;
        if (c.Guard(() => m.PropFrame.GetTypeRebar(section, ref rebarType)) != 0 || rebarType != 1) return null;
        string longMat = "", confineMat = "", size = "", tie = ""; int pattern = 0, confine = 0, nc = 0, n3 = 0, n2 = 0, t2 = 0, t3 = 0; double cover = 0, spacing = 0; bool design = false;
        if (c.Guard(() => m.PropFrame.GetRebarColumn(section, ref longMat, ref confineMat, ref pattern, ref confine, ref cover, ref nc, ref n3, ref n2, ref size, ref tie, ref spacing, ref t2, ref t3, ref design)) != 0)
            return null;
        double? barArea = null;
        double area = 0, diameter = 0;
        if (!string.IsNullOrWhiteSpace(size) && c.Ok(() => m.PropRebar.GetRebarProps(size, ref area, ref diameter), "Read bar size", size, "PropRebar.GetRebarProps", "Modeled column bars of this size cannot be counted."))
            barArea = area;
        return new(longMat, pattern, cover, nc, n3, n2, size, barArea, design);
    }

    public ImmutableArray<RawAreaProperty> ReadAreaProperties()
    {
        int n = 0; string[]? names = null;
        if (!c.Ok(() => m.PropArea.GetNameList(ref n, ref names), "List area properties", null, "PropArea.GetNameList", "Area properties are unknown, so no slab or wall can be quantified."))
            return [];
        var list = new List<RawAreaProperty>();
        foreach (var name in names!.Take(n))
        {
            eSlabType slab = 0; eWallPropType wall = 0; eDeckType deck = 0; eShellType shell = 0; string material = "", notes = "", guid = ""; double thickness = 0; int color = 0;
            // GetSlab, GetWall and GetDeck each return non-zero for the other property kinds; that is how the kind is found.
            if (c.Guard(() => m.PropArea.GetSlab(name, ref slab, ref shell, ref material, ref thickness, ref color, ref notes, ref guid)) == 0)
                list.Add(new(name, "Slab", slab.ToString(), shell.ToString(), material, thickness));
            else if (c.Guard(() => m.PropArea.GetWall(name, ref wall, ref shell, ref material, ref thickness, ref color, ref notes, ref guid)) == 0)
                list.Add(new(name, "Wall", wall.ToString(), shell.ToString(), material, thickness));
            else if (c.Guard(() => m.PropArea.GetDeck(name, ref deck, ref shell, ref material, ref thickness, ref color, ref notes, ref guid)) == 0)
                list.Add(new(name, "Deck", deck.ToString(), shell.ToString(), material, thickness));
            else list.Add(new(name, "Other", null, null, null, null));
        }
        return [.. list];
    }

    ImmutableArray<RawFrame> IEtabsFrameReader.Read(CancellationToken token)
    {
        int n = 0; string[]? names = null, props = null, stories = null, p1 = null, p2 = null;
        double[]? x1 = null, y1 = null, z1 = null, x2 = null, y2 = null, z2 = null, angle = null, o1x = null, o2x = null, o1y = null, o2y = null, o1z = null, o2z = null; int[]? cardinal = null;
        // Coordinates in the Global system; offsets are the insertion-point joint offsets.
        if (!c.Ok(() => m.FrameObj.GetAllFrames(ref n, ref names, ref props, ref stories, ref p1, ref p2, ref x1, ref y1, ref z1, ref x2, ref y2, ref z2,
                ref angle, ref o1x, ref o2x, ref o1y, ref o2y, ref o1z, ref o2z, ref cardinal, "Global"), "List frame objects", null, "FrameObj.GetAllFrames", "No frame can be read, so beams and columns are missing from the snapshot."))
            return [];
        var labels = Labels(isFrame: true);
        var list = new List<RawFrame>(n);
        for (var i = 0; i < n; i++)
        {
            token.ThrowIfCancellationRequested();
            var name = names![i];
            string label = "", story = "", guid = "", overwrite = "";
            if (labels.TryGetValue(name, out var known)) (label, story) = known;
            else c.Ok(() => m.FrameObj.GetLabelFromName(name, ref label, ref story), "Read frame label", name, "FrameObj.GetLabelFromName", "The frame is identified by its unique name instead of its label.");
            c.Ok(() => m.FrameObj.GetGUID(name, ref guid), "Read frame GUID", name, "FrameObj.GetGUID", "The GUID is not recorded.");
            eFrameDesignOrientation orientation = eFrameDesignOrientation.Null;
            c.Ok(() => m.FrameObj.GetDesignOrientation(name, ref orientation), "Read frame design orientation", name, "FrameObj.GetDesignOrientation", "The frame cannot be classified as beam or column, so it is not quantified.");
            int? designProcedure = null; // not read: it does not affect classification, and each per-object call costs time on large models
            string? materialOverwrite = null;
            if (c.Guard(() => m.FrameObj.GetMaterialOverwrite(name, ref overwrite)) == 0 && !string.IsNullOrWhiteSpace(overwrite) && overwrite != "None") materialOverwrite = overwrite;
            int curve = 0, points = 0; double tension = 0; double[]? gx = null, gy = null, gz = null;
            // Verified on ETABS 22.7: GetCurved_2 returns 1 for every straight frame and 0 with the curve type for a curved one.
            var curveType = c.Guard(() => m.FrameObj.GetCurved_2(name, ref curve, ref tension, ref points, ref gx, ref gy, ref gz)) == 0 ? curve : 0;
            list.Add(new RawFrame
            {
                Name = name, Label = Blank(label), Story = Blank(story) ?? Blank(stories![i]), Guid = Blank(guid), Section = props![i],
                Point1 = new(x1![i], y1![i], z1![i]), Point2 = new(x2![i], y2![i], z2![i]),
                Offset1 = new(o1x![i], o1y![i], o1z![i]), Offset2 = new(o2x![i], o2y![i], o2z![i]),
                CardinalPoint = cardinal![i], Angle = angle![i], DesignOrientation = orientation.ToString(), DesignProcedure = designProcedure,
                MaterialOverwrite = materialOverwrite, CurveType = curveType
            });
        }
        return [.. list];
    }

    ImmutableArray<RawArea> IEtabsAreaReader.Read(CancellationToken token)
    {
        int n = 0, total = 0; string[]? names = null, pointNames = null; eAreaDesignOrientation[]? orientation = null; int[]? delimiter = null; double[]? x = null, y = null, z = null;
        if (!c.Ok(() => m.AreaObj.GetAllAreas(ref n, ref names, ref orientation, ref total, ref delimiter, ref pointNames, ref x, ref y, ref z),
                "List area objects", null, "AreaObj.GetAllAreas", "No area can be read, so slabs, walls and openings are missing from the snapshot."))
            return [];
        var labels = Labels(isFrame: false);
        var list = new List<RawArea>(n);
        for (var i = 0; i < n; i++)
        {
            token.ThrowIfCancellationRequested();
            var name = names![i];
            // PointDelimiter[i] is the index of the last boundary point of area i.
            var first = i == 0 ? 0 : delimiter![i - 1] + 1;
            var boundary = Enumerable.Range(first, delimiter![i] - first + 1).Select(k => new RawPoint(x![k], y![k], z![k])).ToImmutableArray();
            string label = "", story = "", guid = "", property = "", overwrite = "";
            if (labels.TryGetValue(name, out var known)) (label, story) = known;
            else c.Ok(() => m.AreaObj.GetLabelFromName(name, ref label, ref story), "Read area label", name, "AreaObj.GetLabelFromName", "The area is identified by its unique name instead of its label.");
            c.Ok(() => m.AreaObj.GetGUID(name, ref guid), "Read area GUID", name, "AreaObj.GetGUID", "The GUID is not recorded.");
            c.Ok(() => m.AreaObj.GetProperty(name, ref property), "Read area property", name, "AreaObj.GetProperty", "The area's thickness and material are unknown, so it is not quantified.");
            var opening = false;
            c.Ok(() => m.AreaObj.GetOpening(name, ref opening), "Read area opening flag", name, "AreaObj.GetOpening", "The area is treated as a solid area, not an opening.");
            string? materialOverwrite = null;
            if (c.Guard(() => m.AreaObj.GetMaterialOverwrite(name, ref overwrite)) == 0 && !string.IsNullOrWhiteSpace(overwrite) && overwrite != "None") materialOverwrite = overwrite;
            int edges = 0; int[]? types = null, counts = null; double[]? tension = null, gx = null, gy = null, gz = null;
            // Returns non-zero for areas without curved edges.
            var curved = c.Guard(() => m.AreaObj.GetCurvedEdges(name, ref edges, ref types, ref tension, ref counts, ref gx, ref gy, ref gz)) == 0 && (types ?? []).Any(t => t != 0);
            list.Add(new RawArea
            {
                Name = name, Label = Blank(label), Story = Blank(story), Guid = Blank(guid), Property = property, DesignOrientation = orientation![i].ToString(),
                IsOpening = opening, Boundary = boundary, MaterialOverwrite = materialOverwrite, HasCurvedEdges = curved
            });
        }
        return [.. list];
    }

    RawDesign IEtabsDesignResultReader.Read(IReadOnlyList<RawFrame> frames, CancellationToken token)
    {
        var available = c.Guard(() => m.DesignConcrete.GetResultsAvailable());
        string code = "";
        var codeName = c.Ok(() => m.DesignConcrete.GetCode(ref code), "Read concrete design code", null, "DesignConcrete.GetCode", "The design code is not recorded.") && code.Length > 0 ? code : null;
        if (!available) return new(false, codeName, [], []);
        var beams = new List<RawBeamDesign>(); var columns = new List<RawColumnDesign>();
        foreach (var frame in frames)
        {
            token.ThrowIfCancellationRequested();
            if (frame.DesignOrientation is not ("Beam" or "Column")) continue;
            var name = frame.Name;
            string designSection = "";
            string? section = c.Guard(() => m.DesignConcrete.GetDesignSection(name, ref designSection)) == 0 && designSection.Length > 0 ? designSection : null;
            int count = 0; string[]? names = null, errors = null, warnings = null;
            double[]? location = null;
            if (frame.DesignOrientation == "Beam")
            {
                string[]? tc = null, bc = null, vc = null, tlc = null, ttc = null; double[]? top = null, bottom = null, shear = null, tl = null, tt = null;
                // Frames without concrete frame design (steel, or not designed) return non-zero; they simply have no results.
                if (c.Guard(() => m.DesignConcrete.GetSummaryResultsBeam(name, ref count, ref names, ref location, ref tc, ref top, ref bc, ref bottom, ref vc, ref shear,
                        ref tlc, ref tl, ref ttc, ref tt, ref errors, ref warnings, eItemType.Objects)) != 0 || count == 0) continue;
                beams.Add(new(name, [.. location!.Take(count)], [.. top!.Take(count)], [.. bottom!.Take(count)], [.. shear!.Take(count)], [.. tl!.Take(count)], [.. tt!.Take(count)],
                    [.. (errors ?? []).Take(count)], [.. (warnings ?? []).Take(count)], section));
            }
            else
            {
                int[]? option = null; string[]? pc = null, vmc = null, vnc = null; double[]? pmm = null, ratio = null, avm = null, avn = null;
                if (c.Guard(() => m.DesignConcrete.GetSummaryResultsColumn(name, ref count, ref names, ref option, ref location, ref pc, ref pmm, ref ratio, ref vmc, ref avm,
                        ref vnc, ref avn, ref errors, ref warnings, eItemType.Objects)) != 0 || count == 0) continue;
                columns.Add(new(name, option![0], [.. location!.Take(count)], [.. pmm!.Take(count)], [.. ratio!.Take(count)],
                    [.. (errors ?? []).Take(count)], [.. (warnings ?? []).Take(count)], section));
            }
        }
        return new(true, codeName, [.. beams], [.. columns]);
    }

    // Every label and story in one call. Measured on a 10,000-object model: 8 ms for all 7,650 frames, against 1.27 s
    // object by object. If the bulk call fails, labels are read per object.
    private Dictionary<string, (string Label, string Story)> Labels(bool isFrame)
    {
        int n = 0; string[]? names = null, labels = null, stories = null;
        var ret = c.Guard(() => isFrame ? m.FrameObj.GetLabelNameList(ref n, ref names, ref labels, ref stories) : m.AreaObj.GetLabelNameList(ref n, ref names, ref labels, ref stories));
        var result = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        if (ret != 0 || names is null || labels is null || stories is null) return result;
        for (var i = 0; i < n; i++) result[names[i]] = (labels[i], stories[i]);
        return result;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
#endif
