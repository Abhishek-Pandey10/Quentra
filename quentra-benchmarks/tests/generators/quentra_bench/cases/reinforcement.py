"""Reinforcement cases: beams, stirrups, columns, slabs, walls, source classification, required areas, plausibility."""
import math
from ..snapshot import Snap, provided, bars, hoops, crossties, mesh, required, ratio, fixed_mass
from ..case import Case, Hand

ONE = [("L1", 3.0, 3.0)]
RHO = 7850.0


def A(d):
    return math.pi * d * d / 4


def beam_snap(cid, name, rebar, section=(0.3, 0.6), length=6.0):
    s = Snap(cid, name, stories=ONE)
    s.concrete("C30", 30).rebar("B500", fy=500)
    s.rect("B", *section)
    s.beam("B1", "L1", "B", "C30", (0, 0, 3), (length, 0, 3), rebar=rebar)
    return s


def beams():
    out = []
    specs = [
        ("RBEAM-001", "Beam longitudinal 2T16", [bars(2, 0.016, role="bottom")],
         [("2 × A(16) × 6 × 7850", 2 * A(0.016) * 6 * RHO)]),
        ("RBEAM-002", "Beam longitudinal 4T20", [bars(4, 0.020, role="bottom")],
         [("4 × A(20) × 6 × 7850", 4 * A(0.020) * 6 * RHO)]),
        ("RBEAM-003", "Beam longitudinal 6T25", [bars(6, 0.025, role="bottom")],
         [("6 × A(25) × 6 × 7850", 6 * A(0.025) * 6 * RHO)]),
        ("RBEAM-004", "Different top and bottom reinforcement", [bars(4, 0.020, role="bottom"), bars(2, 0.016, role="top")],
         [("bottom 4T20: 4 × A(20) × 6 × 7850", 4 * A(0.020) * 6 * RHO), ("top 2T16: 2 × A(16) × 6 × 7850", 2 * A(0.016) * 6 * RHO)]),
        ("RBEAM-005", "Bars with different lengths", [bars(3, 0.020, role="bottom"), bars(2, 0.020, length=4.5, role="bottom-curtailed"), bars(2, 0.016, role="top")],
         [("3T20 full length: 3 × A(20) × 6 × 7850", 3 * A(0.020) * 6 * RHO), ("2T20 curtailed 4.5 m: 2 × A(20) × 4.5 × 7850", 2 * A(0.020) * 4.5 * RHO),
          ("2T16 top: 2 × A(16) × 6 × 7850", 2 * A(0.016) * 6 * RHO)]),
        ("RBEAM-006", "Additional support bars", [bars(3, 0.020, role="bottom"), bars(2, 0.016, role="top"),
                                                  bars(2, 0.020, length=2.0, role="support-top-left"), bars(2, 0.020, length=2.0, role="support-top-right")],
         [("bottom 3T20 × 6", 3 * A(0.020) * 6 * RHO), ("top 2T16 × 6", 2 * A(0.016) * 6 * RHO),
          ("left support 2T20 × 2.0", 2 * A(0.020) * 2.0 * RHO), ("right support 2T20 × 2.0", 2 * A(0.020) * 2.0 * RHO)]),
    ]
    for cid, name, comps, parts in specs:
        s = beam_snap(cid, name, provided(comps))
        hand = [Hand(f"elements.B1.steelComponentsKg.{i}", v, w) for i, (w, v) in enumerate(parts)]
        hand.append(Hand("modelTotals.steelKg", sum(v for _, v in parts), " + ".join(w.split(':')[0] for w, _ in parts)))
        out.append(Case(cid, name, "reinforcement", "synthetic",
            "Provided longitudinal bars on a 300 × 600 × 6000 mm beam. Steel volume = n × πd²/4 × L; mass = volume × 7850 kg/m³.",
            ["B1: 0.30 × 0.60 m, L = 6.00 m, C30", "Bars: " + "; ".join(f"{c['count']}T{int(round(c['diameter'] * 1000))} {c['role']} L = {c.get('length', 6.0)} m" for c in comps),
             "A(d) = π d² / 4; A(16) = 2.010619e-4 m², A(20) = 3.141593e-4 m², A(25) = 4.908739e-4 m²"],
            s.to_dict(), {}, hand, expected_behaviour="include, exact steel"))
    return out


