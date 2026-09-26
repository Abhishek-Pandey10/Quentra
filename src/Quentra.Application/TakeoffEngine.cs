using System.Collections.Immutable;
using Quentra.Core;

namespace Quentra.Application;

public static class TakeoffEngine
{
    public const string Version = "takeoff/0.3.0";
    public static readonly string[] ComponentNames = ["Longitudinal", "LongitudinalTop", "LongitudinalBottom", "Transverse", "Web", "Boundary", "XTop", "XBottom", "YTop", "YBottom", "Detailing", "Accessories"];

    public static TakeoffResult Run(TakeoffSnapshot snapshot, CancellationToken cancellationToken = default, IReadOnlySet<string>? excluded = null)
    {
        Validate(snapshot);
        var metadata = snapshot.Metadata.ToDictionary(x => x.ObjectId, StringComparer.Ordinal);
        var elements = new List<ElementTakeoff>();
        var stories = new List<StoryQuantity>();
        var warnings = new List<CalculationWarning>
        {
            new("MODELED_BASIS", "Gross and opening-adjusted modeled volumes retain member intersections. Net, BOQ and procurement quantities are unavailable."),
            new("DECLARED_SCOPE", "Completeness applies only to the declared snapshot scope and required steel components. Unmodeled elements and bars are not inferred.")
        };
        if (snapshot.Model.Origin == SnapshotOrigin.Synthetic)
            warnings.Add(new("SYNTHETIC_INPUT", "Illustrative source data; no ETABS compatibility or engineering approval is established."));
        if (snapshot.Policy.ApprovedAt is null || string.IsNullOrWhiteSpace(snapshot.Policy.ApprovedBy))
            warnings.Add(new("POLICY_PENDING", "Measurement policy has not been approved; report acceptance is blocked."));
        if (Plausibility.Policy(snapshot.Policy) is { } policyWarning) warnings.Add(policyWarning);
        foreach (var source in snapshot.Model.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var q = FrameCalculator.Calculate(source, snapshot.Model.CoordinateUnit);
            if (excluded?.Contains(source.ObjectId) == true)
                q = q with { Status = QuantityStatus.OutOfScope, GrossModeledVolume = null, Trace = null, Warnings = [new("USER_EXCLUDED", "Explicitly excluded by a recorded override; see the override ledger.")] };
            var m = metadata[source.ObjectId];
            var element = new ElementTakeoff(source.ObjectId, source.Kind.ToString(), m.MaterialName, m.SectionName,
                q.Status, source.SourceReference, q.GrossModeledVolume?.CubicMetres,
                q.GrossModeledVolume is null ? null : 0, q.GrossModeledVolume?.CubicMetres, null,
                q.Trace?.AxisLength.Metres, q.Trace?.Formula ?? "Unavailable",
                q.Warnings.Where(x => x.Code != "STEEL_UNKNOWN").ToImmutableArray());
            if (element.Status == QuantityStatus.Quantified)
                element = element with { Warnings = element.Warnings.AddRange(Plausibility.Frame(source, q.Trace!.AxisLength.Metres)) };
            elements.Add(element);
            if (element.Status == QuantityStatus.Quantified)
                AllocateFrame(source, element, m, snapshot, stories);
        }
        foreach (var source in snapshot.Areas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var m = metadata[source.ObjectId];
            var status = QuantityStatus.Quantified;
            string? error = null, code = null;
            if (excluded?.Contains(source.ObjectId) == true) { status = QuantityStatus.OutOfScope; code = "USER_EXCLUDED"; error = "Explicitly excluded by a recorded override; see the override ledger."; }
            else if (source.Material == MaterialKind.NonConcrete) { status = QuantityStatus.OutOfScope; code = "NON_CONCRETE"; error = "Non-concrete area is outside RC scope."; }
            else if (source.Material == MaterialKind.Unknown || source.Kind == AreaKind.Unsupported || !source.PhysicalThicknessVerified || !source.OpeningsVerified)
            { status = QuantityStatus.Unsupported; error = "Verify physical thickness, material, planar area role and opening host association."; }
            PlanarGeometry? geometry = null;
            double thickness = 0;
            if (error is null)
            {
                try
                {
                    thickness = new Length(source.Thickness.Metres).Metres;
                    geometry = new PlanarGeometry(source, snapshot.Model.CoordinateUnit, snapshot.Policy);
                    _ = new Volume(geometry.GrossArea * thickness);
                }
                catch (ArgumentException ex) { status = QuantityStatus.Invalid; error = ex.Message; }
            }
            var element = new ElementTakeoff(source.ObjectId, source.Kind.ToString(), m.MaterialName, m.SectionName,
                status, source.SourceReference, error is null ? geometry!.GrossArea * thickness : null,
                error is null ? Math.Max(0, geometry!.GrossArea - geometry.RemainingArea) * thickness : null,
                error is null ? geometry!.RemainingArea * thickness : null,
                error is null ? geometry!.RemainingArea : null, null,
                "true planar area minus union of clipped openings, multiplied by uniform normal thickness",
                error is null ? AreaWarnings(source, geometry!, thickness, snapshot.Model.CoordinateUnit) : [new(code ?? "AREA_" + status.ToString().ToUpperInvariant(), error)]);
            elements.Add(element);
            if (element.Status == QuantityStatus.Quantified)
                AllocateArea(source, element, m, geometry!, thickness, snapshot, stories);
        }
        var ordered = elements.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ToImmutableArray();
        var byId = ordered.ToDictionary(x => x.ObjectId);
        var steel = snapshot.Reinforcement.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Component, StringComparer.Ordinal)
            .Select(x => SteelCalculator.Calculate(x, byId[x.ObjectId], snapshot.Policy)).ToImmutableArray();
        var steelByElement = steel.ToLookup(x => x.ObjectId);
        var coverage = ordered.Where(x => x.Status != QuantityStatus.OutOfScope).Select(x =>
        {
            var components = steelByElement[x.ObjectId].ToArray();
            var covered = components.Where(c => c.MassKg is not null).SelectMany(c => c.CoversComponents).ToHashSet(StringComparer.Ordinal);
            var required = metadata[x.ObjectId].RequiredSteelComponents;
            var missing = required.Where(c => !covered.Contains(c)).Order(StringComparer.Ordinal).ToImmutableArray();
            var known = components.Sum(c => c.MassKg ?? 0);
            return new SteelCoverage(x.ObjectId, required, missing, known, missing.Length == 0 ? known : null);
        }).ToImmutableArray();
        var intensity = coverage.ToDictionary(x => x.ObjectId, x => Plausibility.SteelIntensity(x.KnownMassKg, byId[x.ObjectId].OpeningAdjustedM3));
        ordered = ordered.Select(x => intensity.GetValueOrDefault(x.ObjectId) is { } w ? x with { Warnings = x.Warnings.Add(w) } : x).ToImmutableArray();
        var quantified = ordered.Count(x => x.Status == QuantityStatus.Quantified);
        var inScope = ordered.Count(x => x.Status != QuantityStatus.OutOfScope);
        var gross = Finite(ordered.Sum(x => x.GrossM3 ?? 0));
        var opening = Finite(ordered.Sum(x => x.OpeningAdjustedM3 ?? 0));
        var mass = Finite(coverage.Sum(x => x.KnownMassKg));
        var completeConcrete = inScope > 0 && quantified == inScope;
        var completeSteel = inScope > 0 && coverage.All(x => x.CompleteMassKg is not null);
        var summary = new TakeoffSummary(ordered.Length, inScope, quantified,
            ordered.Count(x => x.Status == QuantityStatus.Unsupported), ordered.Count(x => x.Status == QuantityStatus.Invalid),
            ordered.Length - inScope, gross, opening, completeConcrete ? gross : null, completeConcrete ? opening : null,
            mass, completeSteel ? mass : null, coverage.Sum(x => x.Missing.Length));
        if (!completeConcrete) warnings.Add(new("PARTIAL_CONCRETE", "Concrete scope is empty or contains unquantified elements."));
        if (!completeSteel) warnings.Add(new("PARTIAL_STEEL", inScope == 0
            ? "Steel scope is empty; there is no complete steel quantity."
            : "Required steel components are missing. Known steel is only a subtotal."));
        if (stories.Any(x => x.StoryId == "Unallocated")) warnings.Add(new("UNALLOCATED_STORY", "Some geometry is outside declared story bands or lacks a level assignment."));
        DetectDuplicateFrames(snapshot, warnings);
        DetectAreaOverlaps(snapshot, warnings, cancellationToken);
        return new(Version, ordered, stories.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.StoryId, StringComparer.Ordinal).ToImmutableArray(),
            steel, coverage, summary, Groups(ordered, x => x.Category), Groups(ordered, x => x.MaterialName), warnings.ToImmutableArray());
    }

    private static ImmutableArray<CalculationWarning> AreaWarnings(AreaSnapshot source, PlanarGeometry geometry, double thickness, LengthUnit unit) =>
    [
        .. geometry.OpeningsOutsideHost.Select(i => new CalculationWarning("OPENING_OUTSIDE_HOST",
            $"Opening {i + 1} lies entirely outside the host boundary and deducts nothing. Confirm its host assignment.")),
        .. geometry.OpeningsCrossingHost.Select(i => new CalculationWarning("OPENING_CROSSES_HOST",
            $"Opening {i + 1} crosses the host boundary; only the part inside the host is deducted.")),
        .. Plausibility.Area(source, thickness, Units.MetresPerUnit(unit))
    ];

    private static double Finite(double value) => double.IsFinite(value) ? value : throw new ArgumentException("Quantity sum overflowed.");
    private static ImmutableArray<GroupQuantity> Groups(IEnumerable<ElementTakeoff> elements, Func<ElementTakeoff, string> key) =>
        elements.Where(x => x.Status != QuantityStatus.OutOfScope).GroupBy(key).OrderBy(x => x.Key, StringComparer.Ordinal).Select(g =>
            new GroupQuantity(g.Key, g.Count(), g.Count(x => x.Status == QuantityStatus.Quantified), g.Sum(x => x.GrossM3 ?? 0),
                g.Sum(x => x.OpeningAdjustedM3 ?? 0), g.All(x => x.OpeningAdjustedM3 is not null) ? g.Sum(x => x.OpeningAdjustedM3!.Value) : null)).ToImmutableArray();

    private static void AllocateFrame(FrameSnapshot source, ElementTakeoff element, ElementMetadata metadata, TakeoffSnapshot snapshot, List<StoryQuantity> output)
    {
        var factor = Units.MetresPerUnit(snapshot.Model.CoordinateUnit);
        var z0 = source.Start.Z * factor; var z1 = source.End.Z * factor;
        var tolerance = snapshot.Policy.LinearToleranceM;
        if (Math.Abs(z1 - z0) <= tolerance)
        {
            // Unit conversion can place a beam a rounding error above its level; it still tops the story below.
            var story = metadata.AssignedStoryId ?? snapshot.Stories.FirstOrDefault(s => z0 > s.LowerElevationM + tolerance && z0 <= s.UpperElevationM + tolerance)?.Id ?? "Unallocated";
            output.Add(new(element.ObjectId, story, element.GrossM3!.Value, element.OpeningAdjustedM3!.Value));
            return;
        }
        var low = Math.Min(z0, z1); var high = Math.Max(z0, z1);
        var portions = snapshot.Stories.Select(s => (s.Id, Fraction: Math.Max(0, Math.Min(high, s.UpperElevationM) - Math.Max(low, s.LowerElevationM)) / (high - low)));
        double allocated = 0;
        foreach (var (id, fraction) in portions.Where(x => x.Fraction > 0))
        {
            var volume = element.GrossM3!.Value * fraction; allocated += volume;
            output.Add(new(element.ObjectId, id, volume, volume));
        }
        var remainder = element.GrossM3!.Value - allocated;
        if (remainder > 1e-10 * Math.Max(1, element.GrossM3.Value)) output.Add(new(element.ObjectId, "Unallocated", remainder, remainder));
        CheckConservation(element, output);
    }

    private static void AllocateArea(AreaSnapshot source, ElementTakeoff element, ElementMetadata metadata, PlanarGeometry geometry, double thickness, TakeoffSnapshot snapshot, List<StoryQuantity> output)
    {
        if (source.Kind == AreaKind.Slab || Math.Abs(geometry.U.Z) + Math.Abs(geometry.V.Z) < 1e-12)
        {
            var story = metadata.AssignedStoryId ?? "Unallocated";
            output.Add(new(element.ObjectId, story, element.GrossM3!.Value, element.OpeningAdjustedM3!.Value));
            return;
        }
        double gross = 0, opening = 0;
        foreach (var s in snapshot.Stories)
        {
            var slice = geometry.AreaInBand(s.LowerElevationM, s.UpperElevationM);
            if (slice.Gross <= 0) continue;
            var g = slice.Gross * thickness; var o = slice.Remaining * thickness;
            output.Add(new(element.ObjectId, s.Id, g, o)); gross += g; opening += o;
        }
        var remainingGross = element.GrossM3!.Value - gross; var remainingOpening = element.OpeningAdjustedM3!.Value - opening;
        if (remainingGross > 1e-7 * Math.Max(1, element.GrossM3.Value))
            output.Add(new(element.ObjectId, "Unallocated", Math.Max(0, remainingGross), Math.Max(0, remainingOpening)));
        CheckConservation(element, output);
    }

    private static void CheckConservation(ElementTakeoff element, List<StoryQuantity> allocations)
    {
        var rows = allocations.Where(x => x.ObjectId == element.ObjectId).ToArray();
        if (Math.Abs(rows.Sum(x => x.GrossM3) - element.GrossM3!.Value) > 1e-6 * Math.Max(1, element.GrossM3.Value) ||
            Math.Abs(rows.Sum(x => x.OpeningAdjustedM3) - element.OpeningAdjustedM3!.Value) > 1e-6 * Math.Max(1, element.OpeningAdjustedM3.Value))
            throw new ArgumentException($"Story allocation does not conserve volume for {element.ObjectId}.");
    }

    private static void DetectDuplicateFrames(TakeoffSnapshot snapshot, List<CalculationWarning> warnings)
    {
        // Candidate detection does not silently remove modeled objects.
        var groups = snapshot.Model.Frames.GroupBy(f => string.Join("|", new[] { f.Start.ToString(), f.End.ToString() }.Order(StringComparer.Ordinal)));
        foreach (var group in groups.Where(x => x.Count() > 1))
            warnings.Add(new("COINCIDENT_FRAMES", $"Coincident frame axes: {string.Join(", ", group.Select(x => x.ObjectId))}. Review physical duplication; all modeled records are retained."));
    }

    private static void DetectAreaOverlaps(TakeoffSnapshot snapshot, List<CalculationWarning> warnings, CancellationToken token)
    {
        // Bounding boxes prune candidates; an exact planar intersection establishes overlap.
        var valid = new List<(AreaSnapshot Source, PlanarGeometry Plane, double MinX, double MaxX)>();
        var factor = Units.MetresPerUnit(snapshot.Model.CoordinateUnit);
        foreach (var area in snapshot.Areas.Where(x => x.Material == MaterialKind.Concrete && x.PhysicalThicknessVerified && x.OpeningsVerified))
            try { valid.Add((area, new PlanarGeometry(area, snapshot.Model.CoordinateUnit, snapshot.Policy), area.Boundary.Min(x => x.X) * factor, area.Boundary.Max(x => x.X) * factor)); }
            catch (ArgumentException) { /* Invalid geometry is already reported in its quantity record. */ }
        valid.Sort((a, b) => a.MinX.CompareTo(b.MinX));
        var candidates = 0;
        for (var i = 0; i < valid.Count; i++)
            for (var j = i + 1; j < valid.Count && valid[j].MinX <= valid[i].MaxX; j++)
            {
                token.ThrowIfCancellationRequested();
                if (++candidates > 100000)
                {
                    warnings.Add(new("OVERLAP_SCAN_LIMIT", "Area overlap scan reached its candidate limit. Further overlap review is required.")); return;
                }
                var a = valid[i]; var b = valid[j];
                if (Math.Abs(PlanarGeometry.Dot(a.Plane.Normal, b.Plane.Normal)) < 1 - 1e-10 ||
                    Math.Abs(PlanarGeometry.Dot(PlanarGeometry.Sub(b.Plane.Origin, a.Plane.Origin), a.Plane.Normal)) > snapshot.Policy.PlanarityToleranceM) continue;
                // Near-parallel planes pass the origin test yet can diverge beyond tolerance across a large polygon.
                Clipper2Lib.PathD ring;
                try { ring = a.Plane.ProjectRing(b.Source.Boundary, factor, snapshot.Policy); }
                catch (ArgumentException)
                {
                    warnings.Add(new("OVERLAP_UNCHECKED", $"{a.Source.ObjectId} and {b.Source.ObjectId} are nearly coplanar but could not be compared in one plane; review for layering/duplication."));
                    continue;
                }
                var overlap = Math.Abs(Clipper2Lib.Clipper.Area(Clipper2Lib.Clipper.Intersect(a.Plane.Gross, [ring], Clipper2Lib.FillRule.NonZero, PlanarGeometry.Precision)));
                if (overlap > snapshot.Policy.LinearToleranceM * snapshot.Policy.LinearToleranceM)
                    warnings.Add(new("AREA_OVERLAP", $"Coplanar footprint overlap between {a.Source.ObjectId} and {b.Source.ObjectId}; confirm layering/duplication. Modeled quantities are retained."));
            }
    }

    public static void Validate(TakeoffSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.SchemaVersion != 2) throw new ArgumentException("Expected takeoff schema version 2.");
        CalculateSnapshot.Validate(s.Model);
        if (s.Areas.IsDefault || s.Stories.IsDefault || s.Metadata.IsDefault || s.Reinforcement.IsDefault || s.Policy is null)
            throw new ArgumentException("Areas, stories, metadata, reinforcement and policy are required.");
        var p = s.Policy;
        if (string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.IntendedUse) || !double.IsFinite(p.SteelDensityKgM3) || p.SteelDensityKgM3 <= 0 ||
            !double.IsFinite(p.LinearToleranceM) || p.LinearToleranceM < 1e-6 || p.LinearToleranceM > .001 ||
            !double.IsFinite(p.PlanarityToleranceM) || p.PlanarityToleranceM < p.LinearToleranceM || p.PlanarityToleranceM > .01)
            throw new ArgumentException("Policy requires identity, intended use, positive density and supported finite tolerances (linear 1e-6–1e-3 m, planarity linear–0.01 m).");
        if ((p.ApprovedAt is null) != string.IsNullOrWhiteSpace(p.ApprovedBy) || p.ApprovedAt == default(DateTimeOffset))
            throw new ArgumentException("Policy approval requires both name and a nondefault date, or neither.");
        var ids = s.Model.Frames.Select(x => x.ObjectId).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < s.Areas.Length; i++)
        {
            var area = s.Areas[i];
            var name = area is null || string.IsNullOrWhiteSpace(area.ObjectId) ? $"Area {i + 1}" : $"Area {area.ObjectId}";
            if (area is null || string.IsNullOrWhiteSpace(area.ObjectId)) throw new ArgumentException($"{name}: objectId is required.");
            if (!ids.Add(area.ObjectId)) throw new ArgumentException($"{name}: objectId is already used by another frame or area.");
            if (string.IsNullOrWhiteSpace(area.SourceReference)) throw new ArgumentException($"{name}: sourceReference is required.");
            if (!Enum.IsDefined(area.Kind) || !Enum.IsDefined(area.Material)) throw new ArgumentException($"{name}: unknown kind or material.");
            if (area.Thickness is null || !Enum.IsDefined(area.Thickness.Unit)) throw new ArgumentException($"{name}: thickness needs a value and a known unit.");
            if (area.Boundary.IsDefault || area.Openings.IsDefault) throw new ArgumentException($"{name}: boundary and openings must be arrays (openings may be empty).");
        }
        var storyIds = new HashSet<string>(StringComparer.Ordinal);
        double previousTop = double.NegativeInfinity;
        foreach (var band in s.Stories.OrderBy(x => x?.LowerElevationM))
        {
            if (band is null || string.IsNullOrWhiteSpace(band.Id) || band.Id == "Unallocated")
                throw new ArgumentException("Every story needs a name other than 'Unallocated'.");
            if (!storyIds.Add(band.Id)) throw new ArgumentException($"Story {band.Id}: the name is used twice.");
            if (!double.IsFinite(band.LowerElevationM) || !double.IsFinite(band.UpperElevationM) || band.UpperElevationM <= band.LowerElevationM)
                throw new ArgumentException($"Story {band.Id}: upperElevationM must be finite and above lowerElevationM.");
            if (band.LowerElevationM < previousTop) throw new ArgumentException($"Story {band.Id}: overlaps the story below it.");
            previousTop = band.UpperElevationM;
        }
        var metadataIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in s.Metadata)
        {
            if (m is null || string.IsNullOrWhiteSpace(m.ObjectId)) throw new ArgumentException("Every metadata entry needs an objectId.");
            var name = $"Metadata for {m.ObjectId}";
            if (!ids.Contains(m.ObjectId)) throw new ArgumentException($"{name}: no frame or area has this objectId.");
            if (!metadataIds.Add(m.ObjectId)) throw new ArgumentException($"{name}: appears more than once.");
            if (string.IsNullOrWhiteSpace(m.MaterialName) || string.IsNullOrWhiteSpace(m.SectionName)) throw new ArgumentException($"{name}: materialName and sectionName are required.");
            if (m.AssignedStoryId is not null && !storyIds.Contains(m.AssignedStoryId)) throw new ArgumentException($"{name}: assignedStoryId '{m.AssignedStoryId}' is not a declared story.");
            if (m.RequiredSteelComponents.IsDefaultOrEmpty) throw new ArgumentException($"{name}: requiredSteelComponents must list at least one component.");
            if (m.RequiredSteelComponents.Distinct(StringComparer.Ordinal).Count() != m.RequiredSteelComponents.Length) throw new ArgumentException($"{name}: requiredSteelComponents lists a component twice.");
            if (m.RequiredSteelComponents.FirstOrDefault(c => !ComponentNames.Contains(c)) is { } unknown)
                throw new ArgumentException($"{name}: unknown steel component '{unknown}'. Supported: {string.Join(", ", ComponentNames)}.");
            if (m.RequiredSteelComponents.Contains("Longitudinal") && (m.RequiredSteelComponents.Contains("LongitudinalTop") || m.RequiredSteelComponents.Contains("LongitudinalBottom")))
                throw new ArgumentException($"{name}: Longitudinal cannot be combined with LongitudinalTop or LongitudinalBottom.");
        }
        if (ids.Except(metadataIds).Order(StringComparer.Ordinal).FirstOrDefault() is { } uncovered)
            throw new ArgumentException($"{uncovered}: has no metadata entry. Every frame and area needs exactly one.");
        var meta = s.Metadata.ToDictionary(x => x.ObjectId);
        var componentIds = new HashSet<(string, string)>();
        foreach (var steel in s.Reinforcement)
        {
            if (steel is null || string.IsNullOrWhiteSpace(steel.ObjectId)) throw new ArgumentException("Every reinforcement entry needs an objectId.");
            var name = $"Reinforcement {steel.ObjectId}/{steel.Component}";
            if (!ids.Contains(steel.ObjectId)) throw new ArgumentException($"{name}: no frame or area has this objectId.");
            if (!componentIds.Add((steel.ObjectId, steel.Component))) throw new ArgumentException($"{name}: the component is supplied more than once.");
            if (string.IsNullOrWhiteSpace(steel.SourceReference)) throw new ArgumentException($"{name}: sourceReference is required.");
            if (steel.Component != "AllIn" && !meta[steel.ObjectId].RequiredSteelComponents.Contains(steel.Component))
                throw new ArgumentException($"{name}: the component is not in the element's requiredSteelComponents.");
            if (!Enum.IsDefined(steel.Method) || !Enum.IsDefined(steel.ConcreteBasis) || !Enum.IsDefined(steel.DesignEvidence))
                throw new ArgumentException($"{name}: unknown method, concreteBasis or designEvidence.");
            if (steel.CoversComponents.IsDefault || steel.Stations.IsDefault || steel.Bars.IsDefault || steel.Stations.Any(x => x is null) || steel.Bars.Any(x => x is null))
                throw new ArgumentException($"{name}: coversComponents, stations and bars must be arrays without null entries.");
            if (steel.Component == "AllIn" && (steel.Method is not (SteelMethod.VolumeFraction or SteelMethod.KgPerCubicMetre) ||
                !steel.CoversComponents.ToHashSet(StringComparer.Ordinal).SetEquals(meta[steel.ObjectId].RequiredSteelComponents) ||
                steel.CoversComponents.Distinct().Count() != steel.CoversComponents.Length))
                throw new ArgumentException($"{name}: an all-in estimate must use VolumeFraction or KgPerCubicMetre and cover exactly the element's required components.");
            if (steel.Component != "AllIn" && steel.CoversComponents.Length > 0)
                throw new ArgumentException($"{name}: coversComponents is only for AllIn estimates; a component entry covers only its named component.");
            if (steel.Method == SteelMethod.DemandEquivalent && steel.Component is not ("Longitudinal" or "LongitudinalTop" or "LongitudinalBottom"))
                throw new ArgumentException($"{name}: demand integration supports longitudinal area only, not shear area-per-length.");
        }
        if (s.Reinforcement.GroupBy(x => x.ObjectId).FirstOrDefault(g => g.Count() > 1 && g.Any(x => x.Component == "AllIn")) is { } mixed)
            throw new ArgumentException($"Reinforcement {mixed.Key}: an AllIn estimate replaces component quantities and cannot be combined with them.");
    }
}
