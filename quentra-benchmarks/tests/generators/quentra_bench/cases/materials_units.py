"""Material grouping and unit-system equivalence cases."""
import math
from ..snapshot import Snap, provided, bars, hoops, mesh
from ..case import Case, Hand
from ..units import kg_m3_to

ONE = [("L1", 3.0, 3.0)]
NO_STEEL = {"quantifySteel": False}


def materials():
    out = []
    s = Snap("MAT-001", "Multiple concrete grades", stories=ONE)
    s.concrete("C30", 30).concrete("C35", 35).concrete("C40", 40)
    s.rect("B300x600", 0.3, 0.6).rect("C400x400", 0.4, 0.4).slab_section("S200", 0.2)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.column("C1", "L1", "C400x400", "C40", (0, 0, 0), (0, 0, 3))
    s.slab("S1", "L1", "S200", "C35", [(0, 0, 3), (6, 0, 3), (6, 5, 3), (0, 5, 3)])
    out.append(Case("MAT-001", "Multiple concrete grades", "material", "synthetic",
        "Beam, column and slab in three grades; totals must be grouped by material.",
        ["B1 C30: 0.3 × 0.6 × 6", "C1 C40: 0.4 × 0.4 × 3", "S1 C35: 6 × 5 × 0.2"], s.to_dict(), NO_STEEL,
        [Hand("byMaterial.C30.netConcreteM3", 1.08, "0.3 × 0.6 × 6"), Hand("byMaterial.C40.netConcreteM3", 0.48, "0.4 × 0.4 × 3"),
         Hand("byMaterial.C35.netConcreteM3", 6.0, "6 × 5 × 0.2"), Hand("modelTotals.netConcreteM3", 7.56, "1.08 + 0.48 + 6.0")],
        expected_behaviour="group by material"))

    s = Snap("MAT-002", "Multiple steel grades", stories=ONE)
    s.concrete("C30", 30).rebar("B500", fy=500).rebar("Fe415", fy=415)
    s.rect("B300x600", 0.3, 0.6)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3), rebar=provided([bars(4, 0.020)], steel="B500"))
    s.beam("B2", "L1", "B300x600", "C30", (0, 5, 3), (6, 5, 3), rebar=provided([bars(4, 0.020)], steel="Fe415"))
    kg = 4 * math.pi * 0.02 ** 2 / 4 * 6 * 7850
    out.append(Case("MAT-002", "Multiple steel grades", "material", "synthetic",
        "Identical reinforcement in two steel grades. Steel mass is equal, but must be reported per grade.",
        ["B1: 4T20 × 6 m, grade B500", "B2: 4T20 × 6 m, grade Fe415", "both 7850 kg/m³"], s.to_dict(), {},
        [Hand("steelByMaterial.B500", kg, "4 × π × 0.020²/4 × 6 × 7850 = 59.1876"), Hand("steelByMaterial.Fe415", kg, "same"),
         Hand("modelTotals.steelKg", 2 * kg, "2 × 59.1876")], expected_behaviour="group steel by grade"))

    s = Snap("MAT-003", "Same geometry different materials", stories=ONE)
    s.concrete("C30", 30).concrete("C40", 40).rect("B300x600", 0.3, 0.6)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.beam("B2", "L1", "B300x600", "C40", (0, 5, 3), (6, 5, 3))
    out.append(Case("MAT-003", "Same geometry different materials", "material", "synthetic",
        "Two geometrically identical beams (different locations) in different grades must not be merged.",
        ["B1 C30 at y = 0; B2 C40 at y = 5; both 0.3 × 0.6 × 6"], s.to_dict(), NO_STEEL,
        [Hand("byMaterial.C30.netConcreteM3", 1.08, "0.3 × 0.6 × 6"), Hand("byMaterial.C40.netConcreteM3", 1.08, "0.3 × 0.6 × 6")],
        expected_behaviour="separate groups"))

    aliases = ["C30", "C30/37", "CONC30", "Concrete_30MPa"]

    def alias_snap(cid, name):
        s = Snap(cid, name, stories=ONE)
        for a in aliases:
            s.concrete(a, 30)
        s.rect("B300x600", 0.3, 0.6)
        for i, a in enumerate(aliases):
            s.beam(f"B{i + 1}", "L1", "B300x600", a, (0, 5 * i, 3), (6, 5 * i, 3))
        return s
    s = alias_snap("MAT-004", "Material aliases without mapping")
    out.append(Case("MAT-004", "Material aliases without mapping", "material", "synthetic",
        "Four names that a human would read as 'C30'. With no explicit mapping Quentra must keep four separate groups — no silent normalisation.",
        [f"B{i + 1}: 0.3 × 0.6 × 6 in material '{a}'" for i, a in enumerate(aliases)], s.to_dict(), NO_STEEL,
        [Hand(f"byMaterial.{a}.netConcreteM3", 1.08, "0.3 × 0.6 × 6") for a in ["C30", "CONC30", "Concrete_30MPa"]] +
        [Hand("modelTotals.netConcreteM3", 4.32, "4 × 1.08")],
        expected_behaviour="four separate groups, no warning"))

    s = alias_snap("MAT-005", "Material aliases with explicit mapping")
    mapping = {"C30/37": "C30", "CONC30": "C30", "Concrete_30MPa": "C30"}
    out.append(Case("MAT-005", "Material aliases with explicit mapping", "material", "synthetic",
        "Same snapshot as MAT-004, but the run supplies an explicit alias map. Now all four group under 'C30'.",
        ["Same as MAT-004", f"Run option materialAliases = {mapping}"], s.to_dict(), {"quantifySteel": False, "materialAliases": mapping},
        [Hand("byMaterial.C30.netConcreteM3", 4.32, "4 × 1.08 under one group")],
        notes=["The mapping lives in the run options (a Quentra project setting), not in the snapshot, so the captured ETABS data is never rewritten."],
        expected_behaviour="one group via explicit mapping"))

    s = Snap("MAT-006", "Material classification ambiguity", stories=ONE)
    s.concrete("C30", 30).material("MAT-X", "Other", grade=None).material("S355", "Steel", density=7850.0, grade="S355")
    s.rect("B300x600", 0.3, 0.6)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3))
    s.beam("B2", "L1", "B300x600", "MAT-X", (0, 5, 3), (6, 5, 3))
    s.beam("B3", "L1", "B300x600", "S355", (0, 10, 3), (6, 10, 3))
    out.append(Case("MAT-006", "Material classification ambiguity", "material", "synthetic",
        "B2's material type is 'Other' (not classifiable); B3 is structural steel. Only B1 is concrete. B2's volume is reported separately as unclassified, not guessed as concrete.",
        ["B1 C30 (Concrete)", "B2 MAT-X (type Other)", "B3 S355 (type Steel)", "all 0.3 × 0.6 × 6"], s.to_dict(), NO_STEEL,
        [Hand("modelTotals.netConcreteM3", 1.08, "B1 only"), Hand("modelTotals.unclassifiedVolumeM3", 1.08, "B2 volume, not concrete"),
         Hand("elements.B2.status", "Unclassified", ""), Hand("elements.B3.status", "Skipped", "")],
        expected_behaviour="B2 unclassified + error; B3 skipped + info; block finalization"))

    s = Snap("MAT-007", "Default steel density", stories=ONE)
    s.concrete("C30", 30).rect("B300x600", 0.3, 0.6)
    s.beam("B1", "L1", "B300x600", "C30", (0, 0, 3), (6, 0, 3), rebar={"source": "ProvidedBars", "components": [bars(4, 0.020)]})
    out.append(Case("MAT-007", "Default steel density", "material", "synthetic",
        "Reinforcement with no steel material named. The configured default (7850 kg/m³) is used and the fact is recorded.",
        ["B1: 4T20 × 6 m, no steelMaterial"], s.to_dict(), {},
        [Hand("modelTotals.steelKg", 4 * math.pi * 0.0001 * 6 * 7850, "4 × π × 0.02²/4 × 6 × 7850 = 59.1876"), Hand("steelByMaterial.<default>", 59.18760559307442, "")],
        expected_behaviour="compute with default, info warning"))
    return out