def stirrups():
    out = []
    # common: 300 x 600, T10, cover 25 mm to outside of stirrup, first stirrup 50 mm from each end, hook allowance 2 x 10d = 0.2 m
    w, h = 0.3 - 2 * 0.025 - 0.010, 0.6 - 2 * 0.025 - 0.010   # 0.24, 0.54
    piece = 2 * (w + h) + 0.2                                   # 1.76
    N150 = math.floor((6.0 - 0.1) / 0.15) + 1                  # 39.33 -> 40
    kg_outer = N150 * piece * A(0.010) * RHO
    s = beam_snap("RSTIR-001", "Beam stirrups 2-leg T10@150", provided([hoops(0.010, 0.150, cover=0.025, end_offset=0.05, hook=0.2)]))
    out.append(Case("RSTIR-001", "Beam stirrups 2-leg T10@150", "reinforcement", "synthetic",
        "The prompt's reference stirrup case: closed 2-leg hoop, T10 at 150 mm on a 300 × 600 mm beam.",
        ["B1: 0.30 × 0.60 × 6.00 m", "Stirrup T10, 2 legs (one closed hoop), spacing 150 mm",
         "Detailing assumptions (explicit in the snapshot): clear cover 25 mm to the outside of the stirrup; first/last stirrup 50 mm from the member ends; hook allowance 0.20 m per stirrup (2 × 10d)"],
        s.to_dict(), {},
        [Hand("elements.B1.steelComponentsKg.0", kg_outer,
              "N = floor((6.0 − 2 × 0.05)/0.15) + 1 = floor(39.33) + 1 = 40; centre-line 0.24 × 0.54 m; piece = 2 × (0.24 + 0.54) + 0.20 = 1.76 m; 40 × 1.76 = 70.4 m × A(10) × 7850 = 43.404 kg")],
        notes=["Centre-line dimension = section − 2 × cover − stirrup diameter (cover is to the outside face of the stirrup).",
               "Hook allowance is a single explicit length per stirrup; codes differ (135° hooks, 6d/10d/75 mm minimum) so the benchmark never assumes one."],
        expected_behaviour="include, exact steel"))

    inner_piece = 2 * (0.10 + 0.54) + 0.2
    s = beam_snap("RSTIR-002", "Beam stirrups 4-leg (outer + inner hoop)",
                  provided([hoops(0.010, 0.150, cover=0.025, end_offset=0.05, hook=0.2, role="outer"),
                            hoops(0.010, 0.150, end_offset=0.05, hook=0.2, cl_width=0.10, cl_height=0.54, role="inner")]))
    out.append(Case("RSTIR-002", "Beam stirrups 4-leg (outer + inner hoop)", "reinforcement", "synthetic",
        "4-leg stirrups detailed as an outer closed hoop plus an inner closed hoop around the middle bars.",
        ["Outer hoop as RSTIR-001", "Inner hoop: explicit centre-line 0.10 × 0.54 m, same spacing and hooks"], s.to_dict(), {},
        [Hand("elements.B1.steelComponentsKg.0", kg_outer, "as RSTIR-001"),
         Hand("elements.B1.steelComponentsKg.1", N150 * inner_piece * A(0.010) * RHO, "40 × (2 × (0.10 + 0.54) + 0.2) = 40 × 1.48 = 59.2 m × A(10) × 7850")],
        expected_behaviour="include"))

    ct_piece = 0.54 + 0.2
    s = beam_snap("RSTIR-003", "Beam stirrups 3-leg (hoop + crosstie)",
                  provided([hoops(0.010, 0.150, cover=0.025, end_offset=0.05, hook=0.2), crossties(0.010, 0.150, ct_piece, end_offset=0.05)]))
    out.append(Case("RSTIR-003", "Beam stirrups 3-leg (hoop + crosstie)", "reinforcement", "synthetic",
        "Odd leg count: closed hoop plus one straight crosstie at every stirrup location.",
        ["Hoop as RSTIR-001", "Crosstie T10, piece length 0.54 + 0.20 hooks = 0.74 m, same spacing"], s.to_dict(), {},
        [Hand("elements.B1.steelComponentsKg.1", N150 * ct_piece * A(0.010) * RHO, "40 × 0.74 = 29.6 m × A(10) × 7850")],
        expected_behaviour="include"))

    n_end = 15   # by hand: (1.5 - 0.1)/0.1 = 14 exactly -> 14 + 1
    n_mid = 15   # by hand: (3.0 - 0.2)/0.2 = 14 exactly -> 14 + 1
    s = beam_snap("RSTIR-004", "Beam stirrups with spacing zones",
                  provided([hoops(0.010, 0.10, cover=0.025, end_offset=0.05, zone_length=1.5, hook=0.2, role="end-zone-left"),
                            hoops(0.010, 0.20, cover=0.025, end_offset=0.10, zone_length=3.0, hook=0.2, role="mid-zone"),
                            hoops(0.010, 0.10, cover=0.025, end_offset=0.05, zone_length=1.5, hook=0.2, role="end-zone-right")]))
    out.append(Case("RSTIR-004", "Beam stirrups with spacing zones", "reinforcement", "synthetic",
        "Confinement zones at 100 mm near supports, 200 mm mid-span. Each zone is counted on its own; both counts are exact divisions and test the floor() tolerance.",
        ["End zones: 1.5 m each, T10@100, 50 mm offset → (1.5 − 0.1)/0.1 = 14 exactly → 15 stirrups",
         "Mid zone: 3.0 m, T10@200, 100 mm offset → (3.0 − 0.2)/0.2 = 14 exactly → 15 stirrups",
         "Zones do not share stirrups: the author chose offsets so that zone boundaries are not double-populated"],
        s.to_dict(), {},
        [Hand("elements.B1.steelComponentsKg.0", n_end * piece * A(0.010) * RHO, "15 × 1.76 m × A(10) × 7850"),
         Hand("elements.B1.steelComponentsKg.1", n_mid * piece * A(0.010) * RHO, "15 × 1.76 m × A(10) × 7850"),
         Hand("modelTotals.steelKg", 45 * piece * A(0.010) * RHO, "45 stirrups × 1.76 m × A(10) × 7850")],
        notes=["In floating point (1.5 − 0.1)/0.1 = 13.999999999999998; without the 1e-9 guard the count would be 14 instead of 15."],
        expected_behaviour="include"))
    return out


def col_snap(cid, name, stories=ONE):
    s = Snap(cid, name, stories=stories)
    s.concrete("C40", 40).rebar("B500", fy=500)
    return s


