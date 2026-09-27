"""Story aggregation, double counting, invalid/dangerous inputs and naming cases."""
import copy
import math
from ..snapshot import Snap, provided, bars, hoops, ratio, P
from ..case import Case, Hand

ONE = [("L1", 3.0, 3.0)]
NO_STEEL = {"quantifySteel": False}
RHO = 7850.0


def A(d):
    return math.pi * d * d / 4


def rect(x0, y0, x1, y1, z):
    return [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]


def aggregation():
    out = []
    stories = [("L1", 3.2, 3.2), ("L2", 6.4, 3.2), ("L3", 9.6, 3.2), ("Roof", 12.8, 3.2)]
    s = Snap("AGG-001", "Four-story aggregation model", stories=stories)
    s.concrete("C30", 30).concrete("C40", 40).rebar("B500", fy=500)
    s.rect("C400", 0.4, 0.4).rect("B300x600", 0.3, 0.6).slab_section("S200", 0.2).slab_section("S150", 0.15).wall_section("W200", 0.2)
    col_r = provided([bars(4, 0.020), hoops(0.008, 0.2, cover=0.04, end_offset=0.05, hook=0.16)])
    beam_r = provided([bars(3, 0.016, role="bottom"), bars(2, 0.016, role="top"), hoops(0.008, 0.2, cover=0.025, end_offset=0.05, hook=0.16)])
    for i, (st, elev, h) in enumerate(stories):
        z0, z1 = elev - h, elev
        s.column(f"C1-{st}", st, "C400", "C40", (0, 0, z0), (0, 0, z1), name="C1", rebar=col_r)
        s.column(f"C2-{st}", st, "C400", "C40", (6, 0, z0), (6, 0, z1), name="C2", rebar=col_r)
        s.beam(f"B1-{st}", st, "B300x600", "C30", (0, 0, z1), (6, 0, z1), name="B1", rebar=beam_r)
        s.slab(f"S1-{st}", st, "S150" if st == "Roof" else "S200", "C30", rect(0, 0, 6, 4, z1), name="S1",
               rebar={"source": "EstimatedRatio", "steelMaterial": "B500", "components": [ratio(80)]})
        if st != "Roof":
            s.wall(f"W1-{st}", st, "W200", "C30", [(0, 8, z0), (4, 8, z0), (4, 8, z1), (0, 8, z1)], name="W1")
    col_v = 0.16 * 3.2
    beam_v = 0.18 * 6
    slab_v, roof_v = 24 * 0.2, 24 * 0.15
    wall_v = 4 * 3.2 * 0.2
    nt = math.floor((3.2 - 0.1) / 0.2) + 1          # 15.5 -> 16
    col_kg = 4 * A(0.02) * 3.2 * RHO + nt * (4 * (0.4 - 0.08 - 0.008) + 0.16) * A(0.008) * RHO
    nb = math.floor((6 - 0.1) / 0.2) + 1             # 29.5 -> 30
    beam_kg = 5 * A(0.016) * 6 * RHO + nb * (2 * ((0.3 - 0.05 - 0.008) + (0.6 - 0.05 - 0.008)) + 0.16) * A(0.008) * RHO
    typ = 2 * col_v + beam_v + slab_v + wall_v
    roof = 2 * col_v + beam_v + roof_v
    typ_kg = 2 * col_kg + beam_kg + 80 * slab_v
    roof_kg = 2 * col_kg + beam_kg + 80 * roof_v
    out.append(Case("AGG-001", "Four-story aggregation model", "aggregation", "synthetic",
        "Stories L1, L2, L3, Roof with beams, columns, slabs and walls. Verifies per-story, per-type and whole-model totals and that Σ(story) = model.",
        ["Each story: 2 columns 0.4 × 0.4 × 3.2 m (C40), 1 beam 0.3 × 0.6 × 6 m (C30), 1 slab 6 × 4 m (t 0.20; Roof t 0.15), 1 wall 4 × 3.2 × 0.2 m (not at Roof)",
         "Columns: 4T20 + T8@200 ties; beams: 5T16 + T8@200 stirrups; slabs: estimated 80 kg/m³; walls: no reinforcement (steel unknown, warned)"],
        s.to_dict(), {},
        [Hand("byStory.L1.netConcreteM3", typ, "2 × 0.512 + 1.08 + 4.8 + 2.56 = 9.464"),
         Hand("byStory.Roof.netConcreteM3", roof, "2 × 0.512 + 1.08 + 3.6 = 5.704"),
         Hand("modelTotals.netConcreteM3", 3 * typ + roof, "3 × 9.464 + 5.704 = 34.096"),
         Hand("byElementType.Wall.netConcreteM3", 3 * wall_v, "3 × 2.56"),
         Hand("byElementType.Column.steelKg", 8 * col_kg, "8 columns × (4 × A(20) × 3.2 × 7850 + 16 ties × 1.408 m × A(8) × 7850)"),
         Hand("byStory.L2.steelKg", typ_kg, "2 columns + beam + 80 × 4.8"),
         Hand("modelTotals.steelKg", 3 * typ_kg + roof_kg, "Σ stories"),
         Hand("byMaterial.C40.netConcreteM3", 8 * col_v, "all columns")],
        expected_behaviour="Σ(story totals) = model total; Σ(type totals) = model total"))
    return out


