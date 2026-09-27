"""Warning-code catalog and plausibility rules.

These are the *contract* between the benchmark suite and Quentra. They are written
out to tests/config/*.json by generate.py so the C# harness reads the same data.

Severity meaning
----------------
Fatal   : the whole snapshot is rejected; no quantities are produced.
Error   : the affected element (or its steel) is not trusted; finalization is blocked.
Warning : quantities are produced but a human should look; some Warnings block finalization.
Info    : traceability note only.
"""

SEVERITIES = ["Info", "Warning", "Error", "Fatal"]

# code: (severity, blocksFinalization, category, description)
WARNING_CODES = {
    # --- snapshot level (Fatal) ---
    "UNSUPPORTED_SCHEMA_VERSION": ("Fatal", True, "snapshot", "schemaVersion is not one this engine understands."),
    "PROVENANCE_MISSING": ("Fatal", True, "provenance", "No provenance block, or provenance.type is not a known category."),
    "PROVENANCE_INCONSISTENT": ("Fatal", True, "provenance", "Provenance mixes synthetic and ETABS-verified categories (e.g. synthetic with etabs_verified=true, or a capture without an ETABS version)."),
    "UNSUPPORTED_UNITS": ("Fatal", True, "units", "model.units.length is not one of m, mm, cm, ft, in."),
    # --- element validity (Error) ---
    "DUPLICATE_ELEMENT_ID": ("Error", True, "identity", "Two or more elements share an ID but differ in content. All copies are rejected because the true object cannot be determined."),
    "INVALID_GEOMETRY": ("Error", True, "geometry", "Zero-length frame, polygon with < 3 points, non-planar, zero-area or self-intersecting polygon."),
    "COORDINATE_OUT_OF_RANGE": ("Error", True, "geometry", "A coordinate is non-finite or larger than the configured maximum magnitude."),
    "INVALID_SECTION": ("Error", True, "geometry", "A section dimension (width, depth, diameter, thickness) is zero or negative."),
    "SECTION_NOT_FOUND": ("Error", True, "geometry", "The element references a section that is not defined in the snapshot."),
    "UNSUPPORTED_SECTION": ("Error", True, "geometry", "The section shape is not supported for quantity take-off, or does not suit the element type."),
    "UNKNOWN_MATERIAL": ("Error", True, "material", "The element or its reinforcement references a material not defined in the snapshot."),
    "MATERIAL_TYPE_AMBIGUOUS": ("Error", True, "material", "The material exists but its type is not Concrete, Steel or Rebar, so the element cannot be classified."),
    "STORY_REFERENCE_MISSING": ("Error", True, "story", "The element has no story. It is reported under '<unassigned>'."),
    "STORY_NOT_FOUND": ("Error", True, "story", "The element's story is not in model.stories. It is reported under '<unassigned>'."),
    "INVALID_MATERIAL_PROPERTY": ("Error", True, "material", "A rebar material density is zero or negative. Steel using it is unknown."),
    "UNREALISTIC_MATERIAL_DENSITY": ("Error", True, "plausibility", "A rebar material density is outside the configured plausible band. Steel using it is unknown."),
    "INVALID_REINFORCEMENT": ("Error", True, "reinforcement", "Reinforcement data is invalid (spacing <= 0, diameter <= 0, count <= 0, missing length for area elements, ...). Steel is unknown."),
    "REINFORCEMENT_SOURCE_MISMATCH": ("Error", True, "reinforcement", "The reinforcement source category does not match the component types supplied, or is not a known category."),
    "DESIGN_RESULT_MISSING": ("Error", True, "reinforcement", "A RequiredArea component references a design result that is absent or does not cover the member."),
    "DESIGN_RESULT_NOT_DESIGNED": ("Error", True, "reinforcement", "The referenced result is not designed or has an unsupported status. Demand steel remains unknown."),
    "DESIGN_RESULT_OWNER_MISMATCH": ("Error", True, "reinforcement", "The design result belongs to a different member. Demand steel remains unknown."),
    "INVALID_DESIGN_RESULT": ("Error", True, "reinforcement", "Design results have invalid/nonfinite/negative demands, duplicate stations, ambiguous result records or conversion overflow."),
    "UNSUPPORTED_REINFORCEMENT_LAYOUT": ("Error", True, "reinforcement", "A mesh layout was given for a host that is not a rectangle and no explicit region was supplied."),
    "UNIT_PLAUSIBILITY": ("Error", True, "plausibility", "A dimension is implausibly large and would be typical if divided by 1000 - probably a unit error. The value is NOT auto-corrected."),
    "UNREALISTIC_STEEL_RATE": ("Error", True, "plausibility", "Element steel mass per m3 of concrete exceeds the density of steel - physically impossible. The steel value is discarded (unknown)."),
    "ORPHAN_ANALYTICAL_ELEMENT": ("Error", True, "identity", "An analytical/mesh child references a parent that does not exist."),
    # --- warnings that block finalization ---
    "STALE_DESIGN_RESULT": ("Warning", True, "reinforcement", "The design result used for required reinforcement is flagged stale (model changed after design)."),
    "MISSING_REINFORCEMENT": ("Warning", True, "reinforcement", "Steel quantification was requested but the element has no reinforcement data. Steel is unknown, not zero."),
    "SLAB_OVERLAP": ("Warning", True, "double-counting", "Two slabs on the same story and plane overlap in plan. Both are counted; the overlap is double-counted until resolved."),
    "DUPLICATE_GEOMETRY": ("Warning", True, "double-counting", "Two elements with different IDs have identical type, section and geometry. Both are counted until resolved."),
    "EMPTY_MODEL": ("Warning", True, "snapshot", "The snapshot contains no elements."),
    # --- warnings that do not block ---
    "UNREALISTIC_DIMENSION": ("Warning", False, "plausibility", "A dimension exceeds a configurable plausibility band. Quantities are still computed as given."),
    "OPENING_OUTSIDE_HOST": ("Warning", False, "openings", "An opening does not intersect its host. It deducts nothing."),
    "OPENING_CLIPPED": ("Warning", False, "openings", "An opening lies partly outside its host. Only the part inside the host is deducted."),
    "UNSUPPORTED_ELEMENT_TYPE": ("Warning", False, "scope", "The element type is outside the quantity scope (e.g. Tendon, Link). It is skipped."),
    "WALL_COLUMN_OVERLAP": ("Warning", False, "double-counting", "A column axis lies inside a wall. Both are counted in full (no deduction)."),
    "ESTIMATED_REINFORCEMENT": ("Warning", False, "reinforcement", "Steel comes from an estimated ratio, not from bars or design output."),
    "HIGH_STEEL_RATE": ("Warning", False, "plausibility", "Element steel rate exceeds the configurable high-rate band."),
    "EXACTNESS_FLAG_CONFLICT": ("Warning", False, "reinforcement", "The input 'exact' flag contradicts the reinforcement source. The engine reports exactness derived from the source."),
    "DUPLICATE_ELEMENT": ("Warning", False, "identity", "An element appears more than once with identical content. The first copy is kept, later copies are excluded."),
    "SOURCE_WARNING": ("Warning", False, "source", "A warning reported by the extractor/source was passed through. Subject = the source's own code."),
    # --- info ---
    "ANALYTICAL_CHILD_EXCLUDED": ("Info", False, "double-counting", "An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting."),
    "OPENING_OVERLAP": ("Info", False, "openings", "Openings in one host overlap each other. Their union is deducted, not their sum."),
    "NON_CONCRETE_ELEMENT": ("Info", False, "scope", "The element's material is structural steel; it is not part of concrete quantities."),
    "REQUIRED_AREA_USED": ("Info", False, "reinforcement", "Steel was derived from required design areas, not from provided bars."),
    "DEFAULT_STEEL_DENSITY_USED": ("Info", False, "reinforcement", "No steel material was named; the configured default density was used."),
}