def columns():
    out = []
    # common tie piece for rect columns: explicit via cover
    def rect_tie(b, cov, d, hook):
        w = b - 2 * cov - d
        return 4 * w + hook

    s = col_snap("RCOL-001", "Column 4 bars + ties")
    s.rect("C400", 0.4, 0.4)
    s.column("C1", "L1", "C400", "C40", (0, 0, 0), (0, 0, 3.0),
             rebar=provided([bars(4, 0.016, role="longitudinal"), hoops(0.008, 0.20, cover=0.04, end_offset=0.05, hook=0.16)]))
    nt = math.floor((3.0 - 0.1) / 0.2) + 1   # 14.5 -> 15
    out.append(Case("RCOL-001", "Column 4 bars + ties", "reinforcement", "synthetic",
        "Corner bars only. Longitudinal and tie steel are reported as separate components.",
        ["C1: 0.40 × 0.40 × 3.0 m", "4T16 full height", "Ties T8@200, cover 40 mm, 50 mm end offset, hook allowance 0.16 m"], s.to_dict(), {},
        [Hand("elements.C1.steelComponentsKg.0", 4 * A(0.016) * 3.0 * RHO, "4 × A(16) × 3.0 × 7850 = 18.940 kg"),
         Hand("elements.C1.steelComponentsKg.1", nt * rect_tie(0.4, 0.04, 0.008, 0.16) * A(0.008) * RHO,
              "N = floor(2.9/0.2) + 1 = 15; centre-line 0.4 − 0.08 − 0.008 = 0.312; piece = 4 × 0.312 + 0.16 = 1.408 m")],
        expected_behaviour="include"))

    s = col_snap("RCOL-002", "Column 8 bars + ties + crossties")
    s.rect("C500", 0.5, 0.5)
    ct = 0.5 - 2 * 0.04 - 0.010 + 0.2
    s.column("C1", "L1", "C500", "C40", (0, 0, 0), (0, 0, 3.0),
             rebar=provided([bars(8, 0.020), hoops(0.010, 0.15, cover=0.04, end_offset=0.05, hook=0.2),
                             crossties(0.010, 0.15, ct, end_offset=0.05, per_location=2)]))
    nt = math.floor(2.9 / 0.15) + 1   # 19.33 -> 20
    out.append(Case("RCOL-002", "Column 8 bars + ties + crossties", "reinforcement", "synthetic",
        "8 bars (corners + mid-faces) restrained by a perimeter hoop and two crossties per level.",
        ["C1: 0.50 × 0.50 × 3.0 m", "8T20", "Hoop T10@150, cover 40 mm", "2 crossties T10 per level, piece 0.41 + 0.2 = 0.61 m"], s.to_dict(), {},
        [Hand("elements.C1.steelComponentsKg.0", 8 * A(0.02) * 3 * RHO, "8 × A(20) × 3 × 7850 = 59.188 kg"),
         Hand("elements.C1.steelComponentsKg.1", nt * rect_tie(0.5, 0.04, 0.01, 0.2) * A(0.01) * RHO, "N = 20; piece 4 × 0.41 + 0.2 = 1.84 m"),
         Hand("elements.C1.steelComponentsKg.2", nt * 2 * ct * A(0.01) * RHO, "20 × 2 × 0.61 m × A(10) × 7850")],
        expected_behaviour="include"))

    s = col_snap("RCOL-003", "Column 12 bars + double hoops")
    s.rect("C600", 0.6, 0.6)
    s.column("C1", "L1", "C600", "C40", (0, 0, 0), (0, 0, 3.0),
             rebar=provided([bars(12, 0.025), hoops(0.010, 0.15, cover=0.04, end_offset=0.05, hook=0.2),
                             hoops(0.010, 0.15, end_offset=0.05, hook=0.2, cl_width=0.26, cl_height=0.26, sets=2, role="inner-diamond-equivalent")]))
    out.append(Case("RCOL-003", "Column 12 bars + double hoops", "reinforcement", "synthetic",
        "12 bars with an outer hoop and two inner hoops per level (setsPerLocation = 2).",
        ["C1: 0.60 × 0.60 × 3.0 m", "12T25", "Outer hoop T10@150 (cover 40)", "2 inner hoops T10@150, centre-line 0.26 × 0.26 m"], s.to_dict(), {},
        [Hand("elements.C1.steelComponentsKg.0", 12 * A(0.025) * 3 * RHO, "12 × A(25) × 3 × 7850 = 138.72 kg"),
         Hand("elements.C1.steelComponentsKg.2", 20 * 2 * (2 * 0.52 + 0.2) * A(0.01) * RHO, "20 levels × 2 sets × (2 × (0.26 + 0.26) + 0.2) m")],
        expected_behaviour="include"))

    s = col_snap("RCOL-004", "Circular column with circular hoops", stories=[("L1", 3.5, 3.5)])
    s.circle("C600D", 0.6)
    s.column("C1", "L1", "C600D", "C40", (0, 0, 0), (0, 0, 3.5),
             rebar=provided([bars(8, 0.020), hoops(0.010, 0.15, cover=0.04, end_offset=0.05, hook=0.2)]))
    nt = math.floor((3.5 - 0.1) / 0.15) + 1   # 22.67 -> 23
    Dc = 0.6 - 0.08 - 0.01
    out.append(Case("RCOL-004", "Circular column with circular hoops", "reinforcement", "synthetic",
        "Circular hoops: piece length = π × centre-line diameter + hook allowance.",
        ["C1: D = 0.60 m, L = 3.5 m", "8T20", "Circular hoops T10@150, cover 40 mm"], s.to_dict(), {},
        [Hand("elements.C1.steelComponentsKg.1", nt * (math.pi * Dc + 0.2) * A(0.01) * RHO, "N = floor(3.4/0.15) + 1 = 23; Dc = 0.6 − 0.08 − 0.01 = 0.51; piece = π × 0.51 + 0.2 = 1.8022 m")],
        expected_behaviour="include"))

    st = [("L1", 4.0, 4.0), ("L2", 7.2, 3.2)]
    s = col_snap("RCOL-005", "Column reinforcement varying by story", stories=st)
    s.rect("C500", 0.5, 0.5)
    s.column("C1@L1", "L1", "C500", "C40", (0, 0, 0), (0, 0, 4.0), name="C1",
             rebar=provided([bars(8, 0.025, length=5.0, role="longitudinal+lap"), hoops(0.010, 0.15, cover=0.04, end_offset=0.05, hook=0.2)]))
    s.column("C1@L2", "L2", "C500", "C40", (0, 0, 4.0), (0, 0, 7.2), name="C1",
             rebar=provided([bars(8, 0.020, length=4.0, role="longitudinal+lap"), hoops(0.008, 0.20, cover=0.04, end_offset=0.05, hook=0.16)]))
    n1 = 27   # by hand: 3.9/0.15 = 26 exactly -> 26 + 1
    n2 = math.floor(3.1 / 0.20) + 1   # 15.5 -> 16
    l1 = 8 * A(0.025) * 5.0 * RHO + n1 * rect_tie(0.5, 0.04, 0.01, 0.2) * A(0.01) * RHO
    l2 = 8 * A(0.020) * 4.0 * RHO + n2 * rect_tie(0.5, 0.04, 0.008, 0.16) * A(0.008) * RHO
    out.append(Case("RCOL-005", "Column reinforcement varying by story", "reinforcement", "synthetic",
        "Story heights 4.0 m and 3.2 m, reinforcement reduces up the building. Bar lengths include an explicit lap allowance.",
        ["C1@L1: 0.5 × 0.5 × 4.0 m, 8T25 × 5.0 m (incl. 1.0 m lap), ties T10@150",
         "C1@L2: 0.5 × 0.5 × 3.2 m, 8T20 × 4.0 m (incl. 0.8 m lap), ties T8@200"], s.to_dict(), {},
        [Hand("byStory.L1.steelKg", l1, "8 × A(25) × 5.0 × 7850 + 27 × 1.84 × A(10) × 7850 (3.9/0.15 = 26 exactly → 27)"),
         Hand("byStory.L2.steelKg", l2, "8 × A(20) × 4.0 × 7850 + 16 × 1.808 × A(8) × 7850"),
         Hand("modelTotals.steelKg", l1 + l2, "sum")],
        expected_behaviour="include"))
    return out