def double_counting():
    out = []

    def base(cid, name):
        s = Snap(cid, name, stories=ONE)
        s.concrete("C30", 30).rect("B300x600", 0.3, 0.6).rect("C400", 0.4, 0.4).slab_section("S200", 0.2).wall_section("W200", 0.2)
        return s

    s = base("DUP-001", "Beam plus analytical subdivision")
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.beam("B1~A1", "L1", "B300x600", "C30", (0, 0, 3), (3, 0, 3), role="AnalyticalSegment", parent="B1")
    s.beam("B1~A2", "L1", "B300x600", "C30", (3, 0, 3), (6, 0, 3), role="AnalyticalSegment", parent="B1")
    out.append(Case("DUP-001", "Beam plus analytical subdivision", "double-counting", "synthetic",
        "The extractor delivered the physical beam and its two analytical segments. Only the physical object may be counted.",
        ["B1 physical 0.3 × 0.6 × 6", "B1~A1, B1~A2: analytical halves (role AnalyticalSegment, parentId B1)"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 1.08, "B1 only (2.16 would be double counting)"), Hand("elements.B1~A1.status", "Excluded", "")],
        expected_behaviour="include parent, exclude children (info)"))

    s = base("DUP-002", "Slab plus mesh elements")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 6, 4, 3))
    for i, (x0, y0) in enumerate([(0, 0), (3, 0), (0, 2), (3, 2)]):
        s.slab(f"S1~M{i + 1}", "L1", "S200", "C30", rect(x0, y0, x0 + 3, y0 + 2, 3), role="MeshElement", parent="S1")
    out.append(Case("DUP-002", "Slab plus mesh elements", "double-counting", "synthetic",
        "Physical slab plus its four analysis mesh elements. Mesh elements are excluded; no SLAB_OVERLAP either, because excluded elements do not take part in overlap checks.",
        ["S1 6 × 4 × 0.2", "4 mesh elements 3 × 2 each, parent S1"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 4.8, "24 × 0.2 (9.6 would be double counting)")], expected_behaviour="exclude mesh"))

    s = base("DUP-003", "Wall split into mesh objects")
    s.wall("W1", "L1", "W200", "C30", [(0, 0, 0), (6, 0, 0), (6, 0, 3), (0, 0, 3)])
    s.wall("W1~M1", "L1", "W200", "C30", [(0, 0, 0), (3, 0, 0), (3, 0, 3), (0, 0, 3)], role="MeshElement", parent="W1")
    s.wall("W1~M2", "L1", "W200", "C30", [(3, 0, 0), (6, 0, 0), (6, 0, 3), (3, 0, 3)], role="MeshElement", parent="W1")
    out.append(Case("DUP-003", "Wall split into mesh objects", "double-counting", "synthetic", "Wall plus two mesh children.",
        ["W1 6 × 3 × 0.2; two mesh halves"], s.to_dict(), NO_STEEL, [Hand("modelTotals.netConcreteM3", 3.6, "18 × 0.2")], expected_behaviour="exclude mesh"))

    s = base("DUP-004", "Duplicate object ID with identical content")
    b = s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.raw_element(copy.deepcopy(b))
    out.append(Case("DUP-004", "Duplicate object ID with identical content", "double-counting", "synthetic",
        "The same object delivered twice byte-for-byte (e.g. extracted from two selection sets). Keep one copy, warn.",
        ["B1 appears twice, identical"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 1.08, "one copy"), Hand("elements.B1#1.status", "Excluded", "second copy")], expected_behaviour="keep first, exclude copy, warn"))

    s = base("DUP-005", "Duplicate object ID with conflicting content")
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.beam("B1", "L1", "B300x600", "C30", (0, 5, 3), (8, 5, 3))
    s.beam("B2", "L1", "B300x600", "C30", (0, 10, 3), (6, 10, 3))
    out.append(Case("DUP-005", "Duplicate object ID with conflicting content", "double-counting", "synthetic",
        "Two different beams claim ID 'B1'. Quentra cannot know which is real: reject both, keep B2, block finalization.",
        ["B1 (6 m) and B1 (8 m) — same ID, different geometry", "B2 6 m, unaffected"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 1.08, "B2 only"), Hand("completeness.concrete", "Incomplete", "B1 unknown")],
        expected_behaviour="reject both copies, block finalization"))

    s = base("DUP-006", "Duplicate geometry with different IDs")
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.beam("B7", "L1", "B300x600", "C30", (6, 0, 3), (0, 0, 3))
    out.append(Case("DUP-006", "Duplicate geometry with different IDs", "double-counting", "synthetic",
        "Two objects, different IDs, same section, same end points (reversed direction). Both are counted but finalization is blocked for human review.",
        ["B1: (0,0,3) → (6,0,3)", "B7: (6,0,3) → (0,0,3)"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 2.16, "both counted until resolved")], expected_behaviour="include both, warn, block"))

    s = base("DUP-007", "Drop panel modelled as overlapping slab")
    s.slab_section("S150", 0.15)
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 8, 8, 3))
    s.slab("DP1", "L1", "S150", "C30", rect(3, 3, 5, 5, 3))
    out.append(Case("DUP-007", "Drop panel modelled as overlapping slab", "double-counting", "synthetic",
        "A drop panel entered as a second slab inside the main slab (same plane). The overlap cannot be resolved automatically.",
        ["S1 8 × 8 × 0.20", "DP1 2 × 2 × 0.15 fully inside S1"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 64 * 0.2 + 4 * 0.15, "12.8 + 0.6 (both counted)")], expected_behaviour="include both, warn, block"))

    s = base("DUP-008", "Beam-column intersection")
    s.column("C1", "L1", "C400", "C30", (0, 0, 0), (0, 0, 3))
    s.column("C2", "L1", "C400", "C30", (6, 0, 0), (6, 0, 3))
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.beam("B2", "L1", "B300x600", "C30", (0, 0, 3), (0, 5, 3))
    out.append(Case("DUP-008", "Beam-column intersection", "double-counting", "synthetic",
        "Two beams frame into column C1. Per the centre-line convention nothing is deducted and nothing is warned.",
        ["C1, C2 0.4 × 0.4 × 3", "B1 6 m, B2 5 m, both 0.3 × 0.6, meeting at C1's top"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 2 * 0.48 + 0.18 * 11, "0.96 + 0.18 × 11 = 2.94")],
        notes=["Convention: centre-line quantities, no joint deduction (CONVENTIONS.md §3)."], expected_behaviour="include all, no warning"))

    s = base("DUP-009", "Wall-column overlap")
    s.wall("W1", "L1", "W200", "C30", [(0, 0, 0), (6, 0, 0), (6, 0, 3), (0, 0, 3)])
    s.column("C1", "L1", "C400", "C30", (3, 0, 0), (3, 0, 3))
    out.append(Case("DUP-009", "Wall-column overlap", "double-counting", "synthetic",
        "A column modelled inside a wall. Both are counted in full; Quentra warns so the modeller can decide.",
        ["W1 6 × 3 × 0.2 in plane y = 0", "C1 0.4 × 0.4 at (3, 0) — its axis lies inside W1"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 3.6 + 0.48, "3.6 + 0.48 (overlap 0.2 × 0.4 × 3 = 0.24 m³ not deducted)")], expected_behaviour="include both, warn"))

    s = base("DUP-010", "Orphan analytical element")
    s.beam("B9~A1", "L1", "B300x600", "C30", (0, 0, 3), (3, 0, 3), role="AnalyticalSegment", parent="B9")
    s.beam("B2", "L1", "B300x600", "C30", (0, 5, 3), (6, 5, 3))
    out.append(Case("DUP-010", "Orphan analytical element", "double-counting", "synthetic",
        "An analytical segment whose parent 'B9' is missing from the snapshot. It must not be silently dropped (quantity loss) nor silently counted.",
        ["B9~A1: analytical, parent B9 absent", "B2: physical"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 1.08, "B2 only"), Hand("elements.B9~A1.status", "Rejected", "")], expected_behaviour="reject, block"))
    return out


