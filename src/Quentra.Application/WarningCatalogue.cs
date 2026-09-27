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
        ("ETABS_SOURCE", "Extracted from ETABS. Names the ETABS version, model file and units read, and confirms quantities come from ETABS model objects, not analysis mesh elements."),
        ("ETABS_UNTESTED_VERSION", "The ETABS version is not the tested version (22.7). Check the extraction against the model before relying on it."),
        ("ETABS_MULTIPLE_TOWERS", "The ETABS model has several towers; story bands come from the active tower only."),
        ("ETABS_BASE_STORY", "Objects on the ETABS base level are reported under the base story name, using a 1 mm band just below the base elevation."),
        ("ETABS_DESIGN_UNAVAILABLE", "ETABS has no concrete frame design results, so beam and column design reinforcement is unknown (not zero). Run concrete frame design in ETABS and extract again."),
        ("ETABS_STEEL_UNAPPROVED", "ETABS reinforcement evidence was read but no one approved its use for this takeoff, so it is not counted. Extract again with a named steel approver."),
        ("ETABS_AREA_STEEL_NOT_EXTRACTED", "Slab and wall reinforcement is not extracted from ETABS in this version; their steel is unknown, not zero."),
        ("ETABS_WALL_OPENING_GAPS", "ETABS 22.7 rewrites a wall containing an opening into wall pieces around it when it builds the analysis model. Such openings are gaps between walls: excluded from gross and opening-adjusted volumes, and not listed as openings."),
        ("ETABS_OPENING_NO_HOST", "An ETABS opening object overlaps no slab or wall in its plane, so it deducts nothing. Check the model."),
        ("ETABS_OPENINGS_ASSIGNED", "Lists the ETABS opening objects deducted from this slab or wall: those overlapping it in its plane, as ETABS applies them. Opening numbers match OPENING_* warnings."),
        ("ETABS_MATERIAL_UNRESOLVED", "The element's ETABS material is missing, unreadable, or of a type that is neither concrete nor clearly non-concrete, so its concrete is unknown."),
        ("ETABS_WEIGHTLESS_MATERIAL", "The ETABS material has zero weight and zero mass and is not a concrete type: the member is treated as a dummy or null member and is out of scope. Confirm it is not a real member."),
        ("ETABS_FRAME_ROLE", "ETABS orients the frame as a brace or other member. Only beams and columns are quantified."),
        ("ETABS_SECTION_MISSING", "The frame's ETABS section could not be read or has no positive dimensions, so its concrete is unknown."),
        ("ETABS_SECTION_UNSUPPORTED", "The ETABS section type (non-prismatic, Section Designer, tee, general, ...) is not supported; its concrete is not estimated."),
        ("ETABS_SECTION_AREA_MISMATCH", "ETABS reports a section area different from width x depth. Quentra uses width x depth; check the section."),
        ("ETABS_CURVED_FRAME", "ETABS reports a curved frame. Curved frames are not measured."),
        ("ETABS_JOINT_OFFSET", "The frame has insertion-point joint offsets; its axis is measured between the offset ends."),
        ("ETABS_NULL_AREA", "The ETABS area has no property (None): no thickness or material. It is non-concrete and out of scope."),
        ("ETABS_AREA_PROPERTY_UNSUPPORTED", "The ETABS area property (ribbed, waffle, deck, layered, auto-select, ...) has no single physical thickness, so the area is not measured."),
        ("ETABS_AREA_ORIENTATION", "The area's property kind (slab or wall) disagrees with its ETABS orientation. It is measured by its property kind; check the modelling."),
        ("ETABS_AREA_CURVED_EDGE", "The ETABS area has curved edges; only straight-edged polygons are measured."),
        ("ETABS_STORY_UNDECLARED", "ETABS reported no story, or a story not among those read, for this object; its story is taken from its elevation."),
        ("ETABS_DESIGN_NOT_CURRENT", "The element's ETABS design results reported errors, belong to a different design section, or come from an unlocked model, so they are not counted."),
        ("ETABS_STATION_GAP", "ETABS design stations for a steel component are further apart than the allowed maximum station gap, so that component is not counted. Add output stations in ETABS or approve a larger gap."),
        ("ETABS_COLUMN_BARS_MODELED", "Column longitudinal steel is the ETABS section's modeled bars (to be checked) as straight bars over the column length: not a design result or bar schedule."),
        ("ETABS_COLUMN_BARS_UNREADABLE", "The column section is set to check modeled bars, but the bar count or size could not be read, so its steel is unknown."),
        ("ETABS_API_CALL_FAILED", "An ETABS API call failed during extraction. The message names the call, object, return code and what it means for quantities."),
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