def slab_snap(cid, name, lx, ly, comps, t=0.2):
    s = Snap(cid, name, stories=ONE)
    s.concrete("C30", 30).rebar("B500", fy=500).slab_section("S", t)
    s.slab("S1", "L1", "S", "C30", [(0, 0, 3), (lx, 0, 3), (lx, ly, 3), (0, ly, 3)], rebar=provided(comps))
    return s


def mesh_kg(along, across, d, s, edge=0.05, endc=0.025, faces=1):
    Nb = math.floor((across - 2 * edge) / s + 1e-9) + 1
    return faces * Nb * (along - 2 * endc) * A(d) * RHO, Nb


def slabs():
    out = []
    PROV = "Provided-reinforcement benchmark (drawn bars), not ETABS design output."
    kx, nx = mesh_kg(5, 4, 0.012, 0.2)
    ky, ny = mesh_kg(4, 5, 0.012, 0.2)
    s = slab_snap("RSLAB-001", "Slab bottom mesh T12@200 both ways", 5, 4, [mesh("X", 0.012, 0.2, 0.05, 0.025), mesh("Y", 0.012, 0.2, 0.05, 0.025)])
    out.append(Case("RSLAB-001", "Slab bottom mesh T12@200 both ways", "reinforcement", "synthetic",
        "The prompt's reference slab: 5 × 4 m, bottom T12@200 in X and Y. " + PROV,
        ["S1: 5 × 4 m, t = 0.20", "Bottom X and Y: T12@200", "First bar 50 mm from the slab edge (edgeOffset); bar ends 25 mm from the edges (endCover)"], s.to_dict(), {},
        [Hand("elements.S1.steelComponentsKg.0", kx, f"X bars: N = floor((4 − 0.1)/0.2) + 1 = {nx}; length 5 − 0.05 = 4.95 m; {nx} × 4.95 × A(12) × 7850"),
         Hand("elements.S1.steelComponentsKg.1", ky, f"Y bars: N = floor((5 − 0.1)/0.2) + 1 = {ny}; length 3.95 m"),
         Hand("modelTotals.steelKg", kx + ky, "sum")], expected_behaviour="include, exact steel"))

    comps = [mesh("X", 0.012, 0.2, 0.05, 0.025, "bottom"), mesh("Y", 0.012, 0.2, 0.05, 0.025, "bottom"),
             mesh("X", 0.010, 0.2, 0.05, 0.025, "top"), mesh("Y", 0.010, 0.2, 0.05, 0.025, "top")]
    s = slab_snap("RSLAB-002", "Slab top and bottom mesh", 5, 4, comps)
    tx, _ = mesh_kg(5, 4, 0.010, 0.2)
    ty, _ = mesh_kg(4, 5, 0.010, 0.2)
    out.append(Case("RSLAB-002", "Slab top and bottom mesh", "reinforcement", "synthetic", "Adds a top mesh T10@200 both ways. " + PROV,
        ["As RSLAB-001 plus top T10@200 X and Y"], s.to_dict(), {},
        [Hand("modelTotals.steelKg", kx + ky + tx + ty, "bottom (RSLAB-001) + top X + top Y")], expected_behaviour="include"))

    s = slab_snap("RSLAB-003", "Slab with different X/Y spacing", 6, 5, [mesh("X", 0.012, 0.15, 0.05, 0.025), mesh("Y", 0.010, 0.25, 0.05, 0.025)])
    a, na = mesh_kg(6, 5, 0.012, 0.15)
    b, nb = mesh_kg(5, 6, 0.010, 0.25)
    out.append(Case("RSLAB-003", "Slab with different X/Y spacing", "reinforcement", "synthetic", "Main steel T12@150 in X, distribution T10@250 in Y. " + PROV,
        ["S1: 6 × 5 m", "X: T12@150", "Y: T10@250"], s.to_dict(), {},
        [Hand("elements.S1.steelComponentsKg.0", a, f"N = floor(4.9/0.15) + 1 = {na}; length 5.95 m"),
         Hand("elements.S1.steelComponentsKg.1", b, f"N = floor(5.9/0.25) + 1 = {nb}; length 4.95 m")], expected_behaviour="include"))

    comps = [mesh("X", 0.012, 0.2, 0.05, 0.025), mesh("Y", 0.012, 0.2, 0.05, 0.025),
             mesh("X", 0.012, 0.15, 0.05, 0.025, layer="top-support-left", region={"xMin": 0, "xMax": 1.25, "yMin": 0, "yMax": 5}),
             mesh("X", 0.012, 0.15, 0.05, 0.025, layer="top-support-right", region={"xMin": 4.75, "xMax": 6, "yMin": 0, "yMax": 5})]
    s = slab_snap("RSLAB-004", "Slab with additional support strips", 6, 5, comps)
    b1, _ = mesh_kg(6, 5, 0.012, 0.2)
    b2, _ = mesh_kg(5, 6, 0.012, 0.2)
    sup, nsup = mesh_kg(1.25, 5, 0.012, 0.15)
    out.append(Case("RSLAB-004", "Slab with additional support strips", "reinforcement", "synthetic",
        "Bottom mesh plus top support bars over the two supports, defined by explicit rectangular regions. " + PROV,
        ["S1: 6 × 5 m; bottom T12@200 both ways", "Top strips 1.25 m wide at x = 0 and x = 6, T12@150 running in X"], s.to_dict(), {},
        [Hand("elements.S1.steelComponentsKg.2", sup, f"strip: N = floor((5 − 0.1)/0.15) + 1 = {nsup}; length 1.25 − 0.05 = 1.20 m"),
         Hand("modelTotals.steelKg", b1 + b2 + 2 * sup, "bottom X + bottom Y + 2 strips")], expected_behaviour="include"))

    comps = [mesh("X", 0.016, 0.2, 0.05, 0.025, layer="bottom-layer-1"), mesh("X", 0.012, 0.2, 0.15, 0.025, layer="bottom-layer-2"),
             mesh("Y", 0.012, 0.2, 0.05, 0.025, layer="bottom")]
    s = slab_snap("RSLAB-005", "Slab with two bottom layers in X", 6, 5, comps, t=0.25)
    l1, _ = mesh_kg(6, 5, 0.016, 0.2)
    l2, n2 = mesh_kg(6, 5, 0.012, 0.2, edge=0.15)
    l3, _ = mesh_kg(5, 6, 0.012, 0.2)
    out.append(Case("RSLAB-005", "Slab with two bottom layers in X", "reinforcement", "synthetic",
        "Two reinforcement layers in the same direction (second layer staggered 100 mm, i.e. larger edge offset). " + PROV,
        ["S1: 6 × 5 × 0.25 m", "Layer 1: T16@200 X", "Layer 2: T12@200 X, first bar 150 mm from edge", "Y: T12@200"], s.to_dict(), {},
        [Hand("elements.S1.steelComponentsKg.1", l2, f"N = floor((5 − 0.3)/0.2) + 1 = {n2}"), Hand("modelTotals.steelKg", l1 + l2 + l3, "sum of layers")],
        expected_behaviour="include"))
    return out