def invalid():
    out = []

    def base(cid, name, stories=ONE, unit="m"):
        s = Snap(cid, name, unit=unit, stories=stories)
        s.concrete("C30", 30).rebar("B500", fy=500)
        s.rect("B300x600", 0.3, 0.6).slab_section("S200", 0.2).wall_section("W200", 0.2)
        return s

    def ok_beam(s):
        s.beam("B-OK", "L1", "B300x600", "C30", (0, 20, 3), (6, 20, 3))

    def add(cid, name, s, purpose, truth, behaviour, hand=None, options=None):
        if behaviour == "reject snapshot":
            ok = [Hand("snapshotStatus", "Rejected", "fatal"), Hand("modelTotals", None, "no quantities at all")]
        else:
            ok = [Hand("elements.B-OK.netConcreteM3", 1.08, "unaffected control beam")] if any(e["id"] == "B-OK" for e in s.d["elements"]) else []
        out.append(Case(cid, name, "invalid", "invalid", purpose, truth, s.to_dict(), NO_STEEL if options is None else options,
                        ok + (hand or []), expected_behaviour=behaviour))

    s = base("INVALID-001", "Beam width = 0"); s.rect("B0x600", 0.0, 0.6)
    s.beam("B1", "L1", "B0x600", "C30", (0, 0, 3), (6, 0, 3)); ok_beam(s)
    add("INVALID-001", "Beam width = 0", s, "Zero section width. Rejected, never quantified as 0 m³.", ["B1 section width 0"], "reject element, block finalization",
        [Hand("elements.B1.netConcreteM3", None, "unknown, not 0"), Hand("completeness.concrete", "Incomplete", "")])

    s = base("INVALID-002", "Beam length = 0")
    s.beam("B1", "L1", "B300x600", "C30", (2, 2, 3), (2, 2, 3)); ok_beam(s)
    add("INVALID-002", "Beam length = 0", s, "Start and end nodes coincide.", ["B1 start = end = (2,2,3)"], "reject element, block finalization")

    s = base("INVALID-003", "Negative slab thickness"); s.slab_section("SNEG", -0.2)
    s.slab("S1", "L1", "SNEG", "C30", rect(0, 0, 5, 4, 3)); ok_beam(s)
    add("INVALID-003", "Negative slab thickness", s, "Negative thickness must not produce negative volume.", ["S1 t = −0.20 m"], "reject element, block finalization",
        [Hand("elements.S1.netConcreteM3", None, "never −4.0")])

    s = base("INVALID-004", "Slab thickness = 300 m"); s.slab_section("S300", 300.0)
    s.slab("S1", "L1", "S300", "C30", rect(0, 0, 5, 4, 3)); ok_beam(s)
    add("INVALID-004", "Slab thickness = 300 m", s,
        "Almost certainly '300' mm typed into a metre model. Quentra must NOT silently rescale; it computes as given and blocks finalization with a unit-plausibility error.",
        ["S1: 5 × 4 m, t = 300 m (sic)"], "include as given, warn + unit error, block finalization",
        [Hand("elements.S1.netConcreteM3", 6000.0, "20 × 300 — not rescaled")])

    s = base("INVALID-005", "Wall thickness = 250 m"); s.wall_section("W250M", 250.0)
    s.wall("W1", "L1", "W250M", "C30", [(0, 0, 0), (4, 0, 0), (4, 0, 3), (0, 0, 3)]); ok_beam(s)
    add("INVALID-005", "Wall thickness = 250 m", s, "Same unit mistake for a wall.", ["W1: 4 × 3 m, t = 250 m (sic)"], "include as given, warn + unit error, block finalization",
        [Hand("elements.W1.netConcreteM3", 3000.0, "12 × 250 — not rescaled")])

    s = base("INVALID-006", "Coordinate = 1e300")
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (1e300, 0, 3)); ok_beam(s)
    add("INVALID-006", "Coordinate = 1e300", s, "Coordinate would overflow length/volume arithmetic (1e300² = inf).", ["B1 end x = 1e300 m"], "reject element, block finalization",
        [Hand("elements.B1.lengthM", None, "")])

    s = Snap("INVALID-007", "Steel density = 0", stories=ONE)
    s.concrete("C30", 30).rebar("B500-BAD", density=0.0).rect("B300x600", 0.3, 0.6)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3), rebar=provided([bars(4, 0.02)], steel="B500-BAD"))
    add("INVALID-007", "Steel density = 0", s, "Invalid material property. Steel using it is unknown (not 0 kg). Concrete is unaffected.",
        ["Rebar material density 0", "B1 4T20"], "error on material, steel unknown, block finalization",
        [Hand("modelTotals.netConcreteM3", 1.08, "concrete still counted"), Hand("elements.B1.steelKg", None, "")], options={})

    s = Snap("INVALID-008", "Steel density = 15000 kg/m3", stories=ONE)
    s.concrete("C30", 30).rebar("HEAVY", density=15000.0).rect("B300x600", 0.3, 0.6)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3), rebar=provided([bars(4, 0.02)], steel="HEAVY"))
    add("INVALID-008", "Steel density = 15000 kg/m3", s, "Implausible density (> 9000 kg/m³ is not steel). Error; steel unknown.",
        ["Rebar density 15 000 kg/m³"], "error, steel unknown, block finalization", [Hand("elements.B1.steelKg", None, "")], options={})

    s = base("INVALID-009", "Duplicate element ID")
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.slab("B1", "L1", "S200", "C30", rect(0, 0, 5, 4, 3)); ok_beam(s)
    add("INVALID-009", "Duplicate element ID", s, "A beam and a slab share ID 'B1' (conflicting content).", ["Beam B1 and slab B1"], "reject both, block finalization",
        [Hand("elements.B1#0.status", "Rejected", ""), Hand("elements.B1#1.status", "Rejected", "")])

    s = base("INVALID-010", "Unknown material")
    s.beam("B1", "L1", "B300x600", "C35-UNDEFINED", (0, 0, 3), (6, 0, 3)); ok_beam(s)
    add("INVALID-010", "Unknown material", s, "Material not defined. Geometry is fine, so the volume is reported as unclassified (not concrete, not zero).",
        ["B1 material 'C35-UNDEFINED'"], "unclassified, error, block finalization",
        [Hand("modelTotals.unclassifiedVolumeM3", 1.08, ""), Hand("modelTotals.netConcreteM3", 1.08, "B-OK only")])

    s = base("INVALID-011", "Missing story")
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.d["elements"][-1]["story"] = None; ok_beam(s)
    add("INVALID-011", "Missing story", s, "Element has no story. It is still counted (no quantity loss) but under '<unassigned>', and finalization is blocked.",
        ["B1 story = null"], "include under <unassigned>, error, block finalization",
        [Hand("byStory.<unassigned>.netConcreteM3", 1.08, ""), Hand("modelTotals.netConcreteM3", 2.16, "Σ stories incl. <unassigned>")])

    s = base("INVALID-012", "Story not found")
    s.beam("B1", "L99", "B300x600", "C30", (0, 0, 3), (6, 0, 3)); ok_beam(s)
    add("INVALID-012", "Story not found", s, "Element references story 'L99' which is not declared.", ["B1 story 'L99'"],
        "include under <unassigned>, error, block finalization", [Hand("byStory.<unassigned>.netConcreteM3", 1.08, "")])

    s = base("INVALID-013", "Opening outside host (wall)")
    s.wall("W1", "L1", "W200", "C30", [(0, 0, 0), (4, 0, 0), (4, 0, 3), (0, 0, 3)], openings=[("O1", [(6, 0, 1), (7, 0, 1), (7, 0, 2), (6, 0, 2)])])
    add("INVALID-013", "Opening outside host (wall)", s, "A wall opening that does not touch its wall.", ["W1 4 × 3 m; O1 at u 6–7"], "warn, deduct nothing",
        [Hand("modelTotals.netConcreteM3", 2.4, "12 × 0.2")])

    s = base("INVALID-014", "Invalid polygon (2 points)")
    s.slab("S1", "L1", "S200", "C30", [(0, 0, 3), (5, 0, 3)]); ok_beam(s)
    add("INVALID-014", "Invalid polygon (2 points)", s, "Area object with only two vertices.", ["S1 two points"], "reject element, block finalization")

    s = base("INVALID-015", "Degenerate polygon (collinear)")
    s.slab("S1", "L1", "S200", "C30", [(0, 0, 3), (2, 0, 3), (5, 0, 3), (3, 0, 3)]); ok_beam(s)
    add("INVALID-015", "Degenerate polygon (collinear)", s, "Four collinear points: zero area, no plane.", ["S1 all points on y = 0"], "reject element, block finalization")

    s = base("INVALID-016", "Self-intersecting polygon")
    s.slab("S1", "L1", "S200", "C30", [(0, 0, 3), (4, 4, 3), (4, 0, 3), (0, 4, 3)]); ok_beam(s)
    add("INVALID-016", "Self-intersecting polygon", s,
        "'Bow-tie' polygon. The shoelace formula would return 0 (the two lobes cancel) — a silent wrong answer. Must be rejected.",
        ["S1 vertices (0,0) (4,4) (4,0) (0,4)"], "reject element, block finalization", [Hand("elements.S1.netConcreteM3", None, "")])

    s = Snap("INVALID-017", "Reinforcement spacing = 0", stories=ONE)
    s.concrete("C30", 30).rebar("B500").rect("B300x600", 0.3, 0.6)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3), rebar=provided([bars(4, 0.02), hoops(0.01, 0.0, cover=0.025)]))
    add("INVALID-017", "Reinforcement spacing = 0", s, "Zero spacing would mean infinite stirrups (division by zero).", ["B1 stirrups @ 0 mm"],
        "error, steel unknown, block finalization", [Hand("elements.B1.steelKg", None, ""), Hand("modelTotals.netConcreteM3", 1.08, "")], options={})

    s = Snap("INVALID-018", "Negative bar diameter", stories=ONE)
    s.concrete("C30", 30).rebar("B500").rect("B300x600", 0.3, 0.6)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3), rebar=provided([bars(4, -0.02)]))
    add("INVALID-018", "Negative bar diameter", s, "d² would hide the sign and give a positive mass. Must be rejected.", ["B1 4 bars d = −20 mm"],
        "error, steel unknown, block finalization", [Hand("elements.B1.steelKg", None, "not 59.19")], options={})

    s = base("INVALID-019", "Unsupported element type")
    s.d["elements"].append({"id": "T1", "name": "T1", "type": "Tendon", "role": "Physical", "parentId": None, "story": "L1", "material": "B500", "section": None,
                            "geometry": {"kind": "Line", "start": P(0, 0, 3), "end": P(6, 0, 3)}, "source": {"application": "quentra_bench", "version": "1.0.0", "objectName": "T1", "objectGuid": None, "apiMethod": None}})
    ok_beam(s)
    add("INVALID-019", "Unsupported element type", s, "A post-tensioning tendon. Out of scope: skipped with a warning (not silently ignored).", ["T1 type Tendon"], "skip, warn")

    s = base("INVALID-020", "Unsupported units", unit="m"); ok_beam(s)
    s.d["model"]["units"]["length"] = "furlong"
    add("INVALID-020", "Unsupported units", s, "Unknown length unit. Every number in the file is uninterpretable: reject the snapshot.", ["units.length = 'furlong'"], "reject snapshot")

    s = base("INVALID-021", "Synthetic data claiming ETABS verification"); ok_beam(s)
    s.d["provenance"]["etabs_verified"] = True
    add("INVALID-021", "Synthetic data claiming ETABS verification", s,
        "Provenance says synthetic but etabs_verified = true. The two categories must never mix: reject.", ["provenance.type synthetic, etabs_verified true"], "reject snapshot")

    s = base("INVALID-022", "Unsupported section shape")
    s.raw_section({"name": "SD-SECTION-1", "kind": "Frame", "shape": "SectionDesigner"})
    s.beam("B1", "L1", "SD-SECTION-1", "C30", (0, 0, 3), (6, 0, 3)); ok_beam(s)
    add("INVALID-022", "Unsupported section shape", s, "Section-designer (arbitrary) section: volume unknown. Must not be guessed or zeroed.",
        ["B1 section shape SectionDesigner"], "unquantified, error, block finalization", [Hand("elements.B1.status", "Unquantified", "")])

    s = base("INVALID-023", "Missing section definition")
    s.beam("B1", "L1", "B999", "C30", (0, 0, 3), (6, 0, 3)); ok_beam(s)
    add("INVALID-023", "Missing section definition", s, "Element references a section that is not in the snapshot.", ["B1 section 'B999'"],
        "unquantified, error, block finalization")

    s = base("INVALID-024", "Beam width = 6 m (plausibility only)"); s.rect("B6000x600", 6.0, 0.6)
    s.beam("B1", "L1", "B6000x600", "C30", (0, 0, 3), (6, 0, 3))
    add("INVALID-024", "Beam width = 6 m (plausibility only)", s,
        "Unusual but not impossible (6/1000 = 6 mm is not a typical width, so no unit error). Warning only; finalization allowed.",
        ["B1 6.0 × 0.6 × 6 m"], "include, warn", [Hand("modelTotals.netConcreteM3", 21.6, "6 × 0.6 × 6"), Hand("finalization.allowed", True, "")])

    s = base("INVALID-025", "Empty model")
    add("INVALID-025", "Empty model", s, "No elements at all. Totals are zero (legitimately known), but finalizing an empty take-off is blocked.", ["no elements"],
        "warn, block finalization", [Hand("modelTotals.netConcreteM3", 0.0, "")])

    s = base("INVALID-026", "Unsupported schema version"); ok_beam(s)
    s.d["schemaVersion"] = "9.0"
    add("INVALID-026", "Unsupported schema version", s, "Future/unknown schema.", ["schemaVersion 9.0"], "reject snapshot")

    s = base("INVALID-027", "Missing provenance"); ok_beam(s)
    del s.d["provenance"]
    add("INVALID-027", "Missing provenance", s, "No provenance: the origin of the data is unknown. Reject.", ["no provenance block"], "reject snapshot")

    s = base("INVALID-028", "Slab mesh on irregular slab without region")
    s.d["materials"][1]["density"] = 7850.0
    s.slab("S1", "L1", "S200", "C30", [(0, 0, 3), (8, 0, 3), (8, 4, 3), (4, 4, 3), (4, 7, 3), (0, 7, 3)],
           rebar=provided([{"type": "Mesh", "role": "bottom", "direction": "X", "diameter": 0.012, "spacing": 0.2, "edgeOffset": 0.05, "endCover": 0.025, "faces": 1}]))
    add("INVALID-028", "Slab mesh on irregular slab without region", s,
        "A mesh over an L-shaped slab would be over-estimated if its bounding box were used. Quentra must refuse rather than guess.",
        ["S1 L-shaped, mesh T12@200 X, no region"], "error, steel unknown", [Hand("elements.S1.steelKg", None, "")], options={})
    return out


