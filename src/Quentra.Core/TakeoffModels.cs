using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Quentra.Core;

public enum AreaKind { Slab, Wall, Unsupported }
public enum SteelMethod { DemandEquivalent, VolumeFraction, KgPerCubicMetre, AssignedBars, DistributedIntensity }
public enum DesignEvidence { Missing, Stale, Failed, CheckMode, VerifiedCurrent }
public enum ConcreteBasis { GrossModeled, OpeningAdjusted }

public sealed record AreaSnapshot
{
    public required string ObjectId { get; init; }
    public required string SourceReference { get; init; }
    public required AreaKind Kind { get; init; }
    public required MaterialKind Material { get; init; }
    public required ImmutableArray<Point3> Boundary { get; init; }
    // Host assignment is explicit, never inferred solely from intersecting geometry.
    public required ImmutableArray<ImmutableArray<Point3>> Openings { get; init; }
    public required bool OpeningsVerified { get; init; }
    public required bool PhysicalThicknessVerified { get; init; }
    public required SourceLength Thickness { get; init; }
}

public sealed record StoryBand(string Id, double LowerElevationM, double UpperElevationM);
public sealed record ElementMetadata(string ObjectId, string MaterialName, string SectionName,
    string? AssignedStoryId, ImmutableArray<string> RequiredSteelComponents);
public sealed record DemandStation(double PositionM, double AreaM2);
public sealed record StraightBar(int Count, double AreaM2, double LengthM);

public sealed record SteelInput
{
    public required string ObjectId { get; init; }
    public required string Component { get; init; }
    public required SteelMethod Method { get; init; }
    public required string SourceReference { get; init; }
    public required string ApprovedBy { get; init; }
    public required DateTimeOffset ApprovedAt { get; init; }
    public required string Assumption { get; init; }
    public ImmutableArray<string> CoversComponents { get; init; } = [];
    public ConcreteBasis ConcreteBasis { get; init; } = ConcreteBasis.OpeningAdjusted;
    public double? Ratio { get; init; }
    public DesignEvidence DesignEvidence { get; init; } = DesignEvidence.Missing;
    public string? DesignCode { get; init; }
    public double? DomainStartM { get; init; }
    public double? DomainEndM { get; init; }
    public double? MaximumStationGapM { get; init; }
    public ImmutableArray<DemandStation> Stations { get; init; } = [];
    public ImmutableArray<StraightBar> Bars { get; init; } = [];
    // Sum over explicitly described faces/directions, in m²/m, never a strip total.
    public double? IntensityM2PerM { get; init; }
}

public sealed record TakeoffPolicy
{
    public required string Id { get; init; }
    public required string IntendedUse { get; init; }
    public required string? ApprovedBy { get; init; }
    public required DateTimeOffset? ApprovedAt { get; init; }
    public required double SteelDensityKgM3 { get; init; }
    public required double LinearToleranceM { get; init; }
    public required double PlanarityToleranceM { get; init; }
}

// A condition the extractor found in the source model (for example an unsupported ETABS section type).
// The engine reports it with the named element, or with the run when objectId is null, so it is reviewed
// and acknowledged like any other warning.
public sealed record SourceWarning(string? ObjectId, string Code, string Message);

// Where an extracted snapshot came from. Recorded for traceability; the engine does not use it.
public sealed record SnapshotSource
{
    public required string Program { get; init; }
    public required string ProgramVersion { get; init; }
    public required string ProgramBuild { get; init; }
    public required string ApiAssembly { get; init; }
    public required string ApiAssemblyVersion { get; init; }
    public required string ModelPath { get; init; }
    public string? ModelFileSha256 { get; init; }
    public DateTimeOffset? ModelFileModifiedAt { get; init; }
    public required bool ModelLocked { get; init; }
    public required string PresentUnits { get; init; }
    public required string Extractor { get; init; }
    public required string ExtractorVersion { get; init; }
    public required DateTimeOffset ExtractedAt { get; init; }
    public required string RawCaptureSha256 { get; init; }
}

// Schema 2 adds area objects, physical story bands, explicit component scope and policy.
// Legacy schema 1 snapshots/runs remain handled by the original replay engine.
// Source and SourceWarnings are optional and omitted when absent, so snapshots without them keep their hashes.
public sealed record TakeoffSnapshot
{
    public required int SchemaVersion { get; init; }
    public required ModelSnapshot Model { get; init; }
    public required ImmutableArray<AreaSnapshot> Areas { get; init; }
    public required ImmutableArray<StoryBand> Stories { get; init; }
    public required ImmutableArray<ElementMetadata> Metadata { get; init; }
    public required ImmutableArray<SteelInput> Reinforcement { get; init; }
    public required TakeoffPolicy Policy { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SnapshotSource? Source { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<SourceWarning> SourceWarnings { get; init; }
}

public sealed record ElementTakeoff(string ObjectId, string Category, string MaterialName,
    string SectionName, QuantityStatus Status, string SourceReference,
    double? GrossM3, double? OpeningDeductionM3, double? OpeningAdjustedM3,
    double? SurfaceAreaM2, double? AxisLengthM, string Formula,
    ImmutableArray<CalculationWarning> Warnings);
public sealed record StoryQuantity(string ObjectId, string StoryId, double GrossM3, double OpeningAdjustedM3);
public sealed record SteelQuantity(string ObjectId, string Component, string Evidence,
    double? MassKg, ImmutableArray<string> CoversComponents, string SourceReference,
    string Formula, string Assumption, string ApprovedBy, DateTimeOffset ApprovedAt,
    ImmutableArray<CalculationWarning> Warnings);
public sealed record SteelCoverage(string ObjectId, ImmutableArray<string> Required,
    ImmutableArray<string> Missing, double KnownMassKg, double? CompleteMassKg);
public sealed record GroupQuantity(string Group, int Count, int QuantifiedCount,
    double KnownGrossM3, double KnownOpeningAdjustedM3, double? CompleteOpeningAdjustedM3);
public sealed record TakeoffSummary(int Found, int InScope, int Quantified, int Unsupported,
    int Invalid, int OutOfScope, double KnownGrossM3, double KnownOpeningAdjustedM3,
    double? CompleteGrossM3, double? CompleteOpeningAdjustedM3,
    double KnownSteelKg, double? CompleteSteelKg, int MissingSteelComponents);
public sealed record TakeoffResult(string CalculationVersion, ImmutableArray<ElementTakeoff> Elements,
    ImmutableArray<StoryQuantity> StoryAllocations, ImmutableArray<SteelQuantity> Steel,
    ImmutableArray<SteelCoverage> SteelCoverage, TakeoffSummary Summary,
    ImmutableArray<GroupQuantity> ByCategory, ImmutableArray<GroupQuantity> ByMaterial,
    ImmutableArray<CalculationWarning> Warnings);
