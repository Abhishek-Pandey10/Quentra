namespace Quentra.Application;

// Plain-language meaning of every warning code a run can raise, for 'quentra codes'.
// WarningCatalogueTests checks that each code in the source is listed here.
public static class WarningCatalogue
{
    public static readonly IReadOnlyList<(string Code, string Meaning)> Entries =
    [
        ("MODELED_BASIS", "Always raised. Volumes are measured from the model, member by member; where members meet, the shared concrete is counted in each. Net, BOQ and procurement quantities are not produced."),
        ("DECLARED_SCOPE", "Always raised. 'Complete' means every element and required steel component in this snapshot was quantified, not that the building is complete."),
        ("SYNTHETIC_INPUT", "The snapshot's origin is Synthetic: illustrative data, not extracted from ETABS."),
        ("POLICY_PENDING", "The measurement policy has no approvedBy/approvedAt. Acceptance is blocked until it is approved."),
        ("IMPLAUSIBLE_POLICY", "The policy's steel density is outside 7000–8500 kg/m³. Check the value and unit."),
        ("IMPLAUSIBLE_DIMENSION", "A section, thickness, length or extent is outside its review band. It is still calculated and included in totals; check the value and its unit. Acceptance names each element, e.g. IMPLAUSIBLE_DIMENSION:B1."),
        ("IMPLAUSIBLE_STEEL_INTENSITY", "An element's known steel is above 600 kg per m³ of its concrete. Check rates, bar areas and units. Acceptance names each element, e.g. IMPLAUSIBLE_STEEL_INTENSITY:W1."),
        ("PARTIAL_CONCRETE", "At least one in-scope element could not be quantified, so there is no complete concrete total. The message names the elements; each element's own warning says why."),
        ("PARTIAL_STEEL", "At least one required steel component is missing or could not be calculated, so known steel is a subtotal. The message names each element and its missing components."),
        ("NO_STEEL_REQUIRED", "Some elements declare an empty requiredSteelComponents list: they need no steel, so their steel is complete at 0 kg. Confirm that is intended."),
        ("UNALLOCATED_STORY", "Some concrete is not in any declared story. The message names the elements."),
        ("COINCIDENT_FRAMES", "Two or more frames share the same axis. Check for duplicates; all are counted."),
        ("AREA_OVERLAP", "Two slabs or walls overlap in the same plane. Check for duplicates or layering; both are counted."),
        ("OVERLAP_UNCHECKED", "Two areas are nearly in the same plane but could not be compared. Review them for duplication."),
        ("OVERLAP_SCAN_LIMIT", "The overlap check stopped at its limit on this model. Further overlaps were not checked."),
        ("USER_EXCLUDED", "An override excluded this element. It is out of scope and the run is partial."),
        ("NON_CONCRETE", "The element is not concrete and is outside scope."),
        ("UNKNOWN_MATERIAL", "The frame's material is Unknown, so it is not quantified."),
        ("UNSUPPORTED_ROLE", "The frame is neither a beam nor a column, so it is not quantified."),
        ("UNSUPPORTED_GEOMETRY", "The frame is curved or non-prismatic, so it is not quantified."),
        ("UNSUPPORTED_SECTION", "The section shape is not supported (rectangular beams; rectangular or circular columns)."),
        ("MISSING_SECTION", "The frame has no section dimensions, so its concrete is unknown."),
        ("INVALID_GEOMETRY", "The frame has a zero, negative or non-finite dimension or length."),
        ("AREA_UNSUPPORTED", "The slab or wall is not quantified. The message lists which of material, kind, physicalThicknessVerified or openingsVerified caused it."),
        ("AREA_INVALID", "The slab or wall geometry is invalid (for example warped, self-intersecting or zero thickness). The message says which."),
        ("AREA_FULLY_VOIDED", "Openings remove the whole slab or wall, so its opening-adjusted volume is 0 m³. Check the openings and host."),
        ("OPENING_OUTSIDE_HOST", "An opening lies wholly outside its slab or wall and deducts nothing. Check its host."),
        ("OPENING_CROSSES_HOST", "An opening extends past its slab or wall edge; only the part inside is deducted."),
        ("STEEL_UNAVAILABLE", "A supplied steel component could not be calculated. The message says why (for example design evidence not VerifiedCurrent, or an override changed the section)."),
        ("DEMAND_EQUIVALENT", "Steel from design demand: required area sampled at stations, integrated using the larger end value of each interval. Laps, anchorage and unsampled peaks are not included."),
        ("PARTIAL_DEMAND_DOMAIN", "Demand stations cover only part of the member's length. That length is counted, but the component stays missing from coverage."),
        ("GROSS_BASIS", "Schema 1 runs only. Gross modeled frame concrete; member intersections are counted in each member."),
        ("FRAME_ONLY_SCOPE", "Schema 1 runs only. Beams and columns only; slabs, walls and steel are not calculated."),
        ("EMPTY_SCOPE", "Schema 1 runs only. No in-scope frames were found."),
        ("STEEL_UNKNOWN", "Schema 1 runs only. No reinforcement was supplied, so steel is unknown."),
    ];

    public const string Terms = """
Terms
  Gross modeled volume   Section × length (frames) or area × thickness (slabs, walls), openings ignored.
  Opening-adjusted       Gross minus openings; overlapping openings are counted once.
  Known total            Sum of everything that could be quantified.
  Complete total         Shown only when every in-scope element (or required steel component) was quantified;
                         otherwise 'unknown'. Unknown is never zero.
  designEvidence         Status of the design results a DemandEquivalent steel entry came from. Only
                         VerifiedCurrent is counted; Missing, Stale, Failed and CheckMode are not.
  Methods                DemandEquivalent (design demand), AssignedBars (count × area × length),
                         DistributedIntensity (m²/m × surface), VolumeFraction and KgPerCubicMetre (estimates).
""";
}
