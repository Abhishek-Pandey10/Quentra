"""Large deterministic synthetic buildings A-E.

Every building is generated from a small parameter set so that its expected totals can be
re-derived in closed form (counts x unit quantities). The generator asserts that the
calculator's totals equal those closed-form totals before any fixture is written.
"""
import math
from .snapshot import Snap, provided, bars, hoops, crossties, mesh, required, ratio, P
from .case import Case, Hand

RHO = 7850.0


def A(d):
    return math.pi * d * d / 4


# ---------------- independent hand formulas (deliberately separate from evaluator.py) ----------------

def h_count(zone, off, s):
    # counts in these buildings are never exact divisions except where noted; round() guards the rest
    q = (zone - 2 * off) / s
    k = math.floor(q)
    if abs(q - round(q)) < 1e-9:
        k = round(q)
    return k + 1


def h_bars(n, d, L):
    return n * A(d) * L * RHO


def h_rect_hoops(b, h, cov, d, s, off, hook, L, sets=1):
    N = h_count(L, off, s)
    return N * sets * (2 * ((b - 2 * cov - d) + (h - 2 * cov - d)) + hook) * A(d) * RHO


def h_circ_hoops(D, cov, d, s, off, hook, L):
    N = h_count(L, off, s)
    return N * (math.pi * (D - 2 * cov - d) + hook) * A(d) * RHO


def h_mesh(along, across, d, s, edge, endc, faces=1):
    N = h_count(across, edge, s)
    return faces * N * (along - 2 * endc) * A(d) * RHO


def rect(x0, y0, x1, y1, z):
    return [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]


def vwall(x0, y0, x1, y1, z0, z1):
    return [(x0, y0, z0), (x1, y1, z0), (x1, y1, z1), (x0, y0, z1)]


# ---------------- common building blocks ----------------

class Tally:
    """Closed-form accumulator used for the hand check."""
    def __init__(self):
        self.story = {}
        self.types = {"Beam": 0.0, "Column": 0.0, "Slab": 0.0, "Wall": 0.0}
        self.steel = 0.0
        self.story_steel = {}

    def add(self, story, etype, vol, kg=0.0):
        self.story[story] = self.story.get(story, 0.0) + vol
        self.types[etype] += vol
        self.steel += kg
        self.story_steel[story] = self.story_steel.get(story, 0.0) + kg

    @property
    def total(self):
        return sum(self.story.values())


def frame_rebar_rect(b, h, nlong, dlong, dtie, s, cover, hook, off=0.05, steel="B500"):
    return provided([bars(nlong, dlong), hoops(dtie, s, cover=cover, end_offset=off, hook=hook)], steel=steel)


def beam_rebar(nb, nt, d, dtie, s, cover=0.025, hook=0.2, off=0.05, steel="B500"):
    return provided([bars(nb, d, role="bottom"), bars(nt, d, role="top"), hoops(dtie, s, cover=cover, end_offset=off, hook=hook)], steel=steel)