# ------------------------------------------------------------------ units

IN = 0.0254
UNIT_FACTOR_FROM_IN = {"in": 1.0, "ft": 1.0 / 12.0, "m": 0.0254, "cm": 2.54, "mm": 25.4}


def units_model(unit):
    """One physical model (defined in inches) re-expressed in `unit`.

    All inch dimensions are multiples of 0.375 in, which are exact in ft (k/32 ft) and exact decimals in mm/cm/m.
    """
    k = UNIT_FACTOR_FROM_IN[unit]
    q = lambda v: round(v * k, 12)
    P3 = lambda x, y, z: (q(x), q(y), q(z))
    s = Snap(f"UNITS-{unit.upper()}", f"Unit equivalence model ({unit})", unit=unit, stories=[("L1", q(144), q(144))])
    s.concrete("C4000", 27.6)
    if unit in ("ft", "in"):
        s.rebar("A615-60", density=kg_m3_to("lb/ft3", 7850.0), density_unit="lb/ft3", fy=414)
    else:
        s.rebar("A615-60", density=7850.0, fy=414)
    s.rect("COL18x18", q(18), q(18)).rect("BM12x24", q(12), q(24)).slab_section("SLAB6", q(6)).wall_section("WALL9", q(9))
    s.column("C1", "L1", "COL18x18", "C4000", P3(0, 0, 0), P3(0, 0, 144),
             rebar=provided([bars(4, q(0.75)), hoops(q(0.375), q(9), cover=q(1.5), end_offset=q(3), hook=q(6))], steel="A615-60"))
    s.beam("B1", "L1", "BM12x24", "C4000", P3(0, 0, 144), P3(240, 0, 144),
           rebar=provided([bars(3, q(0.75), role="bottom"), bars(2, q(0.75), role="top"),
                           hoops(q(0.375), q(6), cover=q(1.5), end_offset=q(3), hook=q(6))], steel="A615-60"))
    z = 144
    s.slab("S1", "L1", "SLAB6", "C4000", [P3(0, 12, z), P3(240, 12, z), P3(240, 192, z), P3(0, 192, z)],
           openings=[("O1", [P3(60, 72, z), P3(96, 72, z), P3(96, 120, z), P3(60, 120, z)])],
           rebar=provided([mesh("X", q(0.375), q(9), q(3), q(1.5)), mesh("Y", q(0.375), q(9), q(3), q(1.5))], steel="A615-60"))
    s.wall("W1", "L1", "WALL9", "C4000", [P3(300, 0, 0), P3(300, 120, 0), P3(300, 120, 144), P3(300, 0, 144)],
           openings=[("D1", [P3(300, 24, 0), P3(300, 60, 0), P3(300, 60, 84), P3(300, 24, 84)])],
           rebar=provided([mesh("Vertical", q(0.375), q(12), q(3), q(1.5), layer="vertical", faces=2),
                           mesh("Horizontal", q(0.375), q(12), q(3), q(1.5), layer="horizontal", faces=2)], steel="A615-60"))
    return s