def wall_snap(cid, name, L, H, t, comps, openings=None):
    s = Snap(cid, name, stories=[("L1", H, H)])
    s.concrete("C35", 35).rebar("B500", fy=500).wall_section("W", t)
    s.wall("W1", "L1", "W", "C35", [(0, 0, 0), (L, 0, 0), (L, 0, H), (0, 0, H)], openings=openings, rebar=provided(comps))
    return s


def walls():
    out = []
    comps = [mesh("Vertical", 0.012, 0.2, 0.05, 0.025, layer="vertical"), mesh("Horizontal", 0.010, 0.25, 0.05, 0.025, layer="horizontal")]
    s = wall_snap("RWALL-001", "Wall single-layer mesh", 5, 3, 0.2, comps)
    v, nv = mesh_kg(3, 5, 0.012, 0.2)
    h, nh = mesh_kg(5, 3, 0.010, 0.25)
    out.append(Case("RWALL-001", "Wall single-layer mesh", "reinforcement", "synthetic",
        "Central layer of vertical and horizontal bars. Vertical bars are distributed along the wall length; horizontal bars up the height.",
        ["W1: 5 × 3 m, t = 0.20", "Vertical T12@200, horizontal T10@250, one layer"], s.to_dict(), {},
        [Hand("elements.W1.steelComponentsKg.0", v, f"vertical: N = floor((5 − 0.1)/0.2) + 1 = {nv}; length 3 − 0.05 = 2.95 m"),
         Hand("elements.W1.steelComponentsKg.1", h, f"horizontal: N = floor((3 − 0.1)/0.25) + 1 = {nh}; length 4.95 m")], expected_behaviour="include"))

    comps = [mesh("Vertical", 0.012, 0.15, 0.05, 0.025, layer="vertical", faces=2), mesh("Horizontal", 0.010, 0.2, 0.05, 0.025, layer="horizontal", faces=2)]
    s = wall_snap("RWALL-002", "Wall two faces, different spacing", 5, 3, 0.25, comps)
    v, nv = mesh_kg(3, 5, 0.012, 0.15, faces=2)
    h, nh = mesh_kg(5, 3, 0.010, 0.2, faces=2)
    out.append(Case("RWALL-002", "Wall two faces, different spacing", "reinforcement", "synthetic", "Both faces reinforced; faces = 2 doubles each layer.",
        ["W1: 5 × 3 m, t = 0.25", "Vertical T12@150 each face; horizontal T10@200 each face"], s.to_dict(), {},
        [Hand("elements.W1.steelComponentsKg.0", v, f"2 faces × {nv} bars × 2.95 m"), Hand("elements.W1.steelComponentsKg.1", h, f"2 faces × {nh} bars × 4.95 m")],
        expected_behaviour="include"))

    web = [mesh("Vertical", 0.012, 0.2, 0.05, 0.025, layer="web-vertical", faces=2, region={"uMin": 0.6, "uMax": 5.4, "vMin": 0, "vMax": 3.5}),
           mesh("Horizontal", 0.010, 0.2, 0.05, 0.025, layer="web-horizontal", faces=2)]
    be = [bars(6, 0.020, length=3.5, role="boundary-left"), bars(6, 0.020, length=3.5, role="boundary-right"),
          hoops(0.010, 0.10, end_offset=0.05, zone_length=3.5, hook=0.2, cl_width=0.22, cl_height=0.50, role="boundary-left-hoops"),
          hoops(0.010, 0.10, end_offset=0.05, zone_length=3.5, hook=0.2, cl_width=0.22, cl_height=0.50, role="boundary-right-hoops")]
    s = wall_snap("RWALL-003", "Wall with boundary-zone reinforcement", 6, 3.5, 0.3, web + be)
    wv, nwv = mesh_kg(3.5, 4.8, 0.012, 0.2, faces=2)
    wh, _ = mesh_kg(6, 3.5, 0.010, 0.2, faces=2)
    bl = 6 * A(0.02) * 3.5 * RHO
    nb = 35   # by hand: 3.4/0.1 = 34 exactly -> 34 + 1
    bh = nb * (2 * (0.22 + 0.5) + 0.2) * A(0.01) * RHO
    out.append(Case("RWALL-003", "Wall with boundary-zone reinforcement", "reinforcement", "synthetic",
        "Special-boundary-element style wall: 0.6 m boundary zones at each end with 6T20 + closed hoops T10@100; web mesh confined to the region between them.",
        ["W1: 6.0 × 3.5 m, t = 0.30", "Boundary zones u 0–0.6 and 5.4–6.0: 6T20 × 3.5 m each, hoops T10@100 centre-line 0.22 × 0.50",
         "Web: vertical T12@200 both faces over u 0.6–5.4 (region); horizontal T10@200 both faces over full length"], s.to_dict(), {},
        [Hand("elements.W1.steelComponentsKg.0", wv, f"web vertical: 2 × {nwv} × 3.45 m"),
         Hand("elements.W1.steelComponentsKg.2", bl, "6 × A(20) × 3.5 × 7850"),
         Hand("elements.W1.steelComponentsKg.4", bh, "N = floor(3.4/0.1) + 1 = 35 (exact division); piece 2 × 0.72 + 0.2 = 1.64 m"),
         Hand("modelTotals.steelKg", wv + wh + 2 * bl + 2 * bh, "web + 2 × boundary")], expected_behaviour="include"))

    comps = [mesh("Vertical", 0.012, 0.2, 0.05, 0.025, faces=2), mesh("Horizontal", 0.012, 0.2, 0.05, 0.025, faces=2)]
    s = wall_snap("RWALL-004", "Wall mesh with door opening", 5, 3, 0.2, comps, openings=[("D1", [(1, 0, 0), (2, 0, 0), (2, 0, 2.1), (1, 0, 2.1)])])
    v, _ = mesh_kg(3, 5, 0.012, 0.2, faces=2)
    h, _ = mesh_kg(5, 3, 0.012, 0.2, faces=2)
    out.append(Case("RWALL-004", "Wall mesh with door opening", "reinforcement", "synthetic",
        "Documents the opening convention for mesh steel: bars are counted over the full host rectangle (not cut at openings). Concrete IS deducted.",
        ["W1: 5 × 3 m, t = 0.20, door 1.0 × 2.1 m", "Vertical and horizontal T12@200 both faces"], s.to_dict(), {},
        [Hand("modelTotals.steelKg", v + h, "same as an unpierced wall"), Hand("modelTotals.netConcreteM3", (15 - 2.1) * 0.2, "(15 − 2.1) × 0.2 = 2.58")],
        notes=["Trimming bars at openings is a legitimate alternative convention; if Quentra adopts it, this case must be versioned, not edited."],
        expected_behaviour="include; mesh not reduced by openings"))
    return out