def building_a():
    cid = "BLDG-A"
    s = Snap(cid, "Building A - 1-story RC frame", stories=[("L1", 3.5, 3.5)],
             description="3 x 2 bays (6.0 m x 5.0 m), 12 columns, 17 beams, 1 slab with stair opening, 1 stair wall with door.")
    s.concrete("C30", 30).concrete("C40", 40).rebar("B500", fy=500)
    s.rect("C450", 0.45, 0.45).rect("B300x600", 0.3, 0.6).rect("B300x500", 0.3, 0.5).slab_section("S200", 0.2).wall_section("W200", 0.2)
    xs, ys, z = [0, 6, 12, 18], [0, 5, 10], 3.5
    T = Tally()
    k = 1
    for x in xs:
        for y in ys:
            s.column(f"C{k}", "L1", "C450", "C40", (x, y, 0), (x, y, z), rebar=frame_rebar_rect(0.45, 0.45, 8, 0.020, 0.010, 0.15, 0.04, 0.2))
            T.add("L1", "Column", 0.45 * 0.45 * 3.5, h_bars(8, 0.02, 3.5) + h_rect_hoops(0.45, 0.45, 0.04, 0.01, 0.15, 0.05, 0.2, 3.5))
            k += 1
    k = 1
    for y in ys:
        for i in range(3):
            s.beam(f"B{k}", "L1", "B300x600", "C30", (xs[i], y, z), (xs[i + 1], y, z), rebar=beam_rebar(3, 3, 0.020, 0.010, 0.15))
            T.add("L1", "Beam", 0.18 * 6, h_bars(6, 0.02, 6) + h_rect_hoops(0.3, 0.6, 0.025, 0.01, 0.15, 0.05, 0.2, 6))
            k += 1
    for x in xs:
        for j in range(2):
            s.beam(f"B{k}", "L1", "B300x500", "C30", (x, ys[j], z), (x, ys[j + 1], z), rebar=beam_rebar(3, 2, 0.016, 0.010, 0.15))
            T.add("L1", "Beam", 0.15 * 5, h_bars(5, 0.016, 5) + h_rect_hoops(0.3, 0.5, 0.025, 0.01, 0.15, 0.05, 0.2, 5))
            k += 1
    s.slab("S1", "L1", "S200", "C30", rect(0, 0, 18, 10, z), openings=[("STAIR", rect(8, 1, 10.5, 4, z))],
           rebar=provided([mesh("X", 0.012, 0.2, 0.05, 0.025), mesh("Y", 0.012, 0.2, 0.05, 0.025)]))
    T.add("L1", "Slab", (180 - 2.5 * 3) * 0.2, h_mesh(18, 10, 0.012, 0.2, 0.05, 0.025) + h_mesh(10, 18, 0.012, 0.2, 0.05, 0.025))
    s.wall("W1", "L1", "W200", "C30", vwall(7.5, 1, 7.5, 4, 0, 3.5), openings=[("D1", vwall(7.5, 1.5, 7.5, 2.5, 0, 2.1))],
           rebar=provided([mesh("Vertical", 0.010, 0.2, 0.05, 0.025, faces=2), mesh("Horizontal", 0.010, 0.2, 0.05, 0.025, faces=2)]))
    T.add("L1", "Wall", (3 * 3.5 - 1 * 2.1) * 0.2, h_mesh(3.5, 3, 0.01, 0.2, 0.05, 0.025, 2) + h_mesh(3, 3.5, 0.01, 0.2, 0.05, 0.025, 2))
    return Case(cid, "Building A — 1-story RC frame", "building", "buildings",
        "A complete single-story frame: every element carries provided bars. Totals are cross-checked against a closed-form tally (12 columns + 9 X-beams + 8 Y-beams + slab + wall).",
        ["Grid x = 0, 6, 12, 18 m; y = 0, 5, 10 m; story height 3.5 m",
         "Columns 12 × 0.45 × 0.45 (C40), 8T20 + T10@150 ties", "X-beams 9 × 0.3 × 0.6 × 6 m (C30), 3T20 + 3T20 + T10@150",
         "Y-beams 8 × 0.3 × 0.5 × 5 m (C30), 3T16 + 2T16 + T10@150", "Slab 18 × 10 × 0.2 with 2.5 × 3 m stair opening, T12@200 both ways",
         "Stair wall 3 × 3.5 × 0.2 with 1.0 × 2.1 m door, T10@200 both ways both faces"],
        s.to_dict(), {}, _tally_hand(T), tags=["building"], expected_behaviour="include all")


def _tally_hand(T, steel=True):
    h = [Hand("modelTotals.netConcreteM3", T.total, "closed-form Σ over element groups")]
    for st, v in T.story.items():
        h.append(Hand(f"byStory.{st}.netConcreteM3", v, "closed form"))
    for t, v in T.types.items():
        h.append(Hand(f"byElementType.{t}.netConcreteM3", v, "closed form"))
    if steel:
        h.append(Hand("modelTotals.steelKg", T.steel, "closed-form Σ of bar formulas"))
    return h


