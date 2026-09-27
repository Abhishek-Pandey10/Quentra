using System.Collections.Immutable;

namespace Quentra.Etabs;

// The raw capture: values exactly as the ETABS API returned them, in ETABS present units, before any
// interpretation. It is saved beside every extracted snapshot and hashed into it, so each snapshot value
// can be traced back to the API data it came from, and the mapping can be re-run offline ('quentra etabs build').
public sealed record RawUnits(string Force, string Length, string Temperature)
{
    public override string ToString() => $"{Force}, {Length}, {Temperature}";
}

public sealed record RawPoint(double X, double Y, double Z);

public sealed record RawModelInfo
{
    public required string Program { get; init; }
    public required string ProgramVersion { get; init; }      // as SapModel.GetVersion reports it, e.g. 22.7.0
    public required string ProgramBuild { get; init; }        // ETABS.exe file version, e.g. 22.7.0.4095
    public required string ProgramPath { get; init; }
    public required string ApiAssembly { get; init; }         // the ETABSv1.dll actually loaded
    public required string ApiAssemblyVersion { get; init; }
    public required int ProcessId { get; init; }
    public required string ModelPath { get; init; }
    public string? ModelFileSha256 { get; init; }
    public DateTimeOffset? ModelFileModifiedAt { get; init; }
    public required bool ModelLocked { get; init; }
    public required RawUnits PresentUnits { get; init; }
    public required RawUnits DatabaseUnits { get; init; }
    public required ImmutableArray<string> Towers { get; init; }
}

public sealed record RawStory(string Name, double Elevation, double Height, bool IsMaster, string SimilarTo);
public sealed record RawStories(double BaseElevation, ImmutableArray<RawStory> Stories);

// Type is the ETABS eMatType name (Concrete, Steel, Rebar, ...). Strengths are in present force/length².
public sealed record RawMaterial(string Name, string Type, double? ConcreteStrength, double? RebarYield, double? WeightPerVolume, double? MassPerVolume);

public sealed record RawColumnRebar(string LongitudinalMaterial, int Pattern, double Cover, int NumberCBars, int NumberR3Bars,
    int NumberR2Bars, string RebarSize, double? BarArea, bool ToBeDesigned);

// Type is the ETABS eFramePropType name. Depth is t3, Width is t2, Diameter is t3 of a circle; Area from GetSectProps.
public sealed record RawFrameSection(string Name, string Type, string? Material, double? Depth, double? Width, double? Diameter,
    double? Area, RawColumnRebar? ColumnRebar);

// Kind is Slab, Wall or Other (deck or anything neither GetSlab nor GetWall reads); SubType is the ETABS slab
// or wall property type (Slab, Drop, Ribbed, Waffle, Mat, Footing, Specified, AutoSelectList).
public sealed record RawAreaProperty(string Name, string Kind, string? SubType, string? ShellType, string? Material, double? Thickness);

public sealed record RawFrame
{
    public required string Name { get; init; }
    public string? Label { get; init; }
    public string? Story { get; init; }
    public string? Guid { get; init; }
    public required string Section { get; init; }
    public required RawPoint Point1 { get; init; }
    public required RawPoint Point2 { get; init; }
    public required RawPoint Offset1 { get; init; }
    public required RawPoint Offset2 { get; init; }
    public required int CardinalPoint { get; init; }
    public required double Angle { get; init; }
    public required string DesignOrientation { get; init; }
    public int? DesignProcedure { get; init; }      // not read by the live reader (unused, and costly per object on large models)
    public string? MaterialOverwrite { get; init; }
    public int? CurveType { get; init; }            // GetCurved_2 curve type: 0 straight (the call returns non-zero for straight frames)
}

public sealed record RawArea
{
    public required string Name { get; init; }
    public string? Label { get; init; }
    public string? Story { get; init; }
    public string? Guid { get; init; }
    public required string Property { get; init; }
    public required string DesignOrientation { get; init; }
    public required bool IsOpening { get; init; }
    public required ImmutableArray<RawPoint> Boundary { get; init; }
    public string? MaterialOverwrite { get; init; }
    public required bool HasCurvedEdges { get; init; }
}

// One design station per array index, as GetSummaryResultsBeam returns them; areas in L², locations in L.
public sealed record RawBeamDesign(string Frame, ImmutableArray<double> Location, ImmutableArray<double> TopArea, ImmutableArray<double> BottomArea,
    ImmutableArray<double> ShearAreaPerLength, ImmutableArray<double> TorsionLongitudinalArea, ImmutableArray<double> TorsionTransverseAreaPerLength,
    ImmutableArray<string> Errors, ImmutableArray<string> Warnings, string? DesignSection);

// Option 1 = check (PmmRatio valid), 2 = design (PmmArea valid).
public sealed record RawColumnDesign(string Frame, int Option, ImmutableArray<double> Location, ImmutableArray<double> PmmArea, ImmutableArray<double> PmmRatio,
    ImmutableArray<string> Errors, ImmutableArray<string> Warnings, string? DesignSection);

public sealed record RawDesign(bool ResultsAvailable, string? Code, ImmutableArray<RawBeamDesign> Beams, ImmutableArray<RawColumnDesign> Columns);

// A failed ETABS API call: which operation, object and method, the return code, and what it means for quantities.
public sealed record ApiIssue(string Operation, string? ObjectName, string Method, int? ReturnCode, string? Exception, string Consequence);

public sealed record EtabsRawModel
{
    public const string CurrentFormat = "quentra-etabs-raw/1";
    public required string Format { get; init; }
    public required string ExtractorVersion { get; init; }
    public required DateTimeOffset ExtractedAt { get; init; }
    public required RawModelInfo Model { get; init; }
    public required RawStories Stories { get; init; }
    public required ImmutableArray<RawMaterial> Materials { get; init; }
    public required ImmutableArray<RawFrameSection> FrameSections { get; init; }
    public required ImmutableArray<RawAreaProperty> AreaProperties { get; init; }
    public required ImmutableArray<RawFrame> Frames { get; init; }
    public required ImmutableArray<RawArea> Areas { get; init; }
    public required RawDesign Design { get; init; }
    public required ImmutableArray<ApiIssue> Issues { get; init; }
}