def sources():
    out = []
    V = 0.3 * 0.6 * 6
    prov_kg = 4 * A(0.02) * 6 * RHO
    s = beam_snap("RSRC-001", "Source ProvidedBars", provided([bars(4, 0.020)], exact=True))
    out.append(Case("RSRC-001", "Source ProvidedBars", "source", "synthetic", "Bars as detailed → exact steel.",
        ["B1 0.3 × 0.6 × 6 with 4T20"], s.to_dict(), {},
        [Hand("elements.B1.steelExact", True, "ProvidedBars is exact"), Hand("modelTotals.steelExactKg", prov_kg, "4 × A(20) × 6 × 7850"),
         Hand("modelTotals.steelApproximateKg", 0.0, "")], expected_behaviour="exact"))

    s = beam_snap("RSRC-002", "Source EstimatedRatio", {"source": "EstimatedRatio", "steelMaterial": "B500", "exact": False, "components": [ratio(120)]})
    out.append(Case("RSRC-002", "Source EstimatedRatio", "source", "synthetic", "Steel estimated as 120 kg per m³ of net concrete. Must be flagged approximate.",
        ["B1 0.3 × 0.6 × 6 (1.08 m³)", "rate 120 kg/m³"], s.to_dict(), {},
        [Hand("modelTotals.steelKg", 120 * V, "120 × 1.08 = 129.6"), Hand("elements.B1.steelExact", False, ""),
         Hand("modelTotals.steelKgBySource.EstimatedRatio", 129.6, ""), Hand("modelTotals.steelExactKg", 0.0, "")],
        expected_behaviour="approximate, warn"))

    s = beam_snap("RSRC-003", "Source ManualOverride", {"source": "ManualOverride", "steelMaterial": "B500", "components": [fixed_mass(150.0)]})
    out.append(Case("RSRC-003", "Source ManualOverride", "source", "synthetic", "A user-entered steel mass. Treated as exact (user-asserted), reported under its own source.",
        ["B1 with manual override 150 kg"], s.to_dict(), {},
        [Hand("modelTotals.steelKgBySource.ManualOverride", 150.0, "as entered"), Hand("elements.B1.steelExact", True, "")], expected_behaviour="exact (user-asserted)"))

    s = beam_snap("RSRC-004", "Source Unavailable", {"source": "Unavailable", "components": []})
    out.append(Case("RSRC-004", "Source Unavailable", "source", "synthetic",
        "No reinforcement information. Steel is UNKNOWN: element steel is null, model steel completeness is Incomplete. It must not become 0 kg of steel.",
        ["B1 with source Unavailable"], s.to_dict(), {},
        [Hand("elements.B1.steelKg", None, "null, not 0"), Hand("completeness.steel", "Incomplete", ""), Hand("modelTotals.steelKg", 0.0, "sum of KNOWN steel only")],
        expected_behaviour="warn, steel unknown, block finalization"))

    s = Snap("RSRC-005", "Mixed reinforcement sources", stories=ONE)
    s.concrete("C30", 30).rebar("B500", fy=500).rect("B", 0.3, 0.6)
    s.beam("B1", "L1", "B", "C30", (0, 0, 3), (6, 0, 3), rebar=provided([bars(4, 0.020)]))
    s.beam("B2", "L1", "B", "C30", (0, 5, 3), (6, 5, 3), rebar={"source": "EstimatedRatio", "steelMaterial": "B500", "components": [ratio(120)]})
    s.beam("B3", "L1", "B", "C30", (0, 10, 3), (6, 10, 3), rebar={"source": "ManualOverride", "steelMaterial": "B500", "components": [fixed_mass(150.0)]})
    s.beam("B4", "L1", "B", "C30", (0, 15, 3), (6, 15, 3), rebar={"source": "RequiredDesignArea", "steelMaterial": "B500",
                                                                   "components": [required("B4", "MaxOfStations", include_shear=False)]})
    s.beam("B5", "L1", "B", "C30", (0, 20, 3), (6, 20, 3))
    s.design("B4", [(0, 1.0e-3, 5.0e-4, 0), (3, 5.0e-4, 1.0e-3, 0), (6, 1.0e-3, 5.0e-4, 0)])
    req = (1.0e-3 + 1.0e-3) * 6 * RHO
    out.append(Case("RSRC-005", "Mixed reinforcement sources", "source", "synthetic",
        "Five beams, one per source category. Totals by source must be kept apart; exact and approximate subtotals must add up to the known steel.",
        ["B1 ProvidedBars 4T20", "B2 EstimatedRatio 120 kg/m³", "B3 ManualOverride 150 kg", "B4 RequiredDesignArea (max-of-stations, flexure only)", "B5 no reinforcement"],
        s.to_dict(), {},
        [Hand("modelTotals.steelKgBySource.ProvidedBars", prov_kg, "4 × A(20) × 6 × 7850"),
         Hand("modelTotals.steelKgBySource.EstimatedRatio", 129.6, "120 × 1.08"),
         Hand("modelTotals.steelKgBySource.ManualOverride", 150.0, ""),
         Hand("modelTotals.steelKgBySource.RequiredDesignArea", req, "(max top 1000 mm² + max bottom 1000 mm²) × 6 m × 7850"),
         Hand("modelTotals.steelExactKg", prov_kg + 150, "ProvidedBars + ManualOverride"),
         Hand("modelTotals.steelApproximateKg", 129.6 + req, "EstimatedRatio + RequiredDesignArea"),
         Hand("completeness.steelUnknownElementIds", ["B5"], "B5 unknown")],
        expected_behaviour="separate by source; B5 unknown"))

    s = Snap("RSRC-006", "Conflicting exactness flags", stories=ONE)
    s.concrete("C30", 30).rebar("B500", fy=500).rect("B", 0.3, 0.6)
    s.beam("B1", "L1", "B", "C30", (0, 0, 3), (6, 0, 3), rebar={"source": "EstimatedRatio", "steelMaterial": "B500", "exact": True, "components": [ratio(120)]})
    s.beam("B2", "L1", "B", "C30", (0, 5, 3), (6, 5, 3), rebar=provided([bars(4, 0.020)], exact=False))
    out.append(Case("RSRC-006", "Conflicting exactness flags", "source", "synthetic",
        "Input claims contradict the source: an estimate marked exact, and drawn bars marked inexact. Quentra must derive exactness from the source category, not trust the flag.",
        ["B1: EstimatedRatio with exact = true (wrong)", "B2: ProvidedBars with exact = false (wrong)"], s.to_dict(), {},
        [Hand("elements.B1.steelExact", False, "EstimatedRatio is never exact"), Hand("elements.B2.steelExact", True, "ProvidedBars is exact")],
        expected_behaviour="derive exactness from source, warn"))

    s = beam_snap("RSRC-007", "Source/component mismatch", {"source": "ProvidedBars", "steelMaterial": "B500", "components": [ratio(120)]})
    out.append(Case("RSRC-007", "Source/component mismatch", "source", "synthetic",
        "Declared ProvidedBars but supplied an estimated ratio. Quentra must not quietly accept a ratio as provided bars.",
        ["B1: source ProvidedBars, component Ratio"], s.to_dict(), {},
        [Hand("elements.B1.steelKg", None, "rejected steel")], expected_behaviour="error, steel unknown, block finalization"))
    return out