def building_b():
    cid = "BLDG-B"
    heights = [4.0, 3.5, 3.5, 3.5, 3.5]
    names = ["L1", "L2", "L3", "L4", "L5"]
    elev, stories = 0.0, []
    for nm, h in zip(names, heights):
        elev += h
        stories.append((nm, round(elev, 9), h))
    s = Snap(cid, "Building B - 5-story RC office", stories=stories,
             description="4 x 3 bays (7.5 m x 6.0 m), central core inside one bay, circular corner columns, column step at L3, required-area beams at L4-L5.")
    s.concrete("C30", 30).concrete("C35", 35).concrete("C40", 40).concrete("C45", 45).rebar("B500", fy=500)
    s.rect("C600", 0.6, 0.6).rect("C500", 0.5, 0.5).circle("CC600", 0.6).rect("PB350x700", 0.35, 0.7).rect("SB300x600", 0.3, 0.6)
    s.slab_section("S220", 0.22).wall_section("CW250", 0.25)
    xs, ys = [0, 7.5, 15, 22.5, 30], [0, 6, 12, 18]
    T = Tally()
    corners = {(0, 0), (30, 0), (0, 18), (30, 18)}
    core = (9.0, 7.5, 13.5, 10.5)
    for si, (st, top, h) in enumerate(stories):
        z0, z1 = top - h, top
        low = si < 2
        for x in xs:
            for y in ys:
                cid_ = f"C-{x:g}-{y:g}-{st}"
                if (x, y) in corners:
                    s.column(cid_, st, "CC600", "C45" if low else "C40", (x, y, z0), (x, y, z1), name=f"CC{xs.index(x)}{ys.index(y)}",
                             rebar=provided([bars(8, 0.020), hoops(0.010, 0.15, cover=0.04, end_offset=0.05, hook=0.2)]))
                    T.add(st, "Column", math.pi * 0.36 / 4 * h, h_bars(8, 0.02, h) + h_circ_hoops(0.6, 0.04, 0.01, 0.15, 0.05, 0.2, h))
                else:
                    b = 0.6 if low else 0.5
                    s.column(cid_, st, "C600" if low else "C500", "C45" if low else "C40", (x, y, z0), (x, y, z1), name=f"C{xs.index(x)}{ys.index(y)}",
                             rebar=frame_rebar_rect(b, b, 12 if low else 8, 0.025 if low else 0.020, 0.010, 0.15, 0.04, 0.2))
                    T.add(st, "Column", b * b * h, h_bars(12 if low else 8, 0.025 if low else 0.02, h) + h_rect_hoops(b, b, 0.04, 0.01, 0.15, 0.05, 0.2, h))
        req = si >= 3
        for y in ys:
            for i in range(4):
                eid = f"PB-{y:g}-{i}-{st}"
                if req:
                    s.beam(eid, st, "PB350x700", "C35", (xs[i], y, z1), (xs[i + 1], y, z1),
                           rebar={"source": "RequiredDesignArea", "steelMaterial": "B500", "components": [required(eid, "TrapezoidalIntegration", hoop_length=2.14, legs=2)]})
                    L = 7.5
                    st_ = [(0, 1.6e-3, 5e-4, 1.0e-3), (L / 4, 6e-4, 1.1e-3, 6e-4), (L / 2, 3e-4, 1.4e-3, 4e-4), (3 * L / 4, 6e-4, 1.1e-3, 6e-4), (L, 1.6e-3, 5e-4, 1.0e-3)]
                    s.design(eid, st_)
                    top = L / 4 * ((1.6e-3 + 6e-4) / 2 + (6e-4 + 3e-4) / 2) * 2
                    bot = L / 4 * ((5e-4 + 1.1e-3) / 2 + (1.1e-3 + 1.4e-3) / 2) * 2
                    shr = L / 4 * ((1.0e-3 + 6e-4) / 2 + (6e-4 + 4e-4) / 2) * 2
                    kg = (top + bot + shr * 2.14 / 2) * RHO
                else:
                    s.beam(eid, st, "PB350x700", "C35", (xs[i], y, z1), (xs[i + 1], y, z1), rebar=beam_rebar(4, 3, 0.020, 0.010, 0.15))
                    kg = h_bars(7, 0.02, 7.5) + h_rect_hoops(0.35, 0.7, 0.025, 0.01, 0.15, 0.05, 0.2, 7.5)
                T.add(st, "Beam", 0.35 * 0.7 * 7.5, kg)
        for x in xs:
            for j in range(3):
                s.beam(f"SB-{x:g}-{j}-{st}", st, "SB300x600", "C35", (x, ys[j], z1), (x, ys[j + 1], z1), rebar=beam_rebar(3, 2, 0.016, 0.010, 0.2))
                T.add(st, "Beam", 0.18 * 6, h_bars(5, 0.016, 6) + h_rect_hoops(0.3, 0.6, 0.025, 0.01, 0.2, 0.05, 0.2, 6))
        x0, y0, x1, y1 = core
        s.slab(f"SLAB-{st}", st, "S220", "C30", rect(0, 0, 30, 18, z1),
               openings=[("CORE", rect(x0, y0, x1, y1, z1)), ("STAIR", rect(16.5, 7, 19.5, 11, z1))],
               rebar={"source": "EstimatedRatio", "steelMaterial": "B500", "components": [ratio(90)]})
        vol = (540 - 4.5 * 3 - 3 * 4) * 0.22
        T.add(st, "Slab", vol, 90 * vol)
        wr = provided([mesh("Vertical", 0.012, 0.2, 0.05, 0.025, faces=2), mesh("Horizontal", 0.010, 0.2, 0.05, 0.025, faces=2)])
        for wid, pts, Lw, door in [("CW-S", vwall(x0, y0, x1, y0, z0, z1), 4.5, True), ("CW-E", vwall(x1, y0, x1, y1, z0, z1), 3.0, False),
                                    ("CW-N", vwall(x1, y1, x0, y1, z0, z1), 4.5, False), ("CW-W", vwall(x0, y1, x0, y0, z0, z1), 3.0, False)]:
            ops = [("DOOR", vwall(x0 + 1.5, y0, x0 + 2.5, y0, z0, z0 + 2.2))] if door else None
            s.wall(f"{wid}-{st}", st, "CW250", "C40", pts, openings=ops, rebar=wr, name=wid)
            T.add(st, "Wall", (Lw * h - (2.2 if door else 0)) * 0.25,
                  h_mesh(h, Lw, 0.012, 0.2, 0.05, 0.025, 2) + h_mesh(Lw, h, 0.010, 0.2, 0.05, 0.025, 2))
    return Case(cid, "Building B — 5-story RC office", "building", "buildings",
        "Five-story office with column size/grade step at L3, circular corner columns, a four-wall core with a door, slabs with core and stair openings, estimated slab steel and simulated required-area beams at L4–L5.",
        ["Grid 4 × 3 bays: x = 0…30 m @ 7.5, y = 0…18 m @ 6; L1 4.0 m, L2–L5 3.5 m",
         "Columns: L1–L2 0.6 × 0.6 C45 12T25; L3–L5 0.5 × 0.5 C40 8T20; corners circular D 0.6 8T20; all ties T10@150",
         "Primary X-beams 0.35 × 0.7 (C35): L1–L3 provided 4T20 + 3T20 + T10@150; L4–L5 simulated required areas (trapezoidal)",
         "Secondary Y-beams 0.3 × 0.6 (C35): 3T16 + 2T16 + T10@200", "Slabs 30 × 18 × 0.22 (C30), core 4.5 × 3 and stair 3 × 4 openings, estimated 90 kg/m³",
         "Core walls 0.25 (C40) on x 9–13.5, y 7.5–10.5 with a 1.0 × 2.2 m door in the south wall; T12/T10@200 both faces"],
        s.to_dict(), {}, _tally_hand(T), tags=["building"], expected_behaviour="include all; estimated + required steel flagged")