def units_cases():
    out = []
    m = IN
    ab = lambda d: math.pi * (d * m) ** 2 / 4
    rho = 7850.0
    col_v = (18 * m) ** 2 * 144 * m
    beam_v = 12 * m * 24 * m * 240 * m
    slab_net = (240 * 180 - 36 * 48) * m * m * 6 * m
    wall_net = (120 * 144 - 36 * 84) * m * m * 9 * m
    # steel, by hand (all in inches first, then converted)
    col_long = 4 * ab(0.75) * 144 * m * rho
    col_hoop_n = math.floor((144 - 6) / 9) + 1            # 138/9 = 15.33 -> 16
    col_hoop = col_hoop_n * (2 * (14.625 + 14.625) + 6) * m * ab(0.375) * rho
    beam_long = 5 * ab(0.75) * 240 * m * rho
    beam_hoop_n = math.floor((240 - 6) / 6) + 1           # 234/6 = 39 -> 40
    beam_hoop = beam_hoop_n * (2 * (8.625 + 20.625) + 6) * m * ab(0.375) * rho
    slab_x = (math.floor((180 - 6) / 9) + 1) * (240 - 3) * m * ab(0.375) * rho   # 19.33 -> 20 bars
    slab_y = (math.floor((240 - 6) / 9) + 1) * (180 - 3) * m * ab(0.375) * rho   # 26 exactly -> 27 bars
    wall_v = 2 * (math.floor((120 - 6) / 12) + 1) * (144 - 3) * m * ab(0.375) * rho  # 9.5 -> 10
    wall_h = 2 * (math.floor((144 - 6) / 12) + 1) * (120 - 3) * m * ab(0.375) * rho  # 11.5 -> 12
    steel = col_long + col_hoop + beam_long + beam_hoop + slab_x + slab_y + wall_v + wall_h
    for unit in ["m", "mm", "cm", "ft", "in"]:
        s = units_model(unit)
        cid = f"UNITS-{unit.upper()}"
        out.append(Case(cid, f"Unit equivalence — {unit}", "units", "synthetic",
            f"One physical model (column, beam, slab with opening, wall with door, provided bars) expressed in **{unit}**. "
            "All five UNITS-* cases describe the same structure and must give identical physical quantities.",
            ["Model defined in inches (multiples of 0.375 in): column 18×18 in × 144 in, beam 12×24 in × 240 in, slab 240×180×6 in with a 36×48 in opening, wall 120×144×9 in with a 36×84 in door",
             "Bars: #6 = 0.75 in, #3 = 0.375 in; density 7850 kg/m³ (metric files) or its exact lb/ft³ equivalent (imperial files)",
             f"This file expresses every length in {unit} (1 in = {UNIT_FACTOR_FROM_IN[unit]} {unit})"],
            s.to_dict(), {},
            [Hand("byElementType.Column.netConcreteM3", col_v, "(18 × 0.0254)² × 144 × 0.0254"),
             Hand("byElementType.Beam.netConcreteM3", beam_v, "(12 × 24 × 240) in³ × 0.0254³"),
             Hand("byElementType.Slab.netConcreteM3", slab_net, "(240 × 180 − 36 × 48) × 6 in³ × 0.0254³"),
             Hand("byElementType.Wall.netConcreteM3", wall_net, "(120 × 144 − 36 × 84) × 9 in³ × 0.0254³"),
             Hand("modelTotals.netConcreteM3", col_v + beam_v + slab_net + wall_net, "sum"),
             Hand("elements.S1.steelComponentsKg.1", slab_y, "Y bars: floor((240 − 6)/9) + 1 = 26 + 1 = 27 — an exact division; the count must not drop to 26 from floating-point error"),
             Hand("modelTotals.steelKg", steel, "column 4#6 + 16 hoops; beam 5#6 + 40 hoops; slab 20 + 27 bars; wall 2 × 10 + 2 × 12 bars")],
            notes=["Exact-division bar counts are protected by adding 1e-9 before floor() (see CONVENTIONS.md §5).",
                   "The imperial density value is 7850 kg/m³ ÷ 16.018463373960138 (exact lb/ft³ → kg/m³ factor), written with full double precision."],
            equivalence_group="UNITS-A", expected_behaviour="identical physical quantities in every unit system"))
    return out


def all_cases():
    return materials() + units_cases()
