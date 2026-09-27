using System.Collections.Immutable;
using System.Globalization;
using Quentra.Core;

namespace Quentra.Application;

public static partial class TakeoffEngine
{
    public const string Version = "takeoff/0.5.0";
    public static readonly string[] ComponentNames = ["Longitudinal", "LongitudinalTop", "LongitudinalBottom", "LongitudinalTorsion", "Transverse", "Web", "Boundary", "XTop", "XBottom", "YTop", "YBottom", "Detailing", "Accessories"];
    // Components measured as a longitudinal area along the member, so design demand can be integrated for them.
    public static readonly string[] LongitudinalComponents = ["Longitudinal", "LongitudinalTop", "LongitudinalBottom", "LongitudinalTorsion"];

    public static TakeoffResult Run(TakeoffSnapshot snapshot, CancellationToken cancellationToken = default, IReadOnlySet<string>? excluded = null,
        IReadOnlyDictionary<string, string>? sectionChanges = null)
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
        var sourceWarnings = snapshot.SourceWarnings.IsDefault ? [] : snapshot.SourceWarnings;
        warnings.AddRange(sourceWarnings.Where(x => x.ObjectId is null).Select(x => new CalculationWarning(x.Code, x.Message)));
        var sourceByObject = sourceWarnings.Where(x => x.ObjectId is not null)
            .ToLookup(x => x.ObjectId!, x => new CalculationWarning(x.Code, x.Message), StringComparer.Ordinal);
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
                q.Warnings.Where(x => x.Code != "STEEL_UNKNOWN").Concat(sourceByObject[source.ObjectId]).ToImmutableArray());
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
            else if (UnsupportedReasons(source) is { Length: > 0 } reasons)
            { status = QuantityStatus.Unsupported; error = "Not quantified: " + string.Join("; ", reasons) + "."; }
            PlanarGeometry? geometry = null;
            double thickness = 0;
            if (error is null)
            {
                // Named here: the generic Length check would report a bad thickness as a bad "length".
                if (!double.IsFinite(source.Thickness.Metres) || source.Thickness.Metres <= 0)
                {
                    status = QuantityStatus.Invalid;
                    error = string.Create(CultureInfo.InvariantCulture, $"Thickness must be finite and positive; the snapshot gives {source.Thickness.Value} {source.Thickness.Unit}.");
                }
                else try
                {
                    thickness = source.Thickness.Metres;
                    geometry = new PlanarGeometry(source, snapshot.Model.CoordinateUnit, snapshot.Policy);
                    _ = new Volume(geometry.GrossArea * thickness);
                }
                catch (ArgumentException ex) { status = QuantityStatus.Invalid; error = Plain(ex); }
            }
            var element = new ElementTakeoff(source.ObjectId, source.Kind.ToString(), m.MaterialName, m.SectionName,
                status, source.SourceReference, error is null ? geometry!.GrossArea * thickness : null,
                error is null ? Math.Max(0, geometry!.GrossArea - geometry.RemainingArea) * thickness : null,
                error is null ? geometry!.RemainingArea * thickness : null,
                error is null ? geometry!.RemainingArea : null, null,
                "true planar area minus union of clipped openings, multiplied by uniform normal thickness",
                (error is null ? AreaWarnings(source, geometry!, thickness, snapshot.Model.CoordinateUnit) : [new(code ?? "AREA_" + status.ToString().ToUpperInvariant(), error)])
                    .AddRange(sourceByObject[source.ObjectId]));
            elements.Add(element);
            if (element.Status == QuantityStatus.Quantified)
                AllocateArea(source, element, m, geometry!, thickness, snapshot, stories);
        }
        var ordered = elements.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ToImmutableArray();
        var byId = ordered.ToDictionary(x => x.ObjectId);
        var steel = snapshot.Reinforcement.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Component, StringComparer.Ordinal)
            .Select(x => ExplainOverride(SteelCalculator.Calculate(x, byId[x.ObjectId], snapshot.Policy), x, sectionChanges)).ToImmutableArray();
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
        if (!completeConcrete) warnings.Add(new("PARTIAL_CONCRETE", inScope == 0
            ? "Concrete scope is empty; there is no complete concrete quantity."
            : $"Not quantified: {Names(ordered.Where(x => x.Status is QuantityStatus.Unsupported or QuantityStatus.Invalid).Select(x => x.ObjectId).ToArray())}. Known concrete is only a subtotal; each element's warning says why."));
        if (!completeSteel) warnings.Add(new("PARTIAL_STEEL", inScope == 0
            ? "Steel scope is empty; there is no complete steel quantity."
            : $"Required steel components missing: {Names(coverage.Where(x => x.Missing.Length > 0).Select(x => $"{x.ObjectId} ({string.Join(", ", x.Missing)})").ToArray())}. Known steel is only a subtotal."));
        if (coverage.Where(x => x.Required.IsEmpty).Select(x => x.ObjectId).ToArray() is { Length: > 0 } steelFree)
            warnings.Add(new("NO_STEEL_REQUIRED", $"Declared to need no steel (requiredSteelComponents is empty): {Names(steelFree)}. Their steel counts as complete at 0 kg; confirm none is needed."));
        if (stories.Where(x => x.StoryId == "Unallocated").Select(x => x.ObjectId).Distinct().ToArray() is { Length: > 0 } unallocated)
            warnings.Add(new("UNALLOCATED_STORY", $"Not allocated to a story: {Names(unallocated)}. Their geometry is outside the declared story bands, or a slab has no assignedStoryId."));
        DetectDuplicateFrames(snapshot, warnings);
        DetectAreaOverlaps(snapshot, warnings, cancellationToken);
        return new(Version, ordered, stories.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.StoryId, StringComparer.Ordinal).ToImmutableArray(),
            steel, coverage, summary, Groups(ordered, x => x.Category), Groups(ordered, x => x.MaterialName), warnings.ToImmutableArray());
    }

    private static string[] UnsupportedReasons(AreaSnapshot a) =>
    [
        .. a.Material == MaterialKind.Unknown ? ["material is Unknown"] : Array.Empty<string>(),
        .. a.Kind == AreaKind.Unsupported ? ["kind is Unsupported (only Slab and Wall are quantified)"] : Array.Empty<string>(),
        .. a.PhysicalThicknessVerified ? Array.Empty<string>() : ["physicalThicknessVerified is false (confirm the modeled thickness is the physical thickness)"],
        .. a.OpeningsVerified ? Array.Empty<string>() : ["openingsVerified is false (confirm every opening belongs to this host)"]
    ];

    // ArgumentOutOfRangeException appends "(Parameter 'name')", which means nothing to a user.
    private static string Plain(ArgumentException ex) => ex.ParamName is null ? ex.Message : ex.Message.Replace($" (Parameter '{ex.ParamName}')", "", StringComparison.Ordinal);

    private static string Names(IReadOnlyList<string> ids) => ids.Count <= 20 ? string.Join(", ", ids) : string.Join(", ", ids.Take(20)) + $" and {ids.Count - 20} more";

    private static SteelQuantity ExplainOverride(SteelQuantity quantity, SteelInput input, IReadOnlyDictionary<string, string>? sectionChanges) =>
        quantity.MassKg is null && input.Method == SteelMethod.DemandEquivalent && sectionChanges?.GetValueOrDefault(input.ObjectId) is { } change
            ? quantity with { Warnings = [new("STEEL_UNAVAILABLE", $"Not counted: {change} changed {input.ObjectId}'s section, so the design demand for the original section no longer applies. Supply demand from a design run of the new section, or assigned bars.")] }
            : quantity;

    private static ImmutableArray<CalculationWarning> AreaWarnings(AreaSnapshot source, PlanarGeometry geometry, double thickness, LengthUnit unit) =>
    [
        .. geometry.RemainingArea <= 0 ? [new CalculationWarning("AREA_FULLY_VOIDED",
            "Openings remove the whole area, so its opening-adjusted volume is 0 m³. Confirm the openings and their host.")] : Array.Empty<CalculationWarning>(),
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
        // Bounding boxes prune candidates; an exact planar intersection establishes overlap. Only pairs whose boxes
        // overlap in all three axes count towards the limit, so floors stacked in plan and neighbours that merely
        // share an edge (which cannot overlap in area) do not exhaust it on a tall building.
        var valid = new List<(AreaSnapshot Source, PlanarGeometry Plane, Box Box)>();
        var factor = Units.MetresPerUnit(snapshot.Model.CoordinateUnit);
        var flat = snapshot.Policy.PlanarityToleranceM;
        foreach (var area in snapshot.Areas.Where(x => x.Material == MaterialKind.Concrete && x.PhysicalThicknessVerified && x.OpeningsVerified))
            try { valid.Add((area, new PlanarGeometry(area, snapshot.Model.CoordinateUnit, snapshot.Policy), Box.Of(area.Boundary, factor))); }
            catch (ArgumentException) { /* Invalid geometry is already reported in its quantity record. */ }
        valid.Sort((a, b) => a.Box.MinX.CompareTo(b.Box.MinX));
        var candidates = 0;
        for (var i = 0; i < valid.Count; i++)
            for (var j = i + 1; j < valid.Count && valid[j].Box.MinX <= valid[i].Box.MaxX; j++)
            {
                token.ThrowIfCancellationRequested();
                if (!valid[i].Box.Overlaps(valid[j].Box, flat)) continue;
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
        CalculateSnapshot.ThrowIfAny(Problems(s));
    }

    // Reports every independent problem (see CalculateSnapshot.Problems). An entry that cannot be identified,
    // or whose reference is broken, skips the checks that depend on it rather than adding follow-on errors.
    public static IEnumerable<string> Problems(TakeoffSnapshot s)
    {
        if (s.SchemaVersion != 2) { yield return "Expected takeoff schema version 2."; yield break; }
        if (s.Model is null) { yield return "The model is required."; yield break; }
        foreach (var problem in CalculateSnapshot.Problems(s.Model)) yield return problem;
        if (s.Model.Frames.IsDefault) yield break;
        if (s.Areas.IsDefault || s.Stories.IsDefault || s.Metadata.IsDefault || s.Reinforcement.IsDefault || s.Policy is null)
        {
            yield return "Areas, stories, metadata, reinforcement and policy are required.";
            yield break;
        }
        var p = s.Policy;
        if (string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.IntendedUse) || !double.IsFinite(p.SteelDensityKgM3) || p.SteelDensityKgM3 <= 0 ||
            !double.IsFinite(p.LinearToleranceM) || p.LinearToleranceM < 1e-6 || p.LinearToleranceM > .001 ||
            !double.IsFinite(p.PlanarityToleranceM) || p.PlanarityToleranceM < p.LinearToleranceM || p.PlanarityToleranceM > .01)
            yield return "Policy requires identity, intended use, positive density and supported finite tolerances (linear 1e-6–1e-3 m, planarity linear–0.01 m).";
        if ((p.ApprovedAt is null) != string.IsNullOrWhiteSpace(p.ApprovedBy) || p.ApprovedAt == default(DateTimeOffset))
            yield return "Policy approval requires both name and a nondefault date, or neither.";
        var ids = s.Model.Frames.Where(x => !string.IsNullOrWhiteSpace(x?.ObjectId)).Select(x => x.ObjectId).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < s.Areas.Length; i++)
        {
            var area = s.Areas[i];
            if (area is null || string.IsNullOrWhiteSpace(area.ObjectId)) { yield return $"Area {i + 1}: objectId is required."; continue; }
            var name = $"Area {area.ObjectId}";
            if (!ids.Add(area.ObjectId)) yield return $"{name}: objectId is already used by another frame or area.";
            if (string.IsNullOrWhiteSpace(area.SourceReference)) yield return $"{name}: sourceReference is required.";
            if (!Enum.IsDefined(area.Kind) || !Enum.IsDefined(area.Material)) yield return $"{name}: unknown kind or material.";
            if (area.Thickness is null || !Enum.IsDefined(area.Thickness.Unit)) yield return $"{name}: thickness needs a value and a known unit.";
            if (area.Boundary.IsDefault || area.Openings.IsDefault) yield return $"{name}: boundary and openings must be arrays (openings may be empty).";
        }
        var storyIds = new HashSet<string>(StringComparer.Ordinal);
        double previousTop = double.NegativeInfinity;
        foreach (var band in s.Stories.OrderBy(x => x?.LowerElevationM))
        {
            if (band is null || string.IsNullOrWhiteSpace(band.Id) || band.Id == "Unallocated")
            {
                yield return "Every story needs a name other than 'Unallocated'.";
                continue;
            }
            if (!storyIds.Add(band.Id)) yield return $"Story {band.Id}: the name is used twice.";
            if (!double.IsFinite(band.LowerElevationM) || !double.IsFinite(band.UpperElevationM) || band.UpperElevationM <= band.LowerElevationM)
            {
                yield return $"Story {band.Id}: upperElevationM must be finite and above lowerElevationM.";
                continue;
            }
            if (band.LowerElevationM < previousTop) yield return $"Story {band.Id}: overlaps the story below it.";
            previousTop = Math.Max(previousTop, band.UpperElevationM);
        }
        var meta = new Dictionary<string, ElementMetadata>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in s.Metadata)
        {
            if (m is null || string.IsNullOrWhiteSpace(m.ObjectId)) { yield return "Every metadata entry needs an objectId."; continue; }
            var name = $"Metadata for {m.ObjectId}";
            if (!ids.Contains(m.ObjectId)) yield return $"{name}: no frame or area has this objectId.";
            if (!seen.Add(m.ObjectId)) { yield return $"{name}: appears more than once."; continue; }
            if (string.IsNullOrWhiteSpace(m.MaterialName) || string.IsNullOrWhiteSpace(m.SectionName)) yield return $"{name}: materialName and sectionName are required.";
            if (m.AssignedStoryId is not null && !storyIds.Contains(m.AssignedStoryId)) yield return $"{name}: assignedStoryId '{m.AssignedStoryId}' is not a declared story.";
            // An empty list is an explicit declaration that the element needs no steel; a missing list is an error.
            if (m.RequiredSteelComponents.IsDefault) { yield return $"{name}: requiredSteelComponents is required. List the components, or give [] for an element that needs no steel."; continue; }
            var components = m.RequiredSteelComponents;
            string? componentProblem = null;
            if (components.Distinct(StringComparer.Ordinal).Count() != components.Length) componentProblem = $"{name}: requiredSteelComponents lists a component twice.";
            else if (components.FirstOrDefault(c => !ComponentNames.Contains(c)) is { } unknown)
                componentProblem = $"{name}: unknown steel component '{unknown}'. Supported: {string.Join(", ", ComponentNames)}.";
            else if (components.Contains("Longitudinal") && (components.Contains("LongitudinalTop") || components.Contains("LongitudinalBottom")))
                componentProblem = $"{name}: Longitudinal cannot be combined with LongitudinalTop or LongitudinalBottom.";
            // Only a valid component list is used to check the element's reinforcement entries.
            if (componentProblem is null) meta.Add(m.ObjectId, m); else yield return componentProblem;
        }
        var described = s.Metadata.Where(x => x?.ObjectId is not null).Select(x => x.ObjectId).ToHashSet(StringComparer.Ordinal);
        foreach (var uncovered in ids.Except(described).Order(StringComparer.Ordinal))
            yield return $"{uncovered}: has no metadata entry. Every frame and area needs exactly one.";
        var componentIds = new HashSet<(string, string)>();
        foreach (var steel in s.Reinforcement)
        {
            if (steel is null || string.IsNullOrWhiteSpace(steel.ObjectId)) { yield return "Every reinforcement entry needs an objectId."; continue; }
            var name = $"Reinforcement {steel.ObjectId}/{steel.Component}";
            if (!ids.Contains(steel.ObjectId)) { yield return $"{name}: no frame or area has this objectId."; continue; }
            if (!componentIds.Add((steel.ObjectId, steel.Component))) yield return $"{name}: the component is supplied more than once.";
            if (string.IsNullOrWhiteSpace(steel.SourceReference)) yield return $"{name}: sourceReference is required.";
            // Without usable metadata the element's own problem is already reported; component checks would only repeat it.
            var m = meta.GetValueOrDefault(steel.ObjectId);
            if (m is not null && m.RequiredSteelComponents.IsEmpty)
                yield return $"{name}: the element's requiredSteelComponents is empty (no steel needed), so it takes no reinforcement entries.";
            else if (m is not null && steel.Component != "AllIn" && !m.RequiredSteelComponents.Contains(steel.Component))
                yield return $"{name}: the component is not in the element's requiredSteelComponents.";
            if (!Enum.IsDefined(steel.Method) || !Enum.IsDefined(steel.ConcreteBasis) || !Enum.IsDefined(steel.DesignEvidence))
                yield return $"{name}: unknown method, concreteBasis or designEvidence.";
            if (steel.CoversComponents.IsDefault || steel.Stations.IsDefault || steel.Bars.IsDefault || steel.Stations.Any(x => x is null) || steel.Bars.Any(x => x is null))
            {
                yield return $"{name}: coversComponents, stations and bars must be arrays without null entries.";
                continue;
            }
            if (m is { RequiredSteelComponents.IsEmpty: false } && steel.Component == "AllIn" && (steel.Method is not (SteelMethod.VolumeFraction or SteelMethod.KgPerCubicMetre) ||
                !steel.CoversComponents.ToHashSet(StringComparer.Ordinal).SetEquals(m.RequiredSteelComponents) ||
                steel.CoversComponents.Distinct().Count() != steel.CoversComponents.Length))
                yield return $"{name}: an all-in estimate must use VolumeFraction or KgPerCubicMetre and cover exactly the element's required components.";
            if (steel.Component != "AllIn" && steel.CoversComponents.Length > 0)
                yield return $"{name}: coversComponents is only for AllIn estimates; a component entry covers only its named component.";
            if (steel.Method == SteelMethod.DemandEquivalent && !LongitudinalComponents.Contains(steel.Component))
                yield return $"{name}: demand integration supports longitudinal area only, not shear area-per-length.";
        }
        foreach (var mixed in s.Reinforcement.Where(x => !string.IsNullOrWhiteSpace(x?.ObjectId)).GroupBy(x => x.ObjectId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1 && g.Any(x => x.Component == "AllIn")).OrderBy(g => g.Key, StringComparer.Ordinal))
            yield return $"Reinforcement {mixed.Key}: an AllIn estimate replaces component quantities and cannot be combined with them.";
        if (!s.SourceWarnings.IsDefault)
            foreach (var w in s.SourceWarnings)
            {
                if (w is null || string.IsNullOrWhiteSpace(w.Code) || !SourceCode().IsMatch(w.Code) || string.IsNullOrWhiteSpace(w.Message))
                    yield return "Every sourceWarnings entry needs an upper-case code (e.g. ETABS_SECTION_UNSUPPORTED) and a message.";
                else if (w.ObjectId is not null && !ids.Contains(w.ObjectId))
                    yield return $"Source warning {w.Code}: no frame or area has objectId '{w.ObjectId}'.";
            }
        if (s.Source is { } src && (new[] { src.Program, src.ProgramVersion, src.ProgramBuild, src.ApiAssembly, src.ApiAssemblyVersion, src.ModelPath,
                src.PresentUnits, src.Extractor, src.ExtractorVersion, src.RawCaptureSha256 }.Any(string.IsNullOrWhiteSpace) || src.ExtractedAt == default))
            yield return "Source: program, versions, API assembly, model path, units, extractor, extraction time and raw capture hash are required when a source is given.";
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)+$")]
    private static partial System.Text.RegularExpressions.Regex SourceCode();

    private readonly record struct Box(double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ)
    {
        public static Box Of(ImmutableArray<Point3> points, double factor) => new(
            points.Min(p => p.X) * factor, points.Max(p => p.X) * factor, points.Min(p => p.Y) * factor,
            points.Max(p => p.Y) * factor, points.Min(p => p.Z) * factor, points.Max(p => p.Z) * factor);

        // Along an axis where either area is flat (within tolerance), touching counts, since coplanar areas share
        // that coordinate. Along any other axis the extents must overlap by a positive length: two areas whose extents
        // only touch there meet along a line at most, which has no area.
        public bool Overlaps(Box o, double flat) =>
            Axis(MinX, MaxX, o.MinX, o.MaxX, flat) && Axis(MinY, MaxY, o.MinY, o.MaxY, flat) && Axis(MinZ, MaxZ, o.MinZ, o.MaxZ, flat);

        private static bool Axis(double a0, double a1, double b0, double b1, double flat)
        {
            var overlap = Math.Min(a1, b1) - Math.Max(a0, b0);
            return a1 - a0 <= flat || b1 - b0 <= flat ? overlap >= -flat : overlap > 0;
        }
    }
}