def building_c():
    cid = "BLDG-C"
    heights = [4.5] + [3.2] * 9
    names = [f"L{i}" for i in range(1, 11)]
    elev, stories = 0.0, []
    for nm, h in zip(names, heights):
        elev += h
        stories.append((nm, round(elev, 9), h))
    s = Snap(cid, "Building C - 10-story wall-frame", stories=stories,
             description="5 x 3 bays (8 m), two 6 x 6 m cores, wall thickness and grade step at L6, columns step at L4 and L7.")
    s.concrete("C35", 35).concrete("C40", 40).concrete("C50", 50).rebar("B500", fy=500).rebar("B500-LARGE", fy=500)
    for b in (0.8, 0.7, 0.6):
        s.rect(f"C{int(b * 1000)}", b, b)
    s.rect("B400x750", 0.4, 0.75).slab_section("S250", 0.25).wall_section("CW350", 0.35).wall_section("CW250", 0.25)
    xs, ys = [0, 8, 16, 24, 32, 40], [0, 8, 16, 24]
    cores = [(9, 9, 15, 15), (25, 9, 31, 15)]
    T = Tally()
    for si, (st, top, h) in enumerate(stories):
        z0, z1 = top - h, top
        b = 0.8 if si < 3 else (0.7 if si < 6 else 0.6)
        nlong, dl = (16, 0.032) if si < 3 else ((12, 0.028) if si < 6 else (12, 0.025))
        mat_v = "C50" if si < 5 else "C40"
        steel_mat = "B500-LARGE" if dl >= 0.028 else "B500"
        for x in xs:
            for y in ys:
                s.column(f"C{x:g}_{y:g}.{st}", st, f"C{int(b * 1000)}", mat_v, (x, y, z0), (x, y, z1), name=f"COL {xs.index(x) + 1}{'ABCD'[ys.index(y)]}",
                         rebar=provided([bars(nlong, dl), hoops(0.012, 0.15, cover=0.04, end_offset=0.05, hook=0.24),
                                         crossties(0.012, 0.15, b - 0.08 - 0.012 + 0.24, end_offset=0.05, per_location=2)], steel=steel_mat))
                N = h_count(h, 0.05, 0.15)
                T.add(st, "Column", b * b * h, h_bars(nlong, dl, h) + h_rect_hoops(b, b, 0.04, 0.012, 0.15, 0.05, 0.24, h) + N * 2 * (b - 0.092 + 0.24) * A(0.012) * RHO)
        for y in ys:
            for i in range(5):
                s.beam(f"BX{i}_{y:g}.{st}", st, "B400x750", "C35", (xs[i], y, z1), (xs[i + 1], y, z1), rebar=beam_rebar(4, 4, 0.025, 0.012, 0.15, hook=0.24))
                T.add(st, "Beam", 0.3 * 8, h_bars(8, 0.025, 8) + h_rect_hoops(0.4, 0.75, 0.025, 0.012, 0.15, 0.05, 0.24, 8))
        for x in xs:
            for j in range(3):
                s.beam(f"BY{j}_{x:g}.{st}", st, "B400x750", "C35", (x, ys[j], z1), (x, ys[j + 1], z1), rebar=beam_rebar(4, 4, 0.025, 0.012, 0.15, hook=0.24))
                T.add(st, "Beam", 0.3 * 8, h_bars(8, 0.025, 8) + h_rect_hoops(0.4, 0.75, 0.025, 0.012, 0.15, 0.05, 0.24, 8))
        s.slab(f"SL.{st}", st, "S250", "C35", rect(0, 0, 40, 24, z1),
               openings=[(f"CORE{k + 1}", rect(*c, z1)) for k, c in enumerate(cores)],
               rebar=provided([mesh("X", 0.012, 0.15, 0.05, 0.025, "bottom"), mesh("Y", 0.012, 0.15, 0.05, 0.025, "bottom"),
                               mesh("X", 0.012, 0.2, 0.05, 0.025, "top"), mesh("Y", 0.012, 0.2, 0.05, 0.025, "top")]))
        T.add(st, "Slab", (960 - 72) * 0.25,
              h_mesh(40, 24, 0.012, 0.15, 0.05, 0.025) + h_mesh(24, 40, 0.012, 0.15, 0.05, 0.025) + h_mesh(40, 24, 0.012, 0.2, 0.05, 0.025) + h_mesh(24, 40, 0.012, 0.2, 0.05, 0.025))
        t = 0.35 if si < 5 else 0.25
        for k, (x0, y0, x1, y1) in enumerate(cores):
            for side, pts in [("S", vwall(x0, y0, x1, y0, z0, z1)), ("E", vwall(x1, y0, x1, y1, z0, z1)),
                              ("N", vwall(x1, y1, x0, y1, z0, z1)), ("W", vwall(x0, y1, x0, y0, z0, z1))]:
                door = side == "S"
                ops = [("DOOR", vwall(x0 + 2.5, y0, x0 + 3.5, y0, z0, z0 + 2.4))] if door else None
                s.wall(f"CORE{k + 1}-{side}.{st}", st, "CW350" if t == 0.35 else "CW250", mat_v, pts, openings=ops, name=f"Pier C{k + 1}{side}",
                       rebar=provided([mesh("Vertical", 0.016 if t == 0.35 else 0.012, 0.2, 0.05, 0.025, faces=2),
                                       mesh("Horizontal", 0.012, 0.2, 0.05, 0.025, faces=2),
                                       bars(8, 0.025, length=h, role="boundary-both-ends")]))
                T.add(st, "Wall", (6 * h - (2.4 if door else 0)) * t,
                      h_mesh(h, 6, 0.016 if t == 0.35 else 0.012, 0.2, 0.05, 0.025, 2) + h_mesh(6, h, 0.012, 0.2, 0.05, 0.025, 2) + h_bars(8, 0.025, h))
    return Case(cid, "Building C — 10-story wall-frame", "building", "buildings",
        "Ten stories, 24 columns per floor with two section steps, two six-metre cores with doors, wall thickness and concrete grade step at L6, two rebar grades.",
        ["Grid 5 × 3 bays @ 8 m (40 × 24 m); L1 4.5 m, L2–L10 3.2 m",
         "Columns 0.8 (L1–L3, 16T32), 0.7 (L4–L6, 12T28), 0.6 (L7–L10, 12T25); C50 to L5, C40 above; T12@150 hoop + 2 crossties",
         "Beams 0.4 × 0.75 × 8 m both ways (C35), 4T25 + 4T25 + T12@150",
         "Slabs 40 × 24 × 0.25 (C35) with two 6 × 6 core openings; T12@150 bottom, T12@200 top, both ways",
         "Cores: walls 0.35 (L1–L5) / 0.25 (L6–L10), door 1.0 × 2.4 in each south wall; mesh both faces + 8T25 boundary bars"],
        s.to_dict(), {}, _tally_hand(T), tags=["building", "large"], expected_behaviour="include all")