def required_cases():
    out = []
    xs = [0, 1.5, 3.0, 4.5, 6.0]
    top = [1200e-6, 450e-6, 200e-6, 450e-6, 1200e-6]
    bot = [300e-6, 800e-6, 1100e-6, 800e-6, 300e-6]
    shr = [1.2e-3, 0.6e-3, 0.4e-3, 0.6e-3, 1.2e-3]
    stations = list(zip(xs, top, bot, shr))
    hl, legs = 1.76, 2

    def snap(cid, name, env, status="Current", unit="m"):
        k = {"m": 1.0, "mm": 1000.0}[unit]
        s = Snap(cid, name, unit=unit, stories=[("L1", 3 * k, 3 * k)])
        s.concrete("C30", 30).rebar("B500", fy=500).rect("B", 0.3 * k, 0.6 * k)
        s.beam("B1", "L1", "B", "C30", (0, 0, 3 * k), (6 * k, 0, 3 * k),
               rebar={"source": "RequiredDesignArea", "steelMaterial": "B500", "components": [required("B1", env, hoop_length=hl * k, legs=legs)]})
        s.design("B1", [(x * k, t * k * k, b * k * k, v * k) for x, t, b, v in stations], status=status)
        return s

    truth = ["B1: 0.3 × 0.6 × 6 m", "Simulated required areas at stations 0, 1.5, 3.0, 4.5, 6.0 m:",
             "  top (mm²): 1200, 450, 200, 450, 1200 — hogging at supports",
             "  bottom (mm²): 300, 800, 1100, 800, 300 — sagging mid-span",
             "  shear Av/s (mm²/m): 1200, 600, 400, 600, 1200",
             "Shear steel conversion: hoop length 1.76 m per 2 legs (see RSTIR-001)",
             "These are SIMULATED design results (origin = 'simulated'), not ETABS output."]
    V_max = (1200e-6 + 1100e-6) * 6 + 1.2e-3 * 6 * hl / legs
    V_trap = 3450e-6 + 4500e-6 + 4.2e-3 * hl / legs
    V_step = 4950e-6 + 5700e-6 + 5.4e-3 * hl / legs
    for cid, name, env, V, w in [
        ("RREQ-001", "Required area — max of stations", "MaxOfStations", V_max,
         "(1200 + 1100) mm² × 6 m + 1200 mm²/m × 6 m × 1.76/2 = 0.0138 + 0.006336 = 0.020136 m³"),
        ("RREQ-002", "Required area — trapezoidal integration", "TrapezoidalIntegration", V_trap,
         "top 1.5 × (825 + 325 + 325 + 825) = 3450 mm²·m; bottom 1.5 × (550 + 950 + 950 + 550) = 4500; shear 1.5 × (0.9 + 0.5 + 0.5 + 0.9) × 1e-3 = 4.2e-3 m² × 0.88"),
        ("RREQ-003", "Required area — segment step maximum", "SegmentStepMax", V_step,
         "top 1.5 × (1200 + 450 + 450 + 1200) = 4950; bottom 1.5 × (800 + 1100 + 1100 + 800) = 5700; shear 1.5 × (1.2 + 0.6 + 0.6 + 1.2)e-3 × 0.88")]:
        s = snap(cid, name, env)
        out.append(Case(cid, name, "required", "synthetic",
            f"Required design areas converted to steel with the **{env}** envelope. The envelope is named in the snapshot component; Quentra must use exactly that method.",
            truth, s.to_dict(), {},
            [Hand("modelTotals.steelKg", V * RHO, w + " → × 7850"), Hand("elements.B1.steelExact", False, "required ≠ provided"),
             Hand("modelTotals.steelKgBySource.RequiredDesignArea", V * RHO, "")],
            notes=["MaxOfStations ≥ SegmentStepMax ≥ TrapezoidalIntegration always holds for the same data; the three cases together pin the method."],
            expected_behaviour="approximate steel, info warning"))

    s = snap("RREQ-004", "Required area — stale design result", "MaxOfStations", status="Stale")
    out.append(Case("RREQ-004", "Required area — stale design result", "required", "synthetic",
        "As RREQ-001 but the design result is flagged Stale (model edited after design). Steel is still computed but finalization is blocked.",
        truth + ["Design result status: Stale"], s.to_dict(), {},
        [Hand("modelTotals.steelKg", V_max * RHO, "as RREQ-001"), Hand("finalization.allowed", False, "STALE_DESIGN_RESULT blocks")],
        expected_behaviour="compute, warn stale, block finalization"))

    s = Snap("RREQ-005", "Required area — missing/incomplete design result", stories=ONE)
    s.concrete("C30", 30).rebar("B500", fy=500).rect("B", 0.3, 0.6)
    s.beam("B1", "L1", "B", "C30", (0, 0, 3), (6, 0, 3), rebar={"source": "RequiredDesignArea", "steelMaterial": "B500", "components": [required("B1", "MaxOfStations", hoop_length=1.76, legs=2)]})
    s.beam("B2", "L1", "B", "C30", (0, 5, 3), (6, 5, 3), rebar={"source": "RequiredDesignArea", "steelMaterial": "B500", "components": [required("B2", "MaxOfStations", hoop_length=1.76, legs=2)]})
    s.design("B2", stations[:4])
    out.append(Case("RREQ-005", "Required area — missing/incomplete design result", "required", "synthetic",
        "B1 references a design result that does not exist; B2's stations stop at 4.5 m of a 6 m member. Neither may be extrapolated.",
        ["B1: no design result", "B2: stations 0–4.5 m only"], s.to_dict(), {},
        [Hand("elements.B1.steelKg", None, ""), Hand("elements.B2.steelKg", None, "")], expected_behaviour="error, steel unknown"))

    s = snap("RREQ-006", "Required area — max of stations (mm units)", "MaxOfStations", unit="mm")
    out.append(Case("RREQ-006", "Required area — max of stations (mm units)", "required", "synthetic",
        "RREQ-001 expressed in millimetres: areas in mm², shear in mm²/mm. Checks unit² and unit²/unit scaling of design results.",
        truth + ["Everything in mm: station 1500, area 1200 mm², Av/s 1.2 mm²/mm"], s.to_dict(), {},
        [Hand("modelTotals.steelKg", V_max * RHO, "same as RREQ-001")], equivalence_group="REQ-MAX",
        expected_behaviour="identical to RREQ-001"))
    return out