WARNING_CATEGORY_TITLES = {
    "snapshot": "Snapshot integrity", "provenance": "Provenance", "units": "Units",
    "identity": "Identity", "geometry": "Geometry", "material": "Materials", "story": "Stories",
    "reinforcement": "Reinforcement", "plausibility": "Plausibility", "double-counting": "Double counting",
    "openings": "Openings", "scope": "Scope", "source": "Source",
}

# Plausibility rules. All thresholds in SI (m, kg/m3). Every rule is configurable per run
# (manifest case "options.plausibility": {"RULE_ID": {"enabled": bool, "threshold": number}}).
PLAUSIBILITY_RULES = [
    {"id": "BEAM_SECTION_DIM_MAX", "appliesTo": "Beam", "quantity": "section width or depth", "comparator": ">", "threshold": 5.0, "unit": "m",
     "code": "UNREALISTIC_DIMENSION", "rationale": "Beams wider or deeper than 5 m are very unusual (transfer girders excepted); often a unit error.", "configurable": True, "enabled": True},
    {"id": "COLUMN_SECTION_DIM_MAX", "appliesTo": "Column", "quantity": "section width, depth or diameter", "comparator": ">", "threshold": 5.0, "unit": "m",
     "code": "UNREALISTIC_DIMENSION", "rationale": "Columns larger than 5 m are unusual (mega-columns excepted).", "configurable": True, "enabled": True},
    {"id": "SLAB_THICKNESS_MAX", "appliesTo": "Slab", "quantity": "thickness", "comparator": ">", "threshold": 2.0, "unit": "m",
     "code": "UNREALISTIC_DIMENSION", "rationale": "Slabs thicker than 2 m are rare (raft/transfer slabs excepted).", "configurable": True, "enabled": True},
    {"id": "WALL_THICKNESS_MAX", "appliesTo": "Wall", "quantity": "thickness", "comparator": ">", "threshold": 3.0, "unit": "m",
     "code": "UNREALISTIC_DIMENSION", "rationale": "Walls thicker than 3 m are rare (dams, shielding excepted).", "configurable": True, "enabled": True},
    {"id": "FRAME_LENGTH_MAX", "appliesTo": "Beam,Column", "quantity": "member length", "comparator": ">", "threshold": 100.0, "unit": "m",
     "code": "UNREALISTIC_DIMENSION", "rationale": "Single frame objects longer than 100 m usually indicate a unit or coordinate error.", "configurable": True, "enabled": True},
    {"id": "UNIT_SCALE_SUSPECT", "appliesTo": "Beam,Column,Slab,Wall", "quantity": "section dimension / 1000", "comparator": "in band", "threshold": 0.05, "unit": "m",
     "code": "UNIT_PLAUSIBILITY", "rationale": "If a dimension breaks its MAX rule but value/1000 lies between this lower bound and the MAX threshold, the value was probably entered in mm while the model is in m.", "configurable": True, "enabled": True},
    {"id": "STEEL_DENSITY_MIN", "appliesTo": "Rebar material", "quantity": "density", "comparator": "<", "threshold": 7000.0, "unit": "kg/m3",
     "code": "UNREALISTIC_MATERIAL_DENSITY", "rationale": "Reinforcing steel is about 7850 kg/m3.", "configurable": True, "enabled": True},
    {"id": "STEEL_DENSITY_MAX", "appliesTo": "Rebar material", "quantity": "density", "comparator": ">", "threshold": 9000.0, "unit": "kg/m3",
     "code": "UNREALISTIC_MATERIAL_DENSITY", "rationale": "Reinforcing steel is about 7850 kg/m3; > 9000 is not steel.", "configurable": True, "enabled": True},
    {"id": "STEEL_RATE_HIGH", "appliesTo": "Beam,Column,Slab,Wall", "quantity": "steel kg / net concrete m3", "comparator": ">", "threshold": 600.0, "unit": "kg/m3",
     "code": "HIGH_STEEL_RATE", "rationale": "Above ~600 kg/m3 congestion is severe; worth a check.", "configurable": True, "enabled": True},
    {"id": "STEEL_RATE_IMPOSSIBLE", "appliesTo": "Beam,Column,Slab,Wall", "quantity": "steel kg / net concrete m3", "comparator": "> steel density", "threshold": None, "unit": "kg/m3",
     "code": "UNREALISTIC_STEEL_RATE", "rationale": "Steel cannot occupy more than 100 % of the concrete volume.", "configurable": False, "enabled": True},
    {"id": "COORDINATE_MAX", "appliesTo": "all", "quantity": "|coordinate|", "comparator": ">", "threshold": 1.0e7, "unit": "m",
     "code": "COORDINATE_OUT_OF_RANGE", "rationale": "Coordinates beyond 10,000 km are not a building; also protects against overflow.", "configurable": True, "enabled": True},
]


def rule(rule_id):
    for r in PLAUSIBILITY_RULES:
        if r["id"] == rule_id:
            return r
    raise KeyError(rule_id)


def severity(code):
    return WARNING_CODES[code][0]


def blocks(code):
    return WARNING_CODES[code][1]
