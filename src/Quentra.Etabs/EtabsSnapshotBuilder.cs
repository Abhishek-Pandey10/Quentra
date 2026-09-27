using System.Collections.Immutable;
using System.Globalization;
using Quentra.Core;

namespace Quentra.Etabs;

public sealed record EtabsBuildOptions
{
    // Named person who approves ETABS-derived steel evidence (design demand equivalents, modeled column bars) for
    // this takeoff. Without it the steel inputs are recorded but not counted.
    public string? SteelApprovedBy { get; init; }
    // Named person who approves the measurement policy. Without it the run cannot be accepted.
    public string? PolicyApprovedBy { get; init; }
    // Largest interval between design stations that is integrated; wider gaps leave the component unknown.
    public double MaximumStationGapM { get; init; } = DefaultMaximumStationGapM;
    public const double DefaultMaximumStationGapM = 1.0;

    // The options a snapshot was built with, read back from it, so a re-extraction for comparison is built the same way.
    public static EtabsBuildOptions RecoveredFrom(TakeoffSnapshot snapshot) => new()
    {
        SteelApprovedBy = snapshot.Reinforcement.Select(x => x.ApprovedBy).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
        PolicyApprovedBy = snapshot.Policy.ApprovedBy,
        MaximumStationGapM = snapshot.Reinforcement.Select(x => x.MaximumStationGapM).FirstOrDefault(x => x is > 0) ?? DefaultMaximumStationGapM
    };
}

public sealed record ExtractionCounts(int Beams, int Columns, int OtherFrames, int Slabs, int Walls, int OtherAreas, int Openings,
    int NonConcrete, int ConcreteMaterials, IReadOnlyList<string> ConcreteMaterialNames, int OpeningsWithoutHost, int SteelInputs,
    int ApiIssues, int Stories);

public sealed record EtabsBuildResult(TakeoffSnapshot Snapshot, ExtractionCounts Counts);

// Maps a raw ETABS capture to a Quentra schema 2 snapshot. Deterministic: the same capture gives the same snapshot.
// Rules:
//  * Quantities come from ETABS model objects (FrameObj/AreaObj), never analysis elements, so meshing cannot duplicate them.
//  * Every length is converted from ETABS present units to metres here, once.
//  * Classification uses ETABS metadata: design orientation, section and property types, material types. Never names.
//  * Anything not supported is still listed, marked unsupported, with a source warning saying exactly why.
//  * Reinforcement is taken only from ETABS design results or modeled column bars, labelled by evidence type; walls
//    and slabs get none, so their steel is unknown, never zero.
public sealed class EtabsSnapshotBuilder : IEtabsSnapshotBuilder
{
    public const double LinearToleranceM = 1e-4, PlanarityToleranceM = 1e-3, SteelDensityKgM3 = 7850;
    public const double BaseBandDepthM = 0.001;
    public static readonly string[] BeamSteel = ["LongitudinalBottom", "LongitudinalTop", "Transverse"];
    public static readonly string[] ColumnSteel = ["Longitudinal", "Transverse"];
    public static readonly string[] SlabSteel = ["XBottom", "XTop", "YBottom", "YTop"];
    public static readonly string[] WallSteel = ["Boundary", "Web"];

    public EtabsBuildResult Build(EtabsRawModel raw, EtabsBuildOptions options) => Run(raw, options);

