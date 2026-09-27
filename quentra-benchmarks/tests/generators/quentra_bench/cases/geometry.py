"""Groups A-C: basic frames, slabs and walls (concrete geometry only)."""
import math
from ..snapshot import Snap
from ..case import Case, Hand

ONE = [("L1", 3.0, 3.0)]
NO_STEEL = {"quantifySteel": False}


def _base(cid, name, stories=ONE, unit="m"):
    s = Snap(cid, name, unit=unit, stories=stories)
    s.concrete("C30", 30)
    return s


def frames():
    out = []

    s = _base("FRAME-001", "Single rectangular beam")
    s.rect("B300x600", 0.30, 0.60)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    out.append(Case("FRAME-001", "Single rectangular beam", "frame", "synthetic",
        "Smallest possible frame case: one prismatic rectangular beam. Verifies length from coordinates and A x L.",
        ["Beam B1: width 0.30 m, depth 0.60 m, from (0,0,3) to (6,0,3) → L = 6.00 m", "Concrete C30"],
        s.to_dict(), NO_STEEL,
        [Hand("modelTotals.grossConcreteM3", 0.30 * 0.60 * 6.00, "0.30 × 0.60 × 6.00 = 1.08"),
         Hand("modelTotals.netConcreteM3", 1.08, "no openings → net = gross"),
         Hand("elements.B1.lengthM", 6.0, "|(6,0,3) − (0,0,3)| = 6")],
        expected_behaviour="include"))

    s = _base("FRAME-002", "Single rectangular column")
    s.rect("C400x500", 0.40, 0.50)
    s.column("C1", "L1", "C400x500", "C30", (0, 0, 0), (0, 0, 3))
    out.append(Case("FRAME-002", "Single rectangular column", "frame", "synthetic",
        "One vertical rectangular column; length is the story height.",
        ["Column C1: 0.40 m × 0.50 m, from z = 0 to z = 3 m"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 0.40 * 0.50 * 3.0, "0.40 × 0.50 × 3.00 = 0.60")], expected_behaviour="include"))

    s = _base("FRAME-003", "Single circular column", stories=[("L1", 3.5, 3.5)])
    s.circle("C500D", 0.50)
    s.column("C1", "L1", "C500D", "C30", (2, 2, 0), (2, 2, 3.5))
    out.append(Case("FRAME-003", "Single circular column", "frame", "synthetic",
        "Circular section: A = πD²/4. Checks that Quentra does not use a square D × D.",
        ["Column C1: diameter 0.50 m, length 3.50 m"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", math.pi * 0.5 ** 2 / 4 * 3.5, "π × 0.5² / 4 × 3.5 = 0.21875π = 0.687223"),
         Hand("elements.C1.netConcreteM3", 0.21875 * math.pi, "square would give 0.875 — must NOT")], expected_behaviour="include"))

    s = _base("FRAME-004", "Two connected beams")
    s.rect("B300x600", 0.30, 0.60).rect("B300x500", 0.30, 0.50)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.beam("B2", "L1", "B300x500", "C30", (6, 0, 3), (10, 0, 3))
    out.append(Case("FRAME-004", "Two connected beams", "frame", "synthetic",
        "Two collinear beams sharing a node, different sections. Nothing is deducted at the shared node.",
        ["B1: 0.30 × 0.60, L = 6 m", "B2: 0.30 × 0.50, L = 4 m (shares node (6,0,3) with B1)"], s.to_dict(), NO_STEEL,
        [Hand("elements.B1.netConcreteM3", 0.3 * 0.6 * 6, "0.3 × 0.6 × 6 = 1.08"),
         Hand("elements.B2.netConcreteM3", 0.3 * 0.5 * 4, "0.3 × 0.5 × 4 = 0.60"),
         Hand("modelTotals.netConcreteM3", 1.68, "1.08 + 0.60 = 1.68")], expected_behaviour="include"))

    s = _base("FRAME-005", "Beam-column frame")
    s.rect("C400x400", 0.4, 0.4).rect("B300x600", 0.3, 0.6)
    s.column("C1", "L1", "C400x400", "C30", (0, 0, 0), (0, 0, 3))
    s.column("C2", "L1", "C400x400", "C30", (6, 0, 0), (6, 0, 3))
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    out.append(Case("FRAME-005", "Beam-column frame", "frame", "synthetic",
        "Portal frame. Documents the joint convention: members are measured on their analytical centre-lines and the beam-column joint volume is NOT deducted.",
        ["C1, C2: 0.40 × 0.40, z 0 → 3", "B1: 0.30 × 0.60, x 0 → 6 at z = 3 (centre-line to centre-line)"], s.to_dict(), NO_STEEL,
        [Hand("byElementType.Column.netConcreteM3", 2 * 0.4 * 0.4 * 3, "2 × 0.4 × 0.4 × 3 = 0.96"),
         Hand("byElementType.Beam.netConcreteM3", 1.08, "0.3 × 0.6 × 6 = 1.08"),
         Hand("modelTotals.netConcreteM3", 2.04, "0.96 + 1.08 = 2.04 (no joint deduction)")],
        notes=["The physically shared joint volume (2 × 0.4 × 0.3 × 0.6 ≈ 0.144 m³ if the beam stopped at column faces) is intentionally counted twice by this convention. A future 'clear-span' option would need its own benchmark."],
        expected_behaviour="include (no joint deduction)"))

    st2 = [("L1", 3.5, 3.5), ("L2", 6.5, 3.0)]
    s = _base("FRAME-006", "Two-story column", stories=st2)
    s.rect("C500x500", 0.5, 0.5)
    s.column("C1@L1", "L1", "C500x500", "C30", (0, 0, 0), (0, 0, 3.5), name="C1")
    s.column("C1@L2", "L2", "C500x500", "C30", (0, 0, 3.5), (0, 0, 6.5), name="C1")
    out.append(Case("FRAME-006", "Two-story column", "frame", "synthetic",
        "A column stack split per story, as ETABS does (same label, different object per story). Verifies story aggregation and that the shared label 'C1' does not merge the objects.",
        ["C1 at L1: 0.5 × 0.5, height 3.5 m", "C1 at L2: 0.5 × 0.5, height 3.0 m"], s.to_dict(), NO_STEEL,
        [Hand("byStory.L1.netConcreteM3", 0.5 * 0.5 * 3.5, "0.25 × 3.5 = 0.875"),
         Hand("byStory.L2.netConcreteM3", 0.5 * 0.5 * 3.0, "0.25 × 3.0 = 0.75"),
         Hand("modelTotals.netConcreteM3", 1.625, "0.875 + 0.75")], expected_behaviour="include"))

    s = _base("FRAME-007", "Sloped beam", stories=[("L1", 5.5, 5.5)])
    s.rect("B300x500", 0.3, 0.5)
    s.beam("RB1", "L1", "B300x500", "C30", (0, 0, 3), (6, 0, 5.5))
    out.append(Case("FRAME-007", "Sloped beam", "frame", "synthetic",
        "Sloped (raking) beam. Length is the true 3-D length, not the plan length.",
        ["RB1: 0.30 × 0.50 from (0,0,3) to (6,0,5.5): plan 6 m, rise 2.5 m"], s.to_dict(), NO_STEEL,
        [Hand("elements.RB1.lengthM", 6.5, "√(6² + 2.5²) = √42.25 = 6.5 (plan length 6 would be wrong)"),
         Hand("modelTotals.netConcreteM3", 0.3 * 0.5 * 6.5, "0.15 × 6.5 = 0.975")], expected_behaviour="include"))

    s = _base("FRAME-008", "Inclined column", stories=[("L1", 3.5, 3.5)])
    s.rect("C400x400", 0.4, 0.4)
    s.column("IC1", "L1", "C400x400", "C30", (0, 0, 0), (1.2, 0, 3.5))
    out.append(Case("FRAME-008", "Inclined column", "frame", "synthetic",
        "Inclined column; section is perpendicular to the member axis, so V = A × true length.",
        ["IC1: 0.40 × 0.40 from (0,0,0) to (1.2,0,3.5)"], s.to_dict(), NO_STEEL,
        [Hand("elements.IC1.lengthM", 3.7, "√(1.2² + 3.5²) = √13.69 = 3.7"),
         Hand("modelTotals.netConcreteM3", 0.16 * 3.7, "0.16 × 3.7 = 0.592")], expected_behaviour="include"))
    return out