def building_d():
    """Irregular: L-shaped plan with setback, sloped roof beams, inclined column, circular columns, messy names."""
    cid = "BLDG-D"
    stories = [("GF", 4.0, 4.0), ("1F", 7.5, 3.5), ("2F", 11.0, 3.5), ("RF", 14.0, 3.0)]
    s = Snap(cid, "Building D - irregular multi-story", stories=stories,
             description="L-shaped plan (GF-1F), setback rectangle above, sloped roof beams, one inclined column, circular columns, irregular slab reinforced by regions.")
    s.concrete("C30/37", 30).concrete("C40/50", 40).rebar("B500B", fy=500)
    s.rect("COL-500SQ", 0.5, 0.5).circle("COL-D500", 0.5).rect("BM 300X650", 0.3, 0.65).rect("BM-SLOPE 300X500", 0.3, 0.5)
    s.slab_section("SLAB 200", 0.2).slab_section("ROOF 180", 0.18).wall_section("RW 250", 0.25)
    T = Tally()
    # L plan: rectangle 0..16 x 0..8 plus 0..8 x 8..16
    Lplan_cols = [(0, 0), (8, 0), (16, 0), (0, 8), (8, 8), (16, 8), (0, 16), (8, 16)]
    upper_cols = [(0, 0), (8, 0), (0, 8), (8, 8)]
    n = 0
    for si, (st, top, h) in enumerate(stories[:3]):
        z0, z1 = top - h, top
        cols = Lplan_cols if si < 2 else upper_cols
        for (x, y) in cols:
            n += 1
            if (x, y) == (16, 8) and si == 1:
                # inclined column: base at (16,8,z0), top at (15,8,z1) — leaning 1.0 m over 3.5 m
                s.column(f"ICOL_{n:03d}", st, "COL-500SQ", "C40/50", (16, 8, z0), (15, 8, z1), name="COLUMN_INCLINED_1",
                         rebar=provided([bars(8, 0.020), hoops(0.010, 0.15, cover=0.04, end_offset=0.05, hook=0.2)], steel="B500B"))
                L = math.hypot(1.0, h)
                T.add(st, "Column", 0.25 * L, h_bars(8, 0.02, L) + h_rect_hoops(0.5, 0.5, 0.04, 0.01, 0.15, 0.05, 0.2, L))
                continue
            circ = x == 0
            s.column(f"COL_{n:03d}", st, "COL-D500" if circ else "COL-500SQ", "C40/50", (x, y, z0), (x, y, z1),
                     name=f"{'CIRC' if circ else 'COLUMN'}_{x:g}{y:g}",
                     rebar=provided([bars(8, 0.020), hoops(0.010, 0.15, cover=0.04, end_offset=0.05, hook=0.2)], steel="B500B"))
            vol = (math.pi * 0.25 / 4 if circ else 0.25) * h
            hk = h_circ_hoops(0.5, 0.04, 0.01, 0.15, 0.05, 0.2, h) if circ else h_rect_hoops(0.5, 0.5, 0.04, 0.01, 0.15, 0.05, 0.2, h)
            T.add(st, "Column", vol, h_bars(8, 0.02, h) + hk)
        # beams on L plan / upper rectangle
        if si < 2:
            lines = [((0, 0), (8, 0)), ((8, 0), (16, 0)), ((0, 8), (8, 8)), ((8, 8), (16, 8)), ((0, 16), (8, 16)),
                     ((0, 0), (0, 8)), ((0, 8), (0, 16)), ((8, 0), (8, 8)), ((8, 8), (8, 16)), ((16, 0), (16, 8))]
            if si == 1:
                # inclined column top is at (15,8): the two beams framing that node end at (15,8) instead
                lines = [((a, b) if b != (16, 8) else (a, (15, 8))) for a, b in lines]
                lines = [(((15, 8) if a == (16, 8) else a), b) for a, b in lines]
            slab_pts = [(0, 0, z1), (16, 0, z1), (16, 8, z1), (8, 8, z1), (8, 16, z1), (0, 16, z1)]
            regions = [{"xMin": 0, "xMax": 16, "yMin": 0, "yMax": 8}, {"xMin": 0, "xMax": 8, "yMin": 8, "yMax": 16}]
        else:
            lines = [((0, 0), (8, 0)), ((0, 8), (8, 8)), ((0, 0), (0, 8)), ((8, 0), (8, 8))]
            slab_pts = [(0, 0, z1), (8, 0, z1), (8, 8, z1), (0, 8, z1)]
            regions = [{"xMin": 0, "xMax": 8, "yMin": 0, "yMax": 8}]
        for k, (a, b) in enumerate(lines):
            s.beam(f"Beam_{st}_{k + 1:02d}", st, "BM 300X650", "C30/37", (a[0], a[1], z1), (b[0], b[1], z1), name=f"B-{k + 1:02d}",
                   rebar=beam_rebar(3, 3, 0.020, 0.010, 0.15, steel="B500B"))
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            T.add(st, "Beam", 0.3 * 0.65 * L, h_bars(6, 0.02, L) + h_rect_hoops(0.3, 0.65, 0.025, 0.01, 0.15, 0.05, 0.2, L))
        comps = []
        kg = 0.0
        for r in regions:
            comps += [mesh("X", 0.012, 0.2, 0.05, 0.025, region=r), mesh("Y", 0.012, 0.2, 0.05, 0.025, region=r)]
            wx, wy = r["xMax"] - r["xMin"], r["yMax"] - r["yMin"]
            kg += h_mesh(wx, wy, 0.012, 0.2, 0.05, 0.025) + h_mesh(wy, wx, 0.012, 0.2, 0.05, 0.025)
        area = 192.0 if si < 2 else 64.0
        s.slab(f"SLAB-{st}-A", st, "SLAB 200", "C30/37", slab_pts, name=f"SLAB-{st}-A", rebar=provided(comps, steel="B500B"))
        T.add(st, "Slab", area * 0.2, kg)
        # retaining wall along y=0 at GF only (offset 1 m outside the grid so no column sits in it)
        if si == 0:
            s.wall("RW-01", st, "RW 250", "C40/50", vwall(0, -1, 16, -1, z0, z1), name="Wall Pier 01",
                   openings=[("VENT", vwall(6, -1, 7.2, -1, 2.5, 3.1))],
                   rebar=provided([mesh("Vertical", 0.016, 0.15, 0.05, 0.025, faces=2), mesh("Horizontal", 0.012, 0.2, 0.05, 0.025, faces=2)], steel="B500B"))
            T.add(st, "Wall", (16 * 4 - 1.2 * 0.6) * 0.25, h_mesh(4, 16, 0.016, 0.15, 0.05, 0.025, 2) + h_mesh(16, 4, 0.012, 0.2, 0.05, 0.025, 2))
    # roof: sloped beams (mono-pitch rising 1.0 m over 8 m) and a sloped slab-free roof frame on the 8 x 8 setback
    st, top, h = stories[3]
    z0 = top - h
    for k, x in enumerate([0, 8]):
        s.column(f"COL_R{k}", st, "COL-500SQ", "C40/50", (x, 8, z0), (x, 8, top), name=f"COLUMN_ROOF_{k}",
                 rebar=provided([bars(4, 0.020), hoops(0.010, 0.15, cover=0.04, end_offset=0.05, hook=0.2)], steel="B500B"))
        T.add(st, "Column", 0.25 * h, h_bars(4, 0.02, h) + h_rect_hoops(0.5, 0.5, 0.04, 0.01, 0.15, 0.05, 0.2, h))
    for k, x in enumerate([0, 8]):
        s.beam(f"RAFTER_{k}", st, "BM-SLOPE 300X500", "C30/37", (x, 0, z0), (x, 8, top), name=f"Sloped Beam {k}",
               rebar=provided([bars(4, 0.016), hoops(0.010, 0.2, cover=0.025, end_offset=0.05, hook=0.2)], steel="B500B"))
        L = math.hypot(8, h)
        T.add(st, "Beam", 0.15 * L, h_bars(4, 0.016, L) + h_rect_hoops(0.3, 0.5, 0.025, 0.01, 0.2, 0.05, 0.2, L))
    return Case(cid, "Building D — irregular multi-story", "building", "buildings",
        "Irregular geometry: L-shaped lower floors, 8 × 8 setback above, circular columns on one grid line, an inclined column, a retaining wall outside the grid with a vent, sloped roof rafters. Messy names throughout; slab steel defined per rectangular region.",
        ["GF/1F: L-shape 16 × 8 + 8 × 8 (192 m²); 2F: 8 × 8 (64 m²)", "Circular D 0.5 columns on x = 0; square 0.5 elsewhere",
         "1F column at (16,8) inclined: base (16,8), top (15,8), rise 3.5 m → L = √(1² + 3.5²)", "Roof: two rafters 0.3 × 0.5 rising 3 m over 8 m → L = √(8² + 3²)",
         "Retaining wall 16 × 4 × 0.25 at y = −1 with 1.2 × 0.6 m vent"],
        s.to_dict(), {}, _tally_hand(T), tags=["building"], expected_behaviour="include all")