def naming():
    out = []
    s = Snap("NAME-001", "Messy real-world naming", stories=[("Base-L01", 3.5, 3.5), ("L02 (Typ)", 7.0, 3.5)])
    s.concrete("C30/37", 30).rect("BM 300X600", 0.3, 0.6).rect("COL-450SQ", 0.45, 0.45).slab_section("SLAB 200", 0.2).wall_section("Core Wall 300", 0.3)
    s.beam("frm-000417", "L02 (Typ)", "BM 300X600", "C30/37", (0, 0, 7), (6, 0, 7), name="Beam_A12")
    s.beam("frm-000418", "L02 (Typ)", "BM 300X600", "C30/37", (6, 0, 7), (12, 0, 7), name="B-01")
    s.column("frm-000102", "Base-L01", "COL-450SQ", "C30/37", (0, 0, 0), (0, 0, 3.5), name="COLUMN_CORE_3")
    s.beam("frm-000419", "L02 (Typ)", "BM 300X600", "C30/37", (0, 6, 7), (6, 6, 7), name="C5")
    s.wall("area-000031", "Base-L01", "Core Wall 300", "C30/37", [(20, 0, 0), (24, 0, 0), (24, 0, 3.5), (20, 0, 3.5)], name="Wall Pier 02", extra={"pier": "P2"})
    s.slab("area-000077", "L02 (Typ)", "SLAB 200", "C30/37", rect(0, 0, 12, 6, 7), name="SLAB-L02-A")
    s.source_warning("EXTRACT_ROUNDING", "Coordinates rounded to 1 mm during extraction (synthetic example of a source-reported note).")
    out.append(Case("NAME-001", "Messy real-world naming", "naming", "synthetic",
        "Names, IDs and story names in real-world styles, including a beam labelled 'C5'. Quentra must rely on the `type` field, not on names, and must pass the source's own warning through.",
        ["Beam 'Beam_A12', beam 'B-01', column 'COLUMN_CORE_3', beam labelled 'C5' (type Beam!), wall 'Wall Pier 02', slab 'SLAB-L02-A'",
         "Stories 'Base-L01' and 'L02 (Typ)'; section names with spaces; material 'C30/37'"], s.to_dict(), NO_STEEL,
        [Hand("byElementType.Beam.netConcreteM3", 3 * 1.08, "three beams incl. the one named 'C5'"),
         Hand("byElementType.Column.netConcreteM3", 0.45 * 0.45 * 3.5, "0.45² × 3.5"),
         Hand("byStory.Base-L01.netConcreteM3", 0.70875 + 4 * 3.5 * 0.3, "column 0.70875 + wall 4.2")],
        expected_behaviour="classify by type field; pass source warning through"))
    return out


def all_cases():
    return aggregation() + double_counting() + invalid() + naming()