def rect(x0, y0, x1, y1, z):
    return [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]


def slabs():
    out = []
    z = 3.0

    def base(cid, name, t=0.2, sec="S200"):
        s = _base(cid, name)
        s.slab_section(sec, t)
        return s

    s = base("SLAB-001", "Simple rectangular slab")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 6, 5, z))
    out.append(Case("SLAB-001", "Simple rectangular slab", "slab", "synthetic", "Rectangular slab, no openings.",
        ["S1: 6 m × 5 m, t = 0.20 m, at z = 3"], s.to_dict(), NO_STEEL,
        [Hand("elements.S1.grossAreaM2", 30.0, "6 × 5"), Hand("modelTotals.netConcreteM3", 6.0, "30 × 0.2")], expected_behaviour="include"))

    s = base("SLAB-002", "Irregular polygon slab")
    s.slab("S1", "L1", "S200", "C30", [(0, 0, z), (8, 0, z), (8, 4, z), (4, 4, z), (4, 7, z), (0, 7, z)])
    out.append(Case("SLAB-002", "Irregular polygon slab", "slab", "synthetic",
        "L-shaped (non-convex) slab. Area must come from the polygon, not its bounding box.",
        ["S1: L-shape (0,0)-(8,0)-(8,4)-(4,4)-(4,7)-(0,7), t = 0.20 m"], s.to_dict(), NO_STEEL,
        [Hand("elements.S1.grossAreaM2", 8 * 4 + 4 * 3, "8 × 4 + 4 × 3 = 44 (bounding box 56 would be wrong)"),
         Hand("modelTotals.netConcreteM3", 44 * 0.2, "44 × 0.2 = 8.8")], expected_behaviour="include"))

    s = base("SLAB-003", "Slab with one rectangular opening", t=0.25, sec="S250")
    s.slab("S1", "L1", "S250", "C30", rect(0, 0, 8, 6, z), openings=[("O1", rect(3, 2, 5, 3.5, z))])
    out.append(Case("SLAB-003", "Slab with one rectangular opening", "slab", "synthetic",
        "One rectangular opening fully inside the slab.",
        ["S1: 8 × 6 m, t = 0.25 m", "O1: 2.0 × 1.5 m at x 3–5, y 2–3.5"], s.to_dict(), NO_STEEL,
        [Hand("elements.S1.openingDeductedAreaM2", 3.0, "2 × 1.5"), Hand("elements.S1.netAreaM2", 45.0, "48 − 3"),
         Hand("modelTotals.grossConcreteM3", 12.0, "48 × 0.25"), Hand("modelTotals.netConcreteM3", 11.25, "45 × 0.25"),
         Hand("modelTotals.openingDeductionM3", 0.75, "3 × 0.25")], expected_behaviour="include, deduct opening"))

    nseg, r, cx, cy = 32, 0.6, 4.0, 3.0
    poly = [(cx + r * math.cos(2 * math.pi * k / nseg), cy + r * math.sin(2 * math.pi * k / nseg), z) for k in range(nseg)]
    s = base("SLAB-004", "Slab with circular-equivalent opening")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 8, 6, z), openings=[("O1", poly)])
    circ = 0.5 * nseg * r * r * math.sin(2 * math.pi / nseg)
    out.append(Case("SLAB-004", "Slab with circular-equivalent opening", "slab", "synthetic",
        "A circular shaft opening. ETABS-style models describe circles as polygons, so the snapshot carries a 32-sided inscribed polygon and the expected deduction is the polygon area, not πr².",
        ["S1: 8 × 6 m, t = 0.20 m", "O1: 32-gon inscribed in a circle r = 0.60 m centred at (4, 3)"], s.to_dict(), NO_STEEL,
        [Hand("elements.S1.openingDeductedAreaM2", circ, "½ × n × r² × sin(2π/n) = ½ × 32 × 0.36 × sin(π/16) = 1.123720"),
         Hand("modelTotals.netConcreteM3", (48 - circ) * 0.2, "(48 − 1.123720) × 0.2 = 9.375256")],
        notes=[f"A true circle would deduct πr² = {math.pi * 0.36:.6f} m² — 0.64 % more. Quentra must deduct the polygon it was given; it must not 'upgrade' polygons to circles."],
        expected_behaviour="include, deduct polygon area"))

    s = base("SLAB-005", "Slab with two separate openings")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 10, 8, z), openings=[("O1", rect(1, 1, 3, 3, z)), ("O2", rect(6, 4, 7, 7, z))])
    out.append(Case("SLAB-005", "Slab with two separate openings", "slab", "synthetic", "Two disjoint openings.",
        ["S1: 10 × 8 m, t = 0.20 m", "O1: 2 × 2 m; O2: 1 × 3 m; disjoint"], s.to_dict(), NO_STEEL,
        [Hand("elements.S1.openingAreaSumM2", 7.0, "4 + 3"), Hand("elements.S1.openingDeductedAreaM2", 7.0, "disjoint → union = sum"),
         Hand("modelTotals.netConcreteM3", 73 * 0.2, "(80 − 7) × 0.2 = 14.6")], expected_behaviour="include"))

    s = base("SLAB-006", "Slab with overlapping openings")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 10, 8, z), openings=[("O1", rect(2, 2, 5, 5, z)), ("O2", rect(4, 4, 7, 6, z))])
    out.append(Case("SLAB-006", "Slab with overlapping openings", "slab", "synthetic",
        "Two openings overlap. Deducting the sum would remove the 1 m² overlap twice; the union must be deducted.",
        ["S1: 10 × 8 m, t = 0.20 m", "O1: x 2–5, y 2–5 (9 m²)", "O2: x 4–7, y 4–6 (6 m²)", "Overlap: x 4–5, y 4–5 (1 m²)"], s.to_dict(), NO_STEEL,
        [Hand("elements.S1.openingAreaSumM2", 15.0, "9 + 6"), Hand("elements.S1.openingDeductedAreaM2", 14.0, "9 + 6 − 1 (union)"),
         Hand("modelTotals.netConcreteM3", 66 * 0.2, "(80 − 14) × 0.2 = 13.2 (sum would give 13.0)")],
        expected_behaviour="include, deduct union, info warning"))

    s = base("SLAB-007", "Opening partly outside slab boundary")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 6, 5, z), openings=[("O1", rect(5, 1, 7, 3, z))])
    out.append(Case("SLAB-007", "Opening partly outside slab boundary", "slab", "synthetic",
        "Opening straddles the slab edge. Only the part inside the slab may be deducted.",
        ["S1: 6 × 5 m, t = 0.20", "O1: x 5–7, y 1–3 (4 m²); x 6–7 lies outside the slab"], s.to_dict(), NO_STEEL,
        [Hand("elements.S1.openingDeductedAreaM2", 2.0, "clipped to x 5–6: 1 × 2"),
         Hand("modelTotals.netConcreteM3", 28 * 0.2, "(30 − 2) × 0.2 = 5.6 (unclipped would give 5.2)")],
        expected_behaviour="include, deduct clipped part, warn"))

    s = base("SLAB-008", "Opening completely outside slab")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 6, 5, z), openings=[("O1", rect(8, 1, 9, 2, z))])
    out.append(Case("SLAB-008", "Opening completely outside slab", "slab", "synthetic",
        "Opening associated with a slab it does not touch (typical extractor association bug). Deduct nothing, warn.",
        ["S1: 6 × 5 m, t = 0.20", "O1: x 8–9, y 1–2 — entirely outside"], s.to_dict(), NO_STEEL,
        [Hand("elements.S1.openingDeductedAreaM2", 0.0, "no intersection"), Hand("modelTotals.netConcreteM3", 6.0, "30 × 0.2")],
        expected_behaviour="include, deduct nothing, warn"))

    s = base("SLAB-009", "Two slabs sharing an edge")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 5, 4, z))
    s.slab("S2", "L1", "S200", "C30", rect(5, 0, 9, 4, z))
    out.append(Case("SLAB-009", "Two slabs sharing an edge", "slab", "synthetic",
        "Adjacent slabs touch along x = 5. Touching is not overlapping: no warning.",
        ["S1: x 0–5, y 0–4", "S2: x 5–9, y 0–4", "t = 0.20 m"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", (20 + 16) * 0.2, "(20 + 16) × 0.2 = 7.2")], expected_behaviour="include both, no warning"))

    s = base("SLAB-010", "Two slabs overlapping incorrectly")
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 6, 4, z))
    s.slab("S2", "L1", "S200", "C30", rect(4, 0, 10, 4, z))
    out.append(Case("SLAB-010", "Two slabs overlapping incorrectly", "slab", "synthetic",
        "Modelling error: two slabs overlap by 2 m × 4 m. Quentra must not silently merge or trim them; it counts both and blocks finalization.",
        ["S1: x 0–6, S2: x 4–10, both y 0–4, t = 0.20", "Overlap x 4–6: 8 m² (1.6 m³ double counted)"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", (24 + 24) * 0.2, "(24 + 24) × 0.2 = 9.6 (both counted)"),
         Hand("finalization.allowed", False, "SLAB_OVERLAP blocks")],
        expected_behaviour="include both, warn, block finalization"))
    return out


def wall_pts(x0, x1, z0, z1, y=0.0):
    return [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]


def wopen(u0, u1, v0, v1, y=0.0):
    return [(u0, y, v0), (u1, y, v0), (u1, y, v1), (u0, y, v1)]


def walls():
    out = []

    def base(cid, name, stories=ONE):
        s = _base(cid, name, stories=stories)
        s.wall_section("W200", 0.20).wall_section("W250", 0.25).wall_section("W300", 0.30)
        return s

    s = base("WALL-001", "Simple rectangular wall")
    s.wall("W1", "L1", "W250", "C30", wall_pts(0, 5, 0, 3))
    out.append(Case("WALL-001", "Simple rectangular wall", "wall", "synthetic", "Vertical rectangular wall.",
        ["W1: 5 m long × 3 m high, t = 0.25 m, in plane y = 0"], s.to_dict(), NO_STEEL,
        [Hand("elements.W1.grossAreaM2", 15.0, "5 × 3"), Hand("modelTotals.netConcreteM3", 3.75, "15 × 0.25")], expected_behaviour="include"))

    s = base("WALL-002", "Wall with door opening")
    s.wall("W1", "L1", "W200", "C30", wall_pts(0, 5, 0, 3), openings=[("D1", wopen(1, 2, 0, 2.1))])
    out.append(Case("WALL-002", "Wall with door opening", "wall", "synthetic",
        "A door opening whose sill coincides with the wall base. Touching the host edge is not 'outside': no clipping warning.",
        ["W1: 5 × 3 m, t = 0.20", "D1: 1.0 m wide × 2.1 m high at u 1–2, from the base"], s.to_dict(), NO_STEEL,
        [Hand("elements.W1.openingDeductedAreaM2", 2.1, "1.0 × 2.1"), Hand("modelTotals.grossConcreteM3", 3.0, "15 × 0.2"),
         Hand("modelTotals.netConcreteM3", 2.58, "(15 − 2.1) × 0.2")], expected_behaviour="include"))

    s = base("WALL-003", "Wall with two openings")
    s.wall("W1", "L1", "W200", "C30", wall_pts(0, 6, 0, 3), openings=[("D1", wopen(0.5, 1.5, 0, 2.1)), ("WN1", wopen(3, 4.5, 1, 2.2))])
    out.append(Case("WALL-003", "Wall with two openings", "wall", "synthetic", "Door plus window, disjoint.",
        ["W1: 6 × 3 m, t = 0.20", "D1: 1.0 × 2.1 m", "WN1: 1.5 × 1.2 m, sill at 1.0 m"], s.to_dict(), NO_STEEL,
        [Hand("elements.W1.openingDeductedAreaM2", 2.1 + 1.8, "2.1 + 1.8 = 3.9"), Hand("modelTotals.netConcreteM3", 14.1 * 0.2, "(18 − 3.9) × 0.2 = 2.82")],
        expected_behaviour="include"))

    s = base("WALL-004", "Wall with overlapping openings")
    s.wall("W1", "L1", "W200", "C30", wall_pts(0, 6, 0, 3), openings=[("O1", wopen(1, 3, 0.5, 2)), ("O2", wopen(2, 4, 1, 2.5))])
    out.append(Case("WALL-004", "Wall with overlapping openings", "wall", "synthetic", "Two overlapping wall openings: deduct the union.",
        ["W1: 6 × 3 m, t = 0.20", "O1: u 1–3, v 0.5–2 (3 m²)", "O2: u 2–4, v 1–2.5 (3 m²)", "overlap u 2–3, v 1–2 (1 m²)"], s.to_dict(), NO_STEEL,
        [Hand("elements.W1.openingDeductedAreaM2", 5.0, "3 + 3 − 1"), Hand("modelTotals.netConcreteM3", 13 * 0.2, "(18 − 5) × 0.2 = 2.6")],
        expected_behaviour="include, deduct union, info warning"))

    s = base("WALL-005", "Wall with opening partly outside boundary")
    s.wall("W1", "L1", "W250", "C30", wall_pts(0, 5, 0, 3), openings=[("O1", wopen(4, 6, 1, 2))])
    out.append(Case("WALL-005", "Wall with opening partly outside boundary", "wall", "synthetic", "Opening extends past the wall end.",
        ["W1: 5 × 3 m, t = 0.25", "O1: u 4–6, v 1–2 (2 m²), half outside"], s.to_dict(), NO_STEEL,
        [Hand("elements.W1.openingDeductedAreaM2", 1.0, "clipped to u 4–5"), Hand("modelTotals.netConcreteM3", 14 * 0.25, "(15 − 1) × 0.25 = 3.5")],
        expected_behaviour="include, deduct clipped part, warn"))

    s = base("WALL-006", "Sloped/inclined wall", stories=[("L1", 3.5, 3.5)])
    s.wall("W1", "L1", "W200", "C30", [(0, 0, 0), (4, 0, 0), (4, 1.2, 3.5), (0, 1.2, 3.5)])
    out.append(Case("WALL-006", "Sloped/inclined wall", "wall", "synthetic",
        "Inclined wall: area is measured in the wall's own plane (true area), not its vertical projection.",
        ["W1: base (0,0,0)–(4,0,0), top (0,1.2,3.5)–(4,1.2,3.5); t = 0.20 measured normal to the wall"], s.to_dict(), NO_STEEL,
        [Hand("elements.W1.grossAreaM2", 4 * 3.7, "4 × √(1.2² + 3.5²) = 4 × 3.7 = 14.8 (vertical projection 14.0 would be wrong)"),
         Hand("modelTotals.netConcreteM3", 14.8 * 0.2, "14.8 × 0.2 = 2.96")], expected_behaviour="include"))

    st2 = [("L1", 3.5, 3.5), ("L2", 6.5, 3.0)]
    s = base("WALL-007", "Wall split across two stories", stories=st2)
    s.wall("W1@L1", "L1", "W250", "C30", wall_pts(0, 6, 0, 3.5), name="W1")
    s.wall("W1@L2", "L2", "W250", "C30", wall_pts(0, 6, 3.5, 6.5), name="W1")
    out.append(Case("WALL-007", "Wall split across two stories", "wall", "synthetic", "One wall line modelled as two story objects.",
        ["W1@L1: 6 × 3.5 m", "W1@L2: 6 × 3.0 m", "t = 0.25"], s.to_dict(), NO_STEEL,
        [Hand("byStory.L1.netConcreteM3", 6 * 3.5 * 0.25, "6 × 3.5 × 0.25 = 5.25"), Hand("byStory.L2.netConcreteM3", 6 * 3 * 0.25, "6 × 3 × 0.25 = 4.5"),
         Hand("modelTotals.netConcreteM3", 9.75, "5.25 + 4.5")], expected_behaviour="include"))

    s = base("WALL-008", "Wall with changing thickness between stories", stories=st2)
    s.wall("W1@L1", "L1", "W300", "C30", wall_pts(0, 6, 0, 3.5), name="W1")
    s.wall("W1@L2", "L2", "W200", "C30", wall_pts(0, 6, 3.5, 6.5), name="W1")
    out.append(Case("WALL-008", "Wall with changing thickness between stories", "wall", "synthetic", "Thickness steps from 300 to 200 mm at L2.",
        ["W1@L1: 6 × 3.5 m, t = 0.30", "W1@L2: 6 × 3.0 m, t = 0.20"], s.to_dict(), NO_STEEL,
        [Hand("byStory.L1.netConcreteM3", 6.3, "21 × 0.3"), Hand("byStory.L2.netConcreteM3", 3.6, "18 × 0.2"), Hand("modelTotals.netConcreteM3", 9.9, "6.3 + 3.6")],
        expected_behaviour="include"))
    return out


def all_cases():
    return frames() + slabs() + walls()