def plausibility():
    out = []
    s = Snap("PLAUS-001", "Transfer slab with overridden threshold", stories=ONE)
    s.concrete("C40", 40).slab_section("TS2500", 2.5)
    s.slab("TS1", "L1", "TS2500", "C40", [(0, 0, 3), (8, 0, 3), (8, 8, 3), (0, 8, 3)])
    out.append(Case("PLAUS-001", "Transfer slab with overridden threshold", "plausibility", "synthetic",
        "A genuine 2.5 m transfer slab. The project raises SLAB_THICKNESS_MAX to 3.0 m, so no warning is expected. Demonstrates that plausibility bands are configuration, not law.",
        ["TS1: 8 × 8 m, t = 2.5 m", "Run option: SLAB_THICKNESS_MAX threshold = 3.0 m"], s.to_dict(),
        {"quantifySteel": False, "plausibility": {"SLAB_THICKNESS_MAX": {"threshold": 3.0}}},
        [Hand("modelTotals.netConcreteM3", 160.0, "64 × 2.5")], expected_behaviour="include, no warning"))
    s2 = dict(s.to_dict()); s2["provenance"] = dict(s2["provenance"], caseId="PLAUS-002")
    out.append(Case("PLAUS-002", "Transfer slab with default threshold", "plausibility", "synthetic",
        "Same snapshot as PLAUS-001 with default rules: 2.5 m > 2.0 m → UNREALISTIC_DIMENSION (non-blocking). 2.5/1000 = 0.0025 m is not a typical thickness, so no unit warning.",
        ["TS1: 8 × 8 m, t = 2.5 m", "Default rules"], s2, {"quantifySteel": False},
        [Hand("modelTotals.netConcreteM3", 160.0, "64 × 2.5 — computed as given"), Hand("finalization.allowed", True, "warning does not block")],
        expected_behaviour="include, warn"))

    s = Snap("PLAUS-003", "High steel rate", stories=ONE)
    s.concrete("C50", 50).rebar("B500", fy=500).rect("C300", 0.3, 0.3)
    s.column("C1", "L1", "C300", "C50", (0, 0, 0), (0, 0, 3), rebar=provided([bars(12, 0.032)]))
    kg = 12 * A(0.032) * 3 * RHO
    out.append(Case("PLAUS-003", "High steel rate", "plausibility", "synthetic",
        "12T32 in a 300 × 300 column: ~841 kg/m³, above the 600 kg/m³ band but below steel density. Warn, keep the value.",
        ["C1: 0.3 × 0.3 × 3 m (0.27 m³), 12T32"], s.to_dict(), {},
        [Hand("modelTotals.steelKg", kg, "12 × A(32) × 3 × 7850 = 227.3 kg → 841.8 kg/m³")], expected_behaviour="include, warn"))

    s = Snap("PLAUS-004", "Impossible steel rate", stories=ONE)
    s.concrete("C30", 30).rebar("B500", fy=500).rect("B", 0.3, 0.6)
    s.beam("B1", "L1", "B", "C30", (0, 0, 3), (6, 0, 3), rebar={"source": "ManualOverride", "steelMaterial": "B500", "components": [fixed_mass(20000.0)]})
    out.append(Case("PLAUS-004", "Impossible steel rate", "plausibility", "synthetic",
        "Manual override of 20 000 kg in 1.08 m³ of concrete = 18 519 kg/m³ > 7850 kg/m³. Physically impossible: the value is discarded (steel unknown), finalization blocked.",
        ["B1: 1.08 m³, manual steel 20 000 kg"], s.to_dict(), {},
        [Hand("elements.B1.steelKg", None, "discarded"), Hand("modelTotals.steelKg", 0.0, "known steel only")], expected_behaviour="error, block finalization"))
    return out


def all_cases():
    return beams() + stirrups() + columns() + slabs() + walls() + sources() + required_cases() + plausibility()