def building_e():
    """Building with intentional data problems layered onto a clean 2-story frame."""
    cid = "BLDG-E"
    stories = [("L1", 3.5, 3.5), ("L2", 7.0, 3.5)]
    s = Snap(cid, "Building E - model with intentional data problems", stories=stories,
             description="Clean 2-story 2 x 2 bay frame + injected defects. Expected results document how each defect is handled.")
    s.concrete("C30", 30).concrete("C40", 40).rebar("B500", fy=500).material("GRADE?", "Other")
    s.rect("C400", 0.4, 0.4).rect("B300x600", 0.3, 0.6).slab_section("S200", 0.2).slab_section("S300M", 300.0).wall_section("W200", 0.2)
    xs, ys = [0, 6, 12], [0, 6, 12]
    clean = Tally()
    for st, top, h in stories:
        z0, z1 = top - h, top
        for x in xs:
            for y in ys:
                s.column(f"C{x:g}{y:g}-{st}", st, "C400", "C40", (x, y, z0), (x, y, z1), rebar=frame_rebar_rect(0.4, 0.4, 4, 0.02, 0.008, 0.2, 0.04, 0.16))
                clean.add(st, "Column", 0.16 * h, h_bars(4, 0.02, h) + h_rect_hoops(0.4, 0.4, 0.04, 0.008, 0.2, 0.05, 0.16, h))
        for y in ys:
            for i in range(2):
                s.beam(f"BX{i}{y:g}-{st}", st, "B300x600", "C30", (xs[i], y, z1), (xs[i + 1], y, z1), rebar=beam_rebar(3, 2, 0.016, 0.01, 0.2))
                clean.add(st, "Beam", 1.08, h_bars(5, 0.016, 6) + h_rect_hoops(0.3, 0.6, 0.025, 0.01, 0.2, 0.05, 0.2, 6))
        for x in xs:
            for j in range(2):
                s.beam(f"BY{j}{x:g}-{st}", st, "B300x600", "C30", (x, ys[j], z1), (x, ys[j + 1], z1), rebar=beam_rebar(3, 2, 0.016, 0.01, 0.2))
                clean.add(st, "Beam", 1.08, h_bars(5, 0.016, 6) + h_rect_hoops(0.3, 0.6, 0.025, 0.01, 0.2, 0.05, 0.2, 6))
    # --- slabs with defects ---
    s.slab("S-L1", "L1", "S200", "C30", rect(0, 0, 12, 12, 3.5), openings=[("OP-EDGE", rect(11, 5, 13, 7, 3.5))],
           rebar={"source": "EstimatedRatio", "steelMaterial": "B500", "components": [ratio(85)]})       # opening half outside -> clipped
    s.slab("S-L2", "L2", "S300M", "C30", rect(0, 0, 12, 12, 7.0),
           rebar={"source": "EstimatedRatio", "steelMaterial": "B500", "components": [ratio(85)]})       # 300 m thick -> unit plausibility
    # --- walls without reinforcement (missing) ---
    s.wall("W-L1", "L1", "W200", "C30", vwall(2, 13, 10, 13, 0, 3.5))
    # --- analytical children of two beams ---
    s.beam("BX00-L1~seg1", "L1", "B300x600", "C30", (0, 0, 3.5), (3, 0, 3.5), role="AnalyticalSegment", parent="BX00-L1")
    s.beam("BX00-L1~seg2", "L1", "B300x600", "C30", (3, 0, 3.5), (6, 0, 3.5), role="AnalyticalSegment", parent="BX00-L1")
    # --- orphan analytical ---
    s.beam("BX99-L2~seg1", "L2", "B300x600", "C30", (0, 3, 7), (3, 3, 7), role="AnalyticalSegment", parent="BX99-L2")
    # --- identical duplicate of a column ---
    s.raw_element(dict(next(e for e in s.d["elements"] if e["id"] == "C00-L1")))
    # --- conflicting duplicate id ---
    s.beam("DUPE-1", "L2", "B300x600", "C30", (0, 3, 7), (6, 3, 7))
    s.beam("DUPE-1", "L2", "B300x600", "C30", (0, 9, 7), (6, 9, 7))
    # --- duplicate geometry: same as BY0 at x=12, L2 ---
    s.beam("B-GHOST", "L2", "B300x600", "C30", (12, 6, 7), (12, 0, 7), rebar=beam_rebar(3, 2, 0.016, 0.01, 0.2))
    # --- unknown/ambiguous materials ---
    s.beam("B-UNK", "L1", "B300x600", "C35-UNDEFINED", (0, 3, 3.5), (6, 3, 3.5))
    s.beam("B-AMB", "L1", "B300x600", "GRADE?", (6, 3, 3.5), (12, 3, 3.5))
    # --- story missing ---
    e = s.beam("B-NOSTORY", "L1", "B300x600", "C30", (0, 9, 3.5), (6, 9, 3.5), rebar=beam_rebar(3, 2, 0.016, 0.01, 0.2))
    e["story"] = None
    # --- stale design result ---
    s.beam("B-STALE", "L2", "B300x600", "C30", (6, 9, 7), (12, 9, 7),
           rebar={"source": "RequiredDesignArea", "steelMaterial": "B500", "components": [required("B-STALE", "MaxOfStations", hoop_length=1.76, legs=2)]})
    s.design("B-STALE", [(0, 9e-4, 3e-4, 8e-4), (3, 3e-4, 8e-4, 4e-4), (6, 9e-4, 3e-4, 8e-4)], status="Stale")
    # --- unsupported element type ---
    s.d["elements"].append({"id": "LINK-1", "name": "Isolator 1", "type": "Link", "role": "Physical", "parentId": None, "story": "L1",
                            "material": None, "section": None, "geometry": {"kind": "Line", "start": P(0, 0, 0), "end": P(0, 0, 0.3)},
                            "source": {"application": "quentra_bench", "version": "1.0.0", "objectName": "Isolator 1", "objectGuid": None, "apiMethod": None}})
    # hand: clean frame + each defect's documented effect
    clean_total = clean.total
    extra_concrete = (144 - 1 * 2) * 0.2 + 144 * 300.0 + 8 * 3.5 * 0.2 + 1.08 + 1.08 + 1.08   # S-L1 clipped, S-L2 as given, wall, B-GHOST, B-NOSTORY, B-STALE
    steel_extra = 85 * (142 * 0.2) + h_bars(5, 0.016, 6) + h_rect_hoops(0.3, 0.6, 0.025, 0.01, 0.2, 0.05, 0.2, 6) * 2 + h_bars(5, 0.016, 6) \
        + ((9e-4 + 8e-4) * 6 + 8e-4 * 6 * 1.76 / 2) * RHO   # max top + max bottom + max shear
    # S-L2: 85 kg/m3 x 43 200 m3 = 3.67 Mt -> rate 85 < 600, kept (the unit error is flagged separately)
    steel_extra += 85 * 144 * 300.0
    return Case(cid, "Building E — model with intentional data problems", "building", "buildings",
        "A clean 2-story frame with fourteen injected defects. Each defect has a documented, deterministic outcome; finalization is blocked. Totals = clean frame + the defects that are counted by rule.",
        ["Clean frame: 2 stories × (9 columns 0.4 × 0.4 C40 + 12 beams 0.3 × 0.6 × 6 C30), all provided bars",
         "Defects: slab opening half outside (clipped); L2 slab 300 m thick (counted as given + UNIT_PLAUSIBILITY); wall without reinforcement; two analytical children (excluded); orphan analytical (rejected); identical duplicate column (excluded); conflicting duplicate ID ×2 (rejected); duplicate geometry B-GHOST (counted + warning); unknown material (unclassified); ambiguous material (unclassified); missing story (<unassigned>); stale design result (counted + blocked); unsupported Link (skipped)"],
        s.to_dict(), {},
        [Hand("modelTotals.netConcreteM3", clean_total + extra_concrete, "clean frame + S-L1 (142 × 0.2) + S-L2 (144 × 300) + wall (8 × 3.5 × 0.2) + B-GHOST + B-NOSTORY + B-STALE"),
         Hand("modelTotals.unclassifiedVolumeM3", 2 * 1.08, "B-UNK + B-AMB"),
         Hand("byStory.<unassigned>.netConcreteM3", 1.08, "B-NOSTORY"),
         Hand("modelTotals.steelKg", clean.steel + steel_extra, "clean bars + slab estimates + B-GHOST + B-NOSTORY + B-STALE required"),
         Hand("finalization.allowed", False, "multiple blocking codes")],
        notes=["The 300 m slab produces an absurd but *as-given* volume; the whole point is that Quentra flags it instead of silently rescaling or silently accepting it."],
        tags=["building", "defects"], expected_behaviour="per-defect rules; block finalization")


def all_cases():
    return [building_a(), building_b(), building_c(), building_d(), building_e()]