    public static EtabsBuildResult Run(EtabsRawModel raw, EtabsBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Format != EtabsRawModel.CurrentFormat) throw new InvalidDataException($"Raw capture format '{raw.Format}' is not supported; expected {EtabsRawModel.CurrentFormat}.");
        return new Mapping(raw, options).Build();
    }

    private sealed class Mapping(EtabsRawModel raw, EtabsBuildOptions options)
    {
        private readonly double f = EtabsUnits.MetresPer(raw.Model.PresentUnits.Length);
        private readonly List<SourceWarning> warnings = [];
        private readonly Dictionary<string, RawMaterial> materials = raw.Materials.ToDictionary(x => x.Name, StringComparer.Ordinal);
        private readonly Dictionary<string, RawFrameSection> sections = raw.FrameSections.ToDictionary(x => x.Name, StringComparer.Ordinal);
        private readonly Dictionary<string, RawAreaProperty> areaProps = raw.AreaProperties.ToDictionary(x => x.Name, StringComparer.Ordinal);
        private readonly TakeoffPolicy policy = new()
        {
            Id = "quentra-etabs-default/1",
            IntendedUse = "Modeled concrete from ETABS model objects and ETABS-derived reinforcement evidence, for quantity review. Not a BOQ or procurement quantity.",
            ApprovedBy = string.IsNullOrWhiteSpace(options.PolicyApprovedBy) ? null : options.PolicyApprovedBy.Trim(),
            ApprovedAt = string.IsNullOrWhiteSpace(options.PolicyApprovedBy) ? null : raw.ExtractedAt,
            SteelDensityKgM3 = SteelDensityKgM3, LinearToleranceM = LinearToleranceM, PlanarityToleranceM = PlanarityToleranceM
        };

        private string F(double v) => v.ToString("G6", CultureInfo.InvariantCulture);
        private Point3 P(RawPoint p) => new(p.X * f, p.Y * f, p.Z * f);
        private Point3 P(RawPoint p, RawPoint offset) => new((p.X + offset.X) * f, (p.Y + offset.Y) * f, (p.Z + offset.Z) * f);
        private static SourceLength M(double metres) => new() { Value = metres, Unit = LengthUnit.Metre };
        private void Warn(string? objectId, string code, string message) => warnings.Add(new(objectId, code, message));

        private (MaterialKind Kind, string? Problem) Material(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return (MaterialKind.Unknown, "no material is assigned");
            if (!materials.TryGetValue(name, out var m)) return (MaterialKind.Unknown, $"material '{name}' could not be read from ETABS");
            return m.Type switch
            {
                "Concrete" => (MaterialKind.Concrete, null),
                "Steel" or "Rebar" or "Tendon" or "ColdFormed" or "Aluminum" or "Masonry" => (MaterialKind.NonConcrete, null),
                _ when Weightless(m) => (MaterialKind.NonConcrete, null),
                _ => (MaterialKind.Unknown, $"material '{name}' has ETABS type '{m.Type}', which is not classified as concrete or non-concrete")
            };
        }

        // A non-concrete-type material with no weight and no mass models a dummy or null member (load transfer, null
        // beam): it has no physical substance. Such members are listed as out of scope with ETABS_WEIGHTLESS_MATERIAL.
        private static bool Weightless(RawMaterial m) => m.Type != "Concrete" && m.WeightPerVolume == 0 && m.MassPerVolume == 0;

        private string? WeightlessNote(string? name) => name is not null && materials.TryGetValue(name, out var m) && Weightless(m)
            ? $"Material '{name}' (ETABS type {m.Type}) has zero weight and zero mass, so this member is treated as a dummy or null member with no physical concrete, and is out of scope. Confirm it is not a real member."
            : null;

        public EtabsBuildResult Build()
        {
            var bands = Stories(out var baseBand);
            var storyIds = bands.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var ids = ObjectIds();
            var frames = new List<FrameSnapshot>();
            var areas = new List<AreaSnapshot>();
            var metadata = new List<ElementMetadata>();
            var steel = new List<SteelInput>();
            int beams = 0, columns = 0, otherFrames = 0, slabs = 0, walls = 0, otherAreas = 0, nonConcrete = 0;
            var concreteMaterials = new SortedSet<string>(StringComparer.Ordinal);
            var frameIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var frameLength = new Dictionary<string, double>(StringComparer.Ordinal);
            var frameKinds = new Dictionary<string, FrameKind>(StringComparer.Ordinal);
            var frameSection = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var fr in raw.Frames)
            {
                var id = ids[("frame", fr.Name)];
                frameIds[fr.Name] = id;
                sections.TryGetValue(fr.Section, out var section);
                var materialName = fr.MaterialOverwrite ?? section?.Material;
                var (material, materialProblem) = Material(materialName);
                var kind = fr.DesignOrientation switch { "Column" => FrameKind.Column, "Beam" => FrameKind.Beam, _ => FrameKind.Other };
                var inScope = material != MaterialKind.NonConcrete;
                if (!inScope) nonConcrete++;
                else if (kind == FrameKind.Beam) beams++;
                else if (kind == FrameKind.Column) columns++;
                else otherFrames++;
                if (material == MaterialKind.Concrete && materialName is not null) concreteMaterials.Add(materialName);
                if (WeightlessNote(materialName) is { } dummy) Warn(id, "ETABS_WEIGHTLESS_MATERIAL", dummy);
                if (inScope)
                {
                    if (materialProblem is not null) Warn(id, "ETABS_MATERIAL_UNRESOLVED", $"Concrete is unknown: {materialProblem}.");
                    if (kind == FrameKind.Other)
                        Warn(id, "ETABS_FRAME_ROLE", $"ETABS design orientation is {fr.DesignOrientation}. Only beams and columns are quantified; braces and other frames are listed but not measured.");
                }
                FrameSection? shape = null;
                var prismatic = true;
                if (section is null)
                {
                    if (inScope) Warn(id, "ETABS_SECTION_MISSING", $"Frame section '{fr.Section}' could not be read from ETABS, so the section dimensions are unknown.");
                }
                else
                {
                    shape = section.Type switch
                    {
                        "Rectangular" when section.Depth is > 0 && section.Width is > 0 =>
                            new FrameSection { Shape = SectionShape.Rectangle, Width = M(section.Width.Value * f), Depth = M(section.Depth.Value * f) },
                        "Circle" when section.Diameter is > 0 => new FrameSection { Shape = SectionShape.Circle, Diameter = M(section.Diameter.Value * f) },
                        "Rectangular" or "Circle" => null,
                        _ => new FrameSection { Shape = SectionShape.Unsupported }
                    };
                    prismatic = section.Type != "Variable";
                    if (inScope && shape is null)
                        Warn(id, "ETABS_SECTION_MISSING", $"Section '{section.Name}' ({section.Type}) did not return positive dimensions, so its concrete is unknown.");
                    if (inScope && section.Type is not ("Rectangular" or "Circle"))
                        Warn(id, "ETABS_SECTION_UNSUPPORTED", $"Section '{section.Name}' is ETABS type {Describe(section.Type)}. Supported: rectangular beams and columns, circular columns. Its concrete is not estimated.");
                    if (inScope && section.Type == "Rectangular" && section.Area is > 0 && section.Depth is > 0 && section.Width is > 0 &&
                        Math.Abs(section.Area.Value - section.Depth.Value * section.Width.Value) > 1e-3 * section.Area.Value)
                        Warn(id, "ETABS_SECTION_AREA_MISMATCH", $"Section '{section.Name}': ETABS reports area {F(section.Area.Value * f * f)} m², but width × depth is {F(section.Depth.Value * section.Width.Value * f * f)} m². Quentra uses width × depth.");
                }
                var straight = fr.CurveType is 0;
                if (inScope && fr.CurveType is not 0)
                    Warn(id, "ETABS_CURVED_FRAME", fr.CurveType is null
                        ? "ETABS did not report whether this frame is curved, so it is not measured as straight."
                        : $"ETABS reports a curved frame (curve type {fr.CurveType}). Curved frames are not measured; the chord would understate the length.");
                var hasOffset = new[] { fr.Offset1.X, fr.Offset1.Y, fr.Offset1.Z, fr.Offset2.X, fr.Offset2.Y, fr.Offset2.Z }.Any(x => x != 0);
                if (inScope && hasOffset)
                    Warn(id, "ETABS_JOINT_OFFSET", $"The frame has insertion-point joint offsets (I-end {Vec(fr.Offset1)}, J-end {Vec(fr.Offset2)} m). Its axis is measured between the offset ends.");
                var start = P(fr.Point1, fr.Offset1); var end = P(fr.Point2, fr.Offset2);
                frames.Add(new FrameSnapshot
                {
                    ObjectId = id, SourceReference = Reference("frame", fr.Name, fr.Label, fr.Story, fr.Guid, $"section {fr.Section}" + (fr.MaterialOverwrite is null ? "" : $", material overwrite {fr.MaterialOverwrite}")),
                    Kind = kind, Material = material, IsStraight = straight, IsPrismatic = prismatic, Start = start, End = end, Section = shape
                });
                var assigned = StoryFor(id, fr.Story, storyIds, inScope);
                metadata.Add(new(id, materialName ?? "(none)", fr.Section, assigned,
                    !inScope ? [] : kind == FrameKind.Beam ? [.. BeamSteel] : [.. ColumnSteel]));
                frameLength[fr.Name] = Distance(start, end);
                frameKinds[fr.Name] = inScope ? kind : FrameKind.Other;
                frameSection[fr.Name] = fr.Section;
            }

            var openings = raw.Areas.Where(x => x.IsOpening).ToArray();
            var hostOpenings = AssignOpenings(openings, ids, out var orphans);
            foreach (var ar in raw.Areas.Where(x => !x.IsOpening))
            {
                var id = ids[("area", ar.Name)];
                areaProps.TryGetValue(ar.Property, out var prop);
                var isNull = prop is null && (ar.Property is "None" or "");
                var materialName = ar.MaterialOverwrite ?? prop?.Material;
                var (material, materialProblem) = isNull ? (MaterialKind.NonConcrete, null) : Material(materialName);
                var kind = prop?.Kind switch
                {
                    "Slab" => AreaKind.Slab,
                    "Wall" => AreaKind.Wall,
                    _ => isNull ? (ar.DesignOrientation == "Wall" ? AreaKind.Wall : ar.DesignOrientation == "Floor" ? AreaKind.Slab : AreaKind.Unsupported) : AreaKind.Unsupported
                };
                var inScope = material != MaterialKind.NonConcrete;
                if (!inScope) nonConcrete++;
                else if (kind == AreaKind.Slab) slabs++;
                else if (kind == AreaKind.Wall) walls++;
                else otherAreas++;
                if (material == MaterialKind.Concrete && materialName is not null) concreteMaterials.Add(materialName);
                if (isNull) Warn(id, "ETABS_NULL_AREA", "The area has no property (None) in ETABS, so it has no thickness or material. It is listed as non-concrete and not measured.");
                else if (WeightlessNote(materialName) is { } dummy) Warn(id, "ETABS_WEIGHTLESS_MATERIAL", dummy);
                if (inScope && materialProblem is not null) Warn(id, "ETABS_MATERIAL_UNRESOLVED", $"Concrete is unknown: {materialProblem}.");
                var thicknessOk = !isNull && prop is not null && AreaPropertyProblem(prop) is null;
                if (inScope && prop is null && !isNull)
                    Warn(id, "ETABS_AREA_PROPERTY_UNSUPPORTED", $"Area property '{ar.Property}' could not be read from ETABS, so its thickness is unknown.");
                else if (inScope && prop is not null && AreaPropertyProblem(prop) is { } problem)
                    Warn(id, "ETABS_AREA_PROPERTY_UNSUPPORTED", $"Area property '{prop.Name}': {problem} Its concrete is not estimated.");
                if (inScope && prop is not null && (prop.Kind, ar.DesignOrientation) is ("Slab", "Wall") or ("Wall", "Floor"))
                    Warn(id, "ETABS_AREA_ORIENTATION", $"The property '{prop.Name}' is a {prop.Kind.ToLowerInvariant()} property but ETABS orients the object as {ar.DesignOrientation}. It is measured as a {prop.Kind.ToLowerInvariant()}; check the modelling.");
                if (inScope && ar.HasCurvedEdges)
                {
                    Warn(id, "ETABS_AREA_CURVED_EDGE", "The area has curved edges in ETABS. Only straight-edged polygons are measured; the chord polygon would misstate the area.");
                    kind = AreaKind.Unsupported;
                }
                var assignedOpenings = hostOpenings.GetValueOrDefault(ar.Name) ?? [];
                if (inScope && assignedOpenings.Count > 0)
                    Warn(id, "ETABS_OPENINGS_ASSIGNED", $"Openings deducted from this {kind.ToString().ToLowerInvariant()} (ETABS opening objects that overlap it in its plane): " +
                        string.Join("; ", assignedOpenings.Select((o, i) => $"opening {i + 1} = {Reference("area", o.Name, o.Label, o.Story, null, null)}")) + ".");
                areas.Add(new AreaSnapshot
                {
                    ObjectId = id, SourceReference = Reference("area", ar.Name, ar.Label, ar.Story, ar.Guid, $"property {ar.Property}, orientation {ar.DesignOrientation}" + (ar.MaterialOverwrite is null ? "" : $", material overwrite {ar.MaterialOverwrite}")),
                    Kind = kind, Material = material, Boundary = [.. ar.Boundary.Select(P)],
                    Openings = [.. assignedOpenings.Select(o => o.Boundary.Select(P).ToImmutableArray())],
                    OpeningsVerified = true, PhysicalThicknessVerified = thicknessOk,
                    Thickness = M(prop?.Thickness is { } t && double.IsFinite(t) ? t * f : 0)
                });
                var assigned = StoryFor(id, ar.Story, storyIds, inScope);
                metadata.Add(new(id, materialName ?? "(none)", ar.Property, assigned,
                    !inScope ? [] : kind == AreaKind.Wall || (kind == AreaKind.Unsupported && ar.DesignOrientation == "Wall") ? [.. WallSteel] : [.. SlabSteel]));
            }
            if (orphans.Count > 0)
                Warn(null, "ETABS_OPENING_NO_HOST", $"These ETABS opening objects do not overlap any slab or wall in their plane, so they deduct nothing: {Names(orphans)}.");

            Steel(frameIds, frameLength, frameKinds, frameSection, metadata, steel);
            RunWarnings(baseBand, slabs + walls + otherAreas > 0, walls > 0, steel.Count);
            foreach (var issue in raw.Issues)
            {
                var objectId = issue.ObjectName is null ? null
                    : ids.TryGetValue(("frame", issue.ObjectName), out var fid) && issue.Operation.Contains("frame", StringComparison.OrdinalIgnoreCase) ? fid
                    : ids.TryGetValue(("area", issue.ObjectName), out var aid) && issue.Operation.Contains("area", StringComparison.OrdinalIgnoreCase) ? aid : null;
                Warn(objectId, "ETABS_API_CALL_FAILED", $"{issue.Operation}{(issue.ObjectName is null || objectId is not null ? "" : $" ({issue.ObjectName})")}: ETABS {issue.Method} " +
                    (issue.ReturnCode is { } code ? $"returned code {code}" : $"failed ({issue.Exception})") + $". {issue.Consequence}");
            }

            var snapshot = new TakeoffSnapshot
            {
                SchemaVersion = 2,
                Model = new ModelSnapshot
                {
                    SchemaVersion = 1, ModelId = ModelId(raw.Model.ModelPath), Origin = SnapshotOrigin.Etabs,
                    SourceDescription = $"ETABS {raw.Model.ProgramVersion} (build {raw.Model.ProgramBuild}) model {raw.Model.ModelPath}, extracted by Quentra ETABS adapter {raw.ExtractorVersion}. Present units {raw.Model.PresentUnits}; all values converted to metres.",
                    CapturedAt = raw.ExtractedAt, CoordinateUnit = LengthUnit.Metre, Frames = [.. frames]
                },
                Areas = [.. areas], Stories = bands, Metadata = [.. metadata], Reinforcement = [.. steel], Policy = policy,
                Source = new SnapshotSource
                {
                    Program = raw.Model.Program, ProgramVersion = raw.Model.ProgramVersion, ProgramBuild = raw.Model.ProgramBuild,
                    ApiAssembly = raw.Model.ApiAssembly, ApiAssemblyVersion = raw.Model.ApiAssemblyVersion, ModelPath = raw.Model.ModelPath,
                    ModelFileSha256 = raw.Model.ModelFileSha256, ModelFileModifiedAt = raw.Model.ModelFileModifiedAt, ModelLocked = raw.Model.ModelLocked,
                    PresentUnits = raw.Model.PresentUnits.ToString(), Extractor = "Quentra ETABS adapter", ExtractorVersion = raw.ExtractorVersion,
                    ExtractedAt = raw.ExtractedAt, RawCaptureSha256 = EtabsRawJson.Sha256(raw)
                },
                SourceWarnings = [.. warnings]
            };
            return new(snapshot, new(beams, columns, otherFrames, slabs, walls, otherAreas, openings.Length, nonConcrete,
                concreteMaterials.Count, [.. concreteMaterials], orphans.Count, steel.Count, raw.Issues.Length, bands.Length));
        }

        private static string Describe(string type) => type switch
        {
            "Variable" => "Variable (non-prismatic)", "SD" => "SD (Section Designer)", "Auto" => "Auto (auto-select list)",
            "General" => "General (user-defined properties)", _ => type
        };

        // Uniform physical thickness is known only for these ETABS property types.
        private static string? AreaPropertyProblem(RawAreaProperty p) => p.Kind switch
        {
            "Slab" when p.SubType is "Ribbed" or "Waffle" => $"ETABS slab type {p.SubType} has ribs, so the property thickness is not the physical concrete depth.",
            "Slab" when p.SubType is not ("Slab" or "Drop" or "Mat" or "Footing") => $"ETABS slab type {p.SubType} is not supported.",
            "Wall" when p.SubType != "Specified" => $"ETABS wall property type {p.SubType} does not give one uniform thickness.",
            "Slab" or "Wall" when p.ShellType is "Layered" => "a layered shell's thickness comes from its layers, which are not read.",
            "Slab" or "Wall" when p.ShellType is not ("ShellThin" or "ShellThick" or "Membrane") => $"shell type {p.ShellType} is not supported.",
            "Slab" or "Wall" when p.Thickness is not > 0 || !double.IsFinite(p.Thickness.Value) => "it has no positive thickness.",
            "Slab" or "Wall" => null,
            _ => $"it is a {p.Kind} property (for example a deck), which is not measured."
        };

        private ImmutableArray<StoryBand> Stories(out StoryBand? baseBand)
        {
            baseBand = null;
            var ordered = raw.Stories.Stories.OrderBy(x => x.Elevation).ToArray();
            var bands = new List<StoryBand>();
            var lower = raw.Stories.BaseElevation * f;
            foreach (var s in ordered)
            {
                bands.Add(new(s.Name, lower, s.Elevation * f));
                lower = s.Elevation * f;
            }
            // ETABS names the base level (normally "Base") but GetStories_2 does not list it. Objects ETABS places on it
            // get a thin band just below the base elevation, so they are reported under that name instead of Unallocated.
            var declared = bands.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var baseZ = raw.Stories.BaseElevation * f;
            var onBase = raw.Frames.Where(x => x.Story is not null && !declared.Contains(x.Story))
                .Select(x => (x.Story!, Math.Max(x.Point1.Z, x.Point2.Z) * f))
                .Concat(raw.Areas.Where(x => x.Story is not null && !declared.Contains(x.Story)).Select(x => (x.Story!, x.Boundary.Max(p => p.Z) * f)))
                .GroupBy(x => x.Item1, StringComparer.Ordinal).ToArray();
            var candidate = onBase.FirstOrDefault(g => g.All(x => x.Item2 <= baseZ + LinearToleranceM));
            if (candidate is not null && bands.Count > 0)
            {
                baseBand = new(candidate.Key, baseZ - BaseBandDepthM, baseZ);
                bands.Insert(0, baseBand);
            }
            return [.. bands];
        }

        private string? StoryFor(string id, string? story, HashSet<string> declared, bool inScope)
        {
            if (story is not null && declared.Contains(story)) return story;
            if (inScope)
                Warn(id, "ETABS_STORY_UNDECLARED", story is null
                    ? "ETABS did not report a story for this object. Its story is taken from its elevation."
                    : $"ETABS assigns this object to story '{story}', which is not among the stories read from ETABS. Its story is taken from its elevation.");
            return null;
        }

        // Label@Story is how engineers find an object in ETABS; the unique name is the fallback when either is missing or the pair repeats.
        private Dictionary<(string, string), string> ObjectIds()
        {
            var proposed = raw.Frames.Select(x => (Key: ("frame", x.Name), Id: Readable(x.Label, x.Story)))
                .Concat(raw.Areas.Where(x => !x.IsOpening).Select(x => (Key: ("area", x.Name), Id: Readable(x.Label, x.Story)))).ToArray();
            var repeated = proposed.Where(x => x.Id is not null).GroupBy(x => x.Id!, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
            var result = proposed.ToDictionary(x => x.Key, x => x.Id is { } readable && !repeated.Contains(readable) ? readable : $"{x.Key.Item1}:{x.Key.Item2}");
            foreach (var opening in raw.Areas.Where(x => x.IsOpening)) result[("area", opening.Name)] = $"opening:{opening.Name}";
            return result;
            static string? Readable(string? label, string? story) =>
                string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(story) ? null : $"{label}@{story}";
        }

        // An ETABS opening is a separate area object that voids whatever it overlaps in the same plane. It is deducted from
        // every slab or wall it overlaps in that plane; the engine then clips it to each host and warns where it crosses an edge.
        private Dictionary<string, List<RawArea>> AssignOpenings(RawArea[] openings, Dictionary<(string, string), string> ids, out List<string> orphans)
        {
            orphans = [];
            var result = new Dictionary<string, List<RawArea>>(StringComparer.Ordinal);
            if (openings.Length == 0) return result;
            var hosts = new List<(RawArea Area, PlanarGeometry Plane, (double, double, double, double, double, double) Box)>();
            foreach (var ar in raw.Areas.Where(x => !x.IsOpening && x.Boundary.Length >= 3))
                try { hosts.Add((ar, Plane(ar.Boundary), Box(ar.Boundary))); }
                catch (ArgumentException) { /* Invalid host geometry is reported when the host itself is calculated. */ }
            foreach (var opening in openings)
            {
                var box = Box(opening.Boundary);
                var found = false;
                foreach (var host in hosts.Where(h => Overlaps(h.Box, box)))
                {
                    Clipper2Lib.PathD ring;
                    try { ring = host.Plane.ProjectRing([.. opening.Boundary.Select(P)], 1, policy); }
                    catch (ArgumentException) { continue; } // not in the host's plane
                    var overlap = Math.Abs(Clipper2Lib.Clipper.Area(Clipper2Lib.Clipper.Intersect(host.Plane.Gross, [ring], Clipper2Lib.FillRule.NonZero, PlanarGeometry.Precision)));
                    if (overlap <= LinearToleranceM * LinearToleranceM) continue;
                    (result.TryGetValue(host.Area.Name, out var list) ? list : result[host.Area.Name] = []).Add(opening);
                    found = true;
                }
                if (!found) orphans.Add(Reference("area", opening.Name, opening.Label, opening.Story, null, null));
            }
            return result;
        }

        private PlanarGeometry Plane(ImmutableArray<RawPoint> boundary) => new(new AreaSnapshot
        {
            ObjectId = "host", SourceReference = "host", Kind = AreaKind.Slab, Material = MaterialKind.Concrete, Boundary = [.. boundary.Select(P)],
            Openings = [], OpeningsVerified = true, PhysicalThicknessVerified = true, Thickness = M(1)
        }, LengthUnit.Metre, policy);

        private (double, double, double, double, double, double) Box(ImmutableArray<RawPoint> points) =>
            (points.Min(p => p.X) * f, points.Max(p => p.X) * f, points.Min(p => p.Y) * f, points.Max(p => p.Y) * f, points.Min(p => p.Z) * f, points.Max(p => p.Z) * f);

        private static bool Overlaps((double, double, double, double, double, double) a, (double, double, double, double, double, double) b) =>
            a.Item1 <= b.Item2 + PlanarityToleranceM && b.Item1 <= a.Item2 + PlanarityToleranceM &&
            a.Item3 <= b.Item4 + PlanarityToleranceM && b.Item3 <= a.Item4 + PlanarityToleranceM &&
            a.Item5 <= b.Item6 + PlanarityToleranceM && b.Item5 <= a.Item6 + PlanarityToleranceM;

        private void Steel(Dictionary<string, string> frameIds, Dictionary<string, double> lengths, Dictionary<string, FrameKind> kinds,
            Dictionary<string, string> frameSections, List<ElementMetadata> metadata, List<SteelInput> output)
        {
            var approvedBy = options.SteelApprovedBy?.Trim() ?? "";
            var code = raw.Design.Code ?? "unknown code";
            bool Is(string frame, FrameKind kind) => kinds.TryGetValue(frame, out var k) && k == kind;
            SteelInput Input(string frame, string component, SteelMethod method, string reference, string assumption) => new()
            {
                ObjectId = frameIds[frame], Component = component, Method = method, SourceReference = reference,
                ApprovedBy = approvedBy, ApprovedAt = raw.ExtractedAt, Assumption = assumption
            };

            foreach (var beam in raw.Design.Beams.Where(x => Is(x.Frame, FrameKind.Beam)))
            {
                var id = frameIds[beam.Frame];
                var (evidence, why) = Evidence(beam.Errors, beam.DesignSection, frameSections[beam.Frame]);
                if (why is not null) Warn(id, "ETABS_DESIGN_NOT_CURRENT", why);
                var reference = $"ETABS DesignConcrete.GetSummaryResultsBeam, frame object '{beam.Frame}', {beam.Location.Length} stations, {code}";
                void Demand(string component, ImmutableArray<double> areas, string what) =>
                    output.Add(DemandInput(Input(beam.Frame, component, SteelMethod.DemandEquivalent, reference,
                        $"ETABS required {what} area ({code}) at the design stations, integrated along the member. A design demand equivalent, not provided bars: laps, anchorage, curtailment and detailing are not included."),
                        evidence, beam.Location, areas));
                Demand("LongitudinalTop", beam.TopArea, "top flexural longitudinal");
                Demand("LongitudinalBottom", beam.BottomArea, "bottom flexural longitudinal");
                if (beam.TorsionLongitudinalArea.Any(x => x > 0))
                {
                    var index = metadata.FindIndex(x => x.ObjectId == id);
                    metadata[index] = metadata[index] with { RequiredSteelComponents = [.. metadata[index].RequiredSteelComponents.Append("LongitudinalTorsion").Order(StringComparer.Ordinal)] };
                    Demand("LongitudinalTorsion", beam.TorsionLongitudinalArea, "torsional longitudinal");
                }
            }
            foreach (var column in raw.Design.Columns.Where(x => Is(x.Frame, FrameKind.Column)))
            {
                var id = frameIds[column.Frame];
                if (column.Option == 2)
                {
                    var (evidence, why) = Evidence(column.Errors, column.DesignSection, frameSections[column.Frame]);
                    if (why is not null) Warn(id, "ETABS_DESIGN_NOT_CURRENT", why);
                    output.Add(DemandInput(Input(column.Frame, "Longitudinal", SteelMethod.DemandEquivalent,
                        $"ETABS DesignConcrete.GetSummaryResultsColumn (design mode), frame object '{column.Frame}', {column.Location.Length} stations, {code}",
                        $"ETABS required PMM longitudinal area ({code}) at the design stations, integrated along the member. A design demand equivalent, not provided bars: laps, splices and detailing are not included."),
                        evidence, column.Location, column.PmmArea));
                }
            }
            // A column section whose reinforcement ETABS checks (rather than designs) models specific bars: count them as
            // straight bars over the column length. A section still "to be designed" has no provided bars, so nothing is taken.
            var supplied = output.Select(x => x.ObjectId).ToHashSet(StringComparer.Ordinal);
            foreach (var fr in raw.Frames.Where(x => Is(x.Name, FrameKind.Column) && !supplied.Contains(frameIds[x.Name])))
            {
                if (!sections.TryGetValue(fr.Section, out var section) || section.ColumnRebar is not { ToBeDesigned: false } rebar) continue;
                var id = frameIds[fr.Name];
                var count = rebar.Pattern == 2 ? rebar.NumberCBars : 2 * (rebar.NumberR3Bars + rebar.NumberR2Bars) - 4;
                if (count <= 0 || rebar.BarArea is not > 0)
                {
                    Warn(id, "ETABS_COLUMN_BARS_UNREADABLE", $"Section '{section.Name}' is set to check modeled bars, but the bar count or size ({rebar.RebarSize}) could not be read, so its longitudinal steel is unknown.");
                    continue;
                }
                output.Add(Input(fr.Name, "Longitudinal", SteelMethod.AssignedBars,
                    $"ETABS PropFrame.GetRebarColumn, section '{section.Name}' (reinforcement to be checked): {count} × {rebar.RebarSize}",
                    $"Modeled column bars from the ETABS section ({count} × {rebar.RebarSize}, {F(rebar.BarArea.Value * f * f * 1e6)} mm² each) as straight bars over the modeled column length. Laps, splices, anchorage and ties are not included.")
                    with { Bars = [new StraightBar(count, rebar.BarArea.Value * f * f, lengths[fr.Name])] });
                Warn(id, "ETABS_COLUMN_BARS_MODELED", $"Longitudinal steel is the ETABS section's modeled bars ({count} × {rebar.RebarSize}) over the column length, not a design result or a bar schedule.");
            }
        }

        // Only a locked model's results can be current: ETABS deletes design results when the model is unlocked and edited.
        private (DesignEvidence, string?) Evidence(ImmutableArray<string> errors, string? designSection, string analysisSection)
        {
            var real = errors.Where(e => !string.IsNullOrWhiteSpace(e) && !e.Trim().Equals("No Message", StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
            if (real.Length > 0) return (DesignEvidence.Failed, $"ETABS design reports errors: {string.Join("; ", real)}. Its reinforcement areas are not counted.");
            if (designSection is not null && designSection != analysisSection)
                return (DesignEvidence.Stale, $"The ETABS design section '{designSection}' differs from the analysis section '{analysisSection}', so the design results do not describe the modeled member.");
            if (!raw.Model.ModelLocked) return (DesignEvidence.Stale, "The ETABS model is unlocked, so its design results may not match the current model.");
            return (DesignEvidence.VerifiedCurrent, null);
        }

        private SteelInput DemandInput(SteelInput input, DesignEvidence evidence, ImmutableArray<double> locations, ImmutableArray<double> areas)
        {
            // ETABS can report two results at one location (for example either side of a support face); the larger governs.
            var stations = locations.Zip(areas, (l, a) => (Position: l * f, Area: a * f * f))
                .GroupBy(x => Math.Round(x.Position, 9)).Select(g => new DemandStation(g.Min(x => x.Position), g.Max(x => x.Area)))
                .OrderBy(x => x.PositionM).ToImmutableArray();
            var gap = stations.Length < 2 ? 0 : Enumerable.Range(1, stations.Length - 1).Max(i => stations[i].PositionM - stations[i - 1].PositionM);
            if (gap > options.MaximumStationGapM + 1e-9)
                Warn(input.ObjectId, "ETABS_STATION_GAP", $"ETABS design stations for {input.Component} are up to {F(gap)} m apart, more than the {F(options.MaximumStationGapM)} m allowed, so that steel is not counted. " +
                    "Add output stations in ETABS, or extract with a larger maximum station gap if that spacing is acceptable.");
            return input with
            {
                DesignEvidence = evidence, DesignCode = raw.Design.Code ?? "", Stations = stations,
                DomainStartM = stations.Length > 0 ? stations[0].PositionM : null, DomainEndM = stations.Length > 0 ? stations[^1].PositionM : null,
                MaximumStationGapM = options.MaximumStationGapM
            };
        }

        private void RunWarnings(StoryBand? baseBand, bool hasAreas, bool hasWalls, int steelInputs)
        {
            var m = raw.Model;
            Warn(null, "ETABS_SOURCE", $"Extracted from ETABS {m.ProgramVersion} (build {m.ProgramBuild}), model {m.ModelPath}, present units {m.PresentUnits} converted to metres; model {(m.ModelLocked ? "locked" : "unlocked")}. " +
                "Quantities come from ETABS model objects, not analysis mesh elements.");
            if (EtabsDiscovery.Classify(m.ProgramBuild) != EtabsSupport.TestedSupported)
                Warn(null, "ETABS_UNTESTED_VERSION", $"ETABS {m.ProgramBuild} is not a tested version (tested: ETABS {EtabsDiscovery.TestedVersionLabel}). Check the extraction against the model before relying on it.");
            if (m.Towers.Length > 1)
                Warn(null, "ETABS_MULTIPLE_TOWERS", $"The model has {m.Towers.Length} towers ({string.Join(", ", m.Towers)}). Story bands come from the active tower only; objects in other towers are allocated by elevation.");
            if (baseBand is not null)
                Warn(null, "ETABS_BASE_STORY", $"Objects on the ETABS base level are reported under story '{baseBand.Id}' (a {F(BaseBandDepthM * 1000)} mm band just below the base elevation {F(baseBand.UpperElevationM)} m).");
            if (!raw.Design.ResultsAvailable)
                Warn(null, "ETABS_DESIGN_UNAVAILABLE", $"ETABS has no concrete frame design results (design code {raw.Design.Code ?? "not set"}). Beam and column design reinforcement is unknown, not zero; run concrete frame design in ETABS and extract again to include it.");
            if (steelInputs > 0 && string.IsNullOrWhiteSpace(options.SteelApprovedBy))
                Warn(null, "ETABS_STEEL_UNAPPROVED", "ETABS reinforcement evidence was read but no one approved its use for this takeoff, so it is not counted. Extract again with a named steel approver to count it.");
            // Verified on ETABS 22.7: building the analysis model replaces a wall that contains an opening with wall objects
            // around the opening, and deletes the opening object, permanently (it survives unlocking and saving).
            if (hasWalls)
                Warn(null, "ETABS_WALL_OPENING_GAPS", "When ETABS builds its analysis model it replaces a wall containing an opening with separate wall objects around the opening. " +
                    "In a model that has been analysed, such wall openings are gaps between wall objects: they are excluded from both gross and opening-adjusted wall volumes, and are not listed as openings.");
            if (hasAreas)
                Warn(null, "ETABS_AREA_STEEL_NOT_EXTRACTED", "Slab and wall reinforcement is not extracted from ETABS in this version (pier, spandrel and slab design results are not read). Their steel is unknown, not zero.");
        }

        private static string Vec(RawPoint p) => string.Create(CultureInfo.InvariantCulture, $"({p.X:G6}, {p.Y:G6}, {p.Z:G6})");
        private static double Distance(Point3 a, Point3 b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y) + (b.Z - a.Z) * (b.Z - a.Z));
        private static string Names(IReadOnlyList<string> items) => items.Count <= 20 ? string.Join("; ", items) : string.Join("; ", items.Take(20)) + $" and {items.Count - 20} more";

        private static string Reference(string type, string name, string? label, string? story, string? guid, string? detail) =>
            $"ETABS {type} object '{name}'" + (label is null ? "" : $", label {label}") + (story is null ? "" : $", story {story}") +
            (string.IsNullOrWhiteSpace(guid) ? "" : $", GUID {guid}") + (detail is null ? "" : $", {detail}");
    }

    public static string ModelId(string path) => Path.GetFileNameWithoutExtension(path.Replace('\\', Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : "etabs-model";
}
