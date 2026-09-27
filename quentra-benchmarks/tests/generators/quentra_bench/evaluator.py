"""Independent first-principles evaluator for Quentra benchmark snapshots.

This is the *expected-value* side of the suite. It never looks at a Quentra result.
Every quantity is derived from the structural definition in the snapshot using the
conventions in docs/CONVENTIONS.md, and every step is written to a trace that becomes
the case README ("no magic numbers").

Polygon booleans use shapely (GEOS) deliberately: Quentra uses Clipper2,
so agreement between the independent implementations is meaningful.
"""
import math
from shapely.geometry import Polygon, Point
from shapely.ops import unary_union

from . import catalog
from .units import LENGTH_TO_M, DENSITY_TO_KG_PER_M3, MASS_TO_KG

SUPPORTED_SCHEMA = {"1.0"}
PROVENANCE_TYPES = {"synthetic_engineering_benchmark", "captured_etabs_snapshot"}
SUPPORTED_TYPES = {"Beam", "Column", "Slab", "Wall"}
FRAME_TYPES = {"Beam", "Column"}
AREA_TYPES = {"Slab", "Wall"}
CHILD_ROLES = {"AnalyticalSegment", "MeshElement"}
SOURCES = ["ProvidedBars", "RequiredDesignArea", "EstimatedRatio", "ManualOverride"]
ALLOWED_COMPONENTS = {
    "ProvidedBars": {"StraightBars", "Hoops", "Crossties", "Mesh"},
    "RequiredDesignArea": {"RequiredArea"},
    "EstimatedRatio": {"Ratio"},
    "ManualOverride": {"FixedMass"},
    "Unavailable": set(),
}
EXACT_SOURCES = {"ProvidedBars", "ManualOverride"}
UNASSIGNED = "<unassigned>"
EPS_COUNT = 1e-9          # added before floor() in bar counts
LEN_TOL_M = 1e-9          # zero-length tolerance
PLANE_TOL_M = 1e-6        # planarity tolerance
AREA_TOL_M2 = 1e-9        # area comparisons
STATION_TOL_M = 1e-6

DEFAULT_OPTIONS = {
    "quantifySteel": True,
    "materialAliases": {},
    "plausibility": {},
    "defaultSteelDensityKgPerM3": 7850.0,
}

TOLERANCE = {"volumeM3": 1e-6, "areaM2": 1e-6, "lengthM": 1e-6, "steelKg": 1e-3, "relative": 1e-9}


def n(x, digits=6):
    """Readable number for READMEs."""
    if x is None:
        return "unknown"
    if isinstance(x, int):
        return str(x)
    if x == 0:
        return "0"
    ax = abs(x)
    if ax >= 1e6 or ax < 1e-4:
        return f"{x:.6e}"
    s = f"{x:.{digits}f}".rstrip("0").rstrip(".")
    return s


class StepError(Exception):
    def __init__(self, code, reason):
        super().__init__(reason)
        self.code = code
        self.reason = reason


# ---------------------------------------------------------------- geometry helpers

def vsub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def vdot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def vcross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def vlen(a): return math.sqrt(vdot(a, a))
def vscale(a, s): return (a[0] * s, a[1] * s, a[2] * s)


def pt(p, f):
    return (p["x"] * f, p["y"] * f, p["z"] * f)


def clean_ring(pts):
    if len(pts) >= 2 and pts[0] == pts[-1]:
        pts = pts[:-1]
    return pts


def plane_frame(pts):
    """Newell normal, origin p0, u along first edge, v = n x u oriented upward."""
    nx = ny = nz = 0.0
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        nx += (a[1] - b[1]) * (a[2] + b[2])
        ny += (a[2] - b[2]) * (a[0] + b[0])
        nz += (a[0] - b[0]) * (a[1] + b[1])
    nrm = (nx, ny, nz)
    L = vlen(nrm)
    if L <= 1e-15:
        return None
    nh = vscale(nrm, 1.0 / L)
    e = vsub(pts[1], pts[0])
    le = vlen(e)
    if le <= LEN_TOL_M:
        return None
    u = vscale(e, 1.0 / le)
    v = vcross(nh, u)
    if v[2] < -1e-12 or (abs(v[2]) <= 1e-12 and v[1] < 0):
        v = vscale(v, -1.0)
    return pts[0], u, v, nh


def project(pts, frame):
    o, u, v, _ = frame
    return [(vdot(vsub(p, o), u), vdot(vsub(p, o), v)) for p in pts]


# ---------------------------------------------------------------- evaluator

class Evaluator:
    def __init__(self, snap, options=None):
        self.s = snap
        self.o = dict(DEFAULT_OPTIONS)
        self.o.update(options or {})
        self.warnings = []   # (code, subject)
        self.trace = []      # model-level lines
        self.el_trace = {}   # key -> lines
        self.rules = {r["id"]: dict(r) for r in catalog.PLAUSIBILITY_RULES}
        for rid, ov in (self.o.get("plausibility") or {}).items():
            self.rules[rid].update(ov)

    def warn(self, code, subject=None):
        key = (code, subject)
        if key not in self.warnings:
            self.warnings.append(key)

    def rule_on(self, rid):
        return self.rules[rid]["enabled"]

    def thr(self, rid):
        return self.rules[rid]["threshold"]

    # ------------------------------------------------------------ main
    def run(self):
        s = self.s
        # ---- snapshot-level fatal checks
        fatal = False
        if s.get("schemaVersion") not in SUPPORTED_SCHEMA:
            self.warn("UNSUPPORTED_SCHEMA_VERSION"); fatal = True
        prov = s.get("provenance")
        if not isinstance(prov, dict) or prov.get("type") not in PROVENANCE_TYPES:
            self.warn("PROVENANCE_MISSING"); fatal = True
        else:
            if prov["type"] == "synthetic_engineering_benchmark" and prov.get("etabs_verified") is not False:
                self.warn("PROVENANCE_INCONSISTENT"); fatal = True
            if prov["type"] == "captured_etabs_snapshot" and (prov.get("etabs_verified") is not True or not prov.get("etabs_version")):
                self.warn("PROVENANCE_INCONSISTENT"); fatal = True
            expected_origin = "simulated" if prov["type"] == "synthetic_engineering_benchmark" else "captured"
            if any(d.get("origin") != expected_origin for d in s.get("designResults", [])):
                self.warn("PROVENANCE_INCONSISTENT"); fatal = True
        unit = ((s.get("model") or {}).get("units") or {}).get("length")
        if unit not in LENGTH_TO_M:
            self.warn("UNSUPPORTED_UNITS"); fatal = True
        if fatal:
            self.trace.append("Snapshot rejected by fatal checks - no quantities are produced.")
            return self._rejected()

        self.f = LENGTH_TO_M[unit]
        self.unit = unit
        self.trace.append(f"Model length unit: {unit} (1 {unit} = {n(self.f, 6)} m).")
        self.stories = [st["name"] for st in s["model"]["stories"]]
        self.materials = {m["name"]: m for m in s.get("materials", [])}
        self.sections = {x["name"]: x for x in s.get("sections", [])}
        self.design = {}
        self.ambiguous_design = set()
        for d in s.get("designResults", []):
            if d["elementId"] in self.design:
                self.ambiguous_design.add(d["elementId"])
            self.design.setdefault(d["elementId"], d)

        for w in s.get("warnings", []) or []:
            self.warn("SOURCE_WARNING", w.get("code"))

        # ---- rebar materials
        self.rebar_density = {}  # name -> kg/m3 or None(unusable)
        for m in s.get("materials", []):
            if m.get("type") != "Rebar":
                continue
            dens, du = m.get("density"), m.get("densityUnit", "kg/m3")
            if dens is None or du not in DENSITY_TO_KG_PER_M3 or dens <= 0:
                self.warn("INVALID_MATERIAL_PROPERTY", m["name"])
                self.rebar_density[m["name"]] = None
                self.trace.append(f"Rebar material {m['name']}: density {dens} {du} is invalid -> steel using it is unknown.")
                continue
            kg = dens * DENSITY_TO_KG_PER_M3[du]
            bad = (self.rule_on("STEEL_DENSITY_MIN") and kg < self.thr("STEEL_DENSITY_MIN")) or \
                  (self.rule_on("STEEL_DENSITY_MAX") and kg > self.thr("STEEL_DENSITY_MAX"))
            if bad:
                self.warn("UNREALISTIC_MATERIAL_DENSITY", m["name"])
                self.rebar_density[m["name"]] = None
                self.trace.append(f"Rebar material {m['name']}: density {n(kg)} kg/m3 is outside the plausible band -> steel using it is unknown.")
            else:
                self.rebar_density[m["name"]] = kg
                conv = "" if du == "kg/m3" else f" ({n(dens, 9)} {du} x {n(DENSITY_TO_KG_PER_M3[du], 9)})"
                self.trace.append(f"Rebar material {m['name']}: density = {n(kg, 6)} kg/m3{conv}.")

        elements = s.get("elements", [])
        if not elements:
            self.warn("EMPTY_MODEL")

        # ---- duplicate ids
        by_id = {}
        for i, e in enumerate(elements):
            by_id.setdefault(e.get("id"), []).append(i)
        dup_status = {}
        for eid, idxs in by_id.items():
            if len(idxs) < 2:
                continue
            first = elements[idxs[0]]
            if all(elements[i] == first for i in idxs[1:]):
                for i in idxs[1:]:
                    dup_status[i] = "identical"
                self.warn("DUPLICATE_ELEMENT", eid)
            else:
                for i in idxs:
                    dup_status[i] = "conflict"
                self.warn("DUPLICATE_ELEMENT_ID", eid)
        physical_ids = {e.get("id") for e in elements if (e.get("role") or "Physical") == "Physical"}

        self.recs = []
        occ = {}
        for i, e in enumerate(elements):
            eid = e.get("id")
            o = occ.get(eid, 0)
            occ[eid] = o + 1
            rec = self._element(i, e, o, dup_status.get(i), physical_ids)
            self.recs.append(rec)

        self._model_checks()
        return self._assemble()

    # ------------------------------------------------------------ element pipeline
    def _element(self, idx, e, occurrence, dup, physical_ids):
        eid = e.get("id")
        etype = e.get("type")
        key = f"{eid}#{occurrence}"
        tr = []
        self.el_trace[key] = tr
        rec = {"id": eid, "occurrence": occurrence, "type": etype, "story": e.get("story"),
               "materialGroup": None, "status": None,
               "grossConcreteM3": None, "netConcreteM3": None,
               "steelKg": None, "reinforcementSource": None, "steelExact": None, "steelComponentsKg": [],
               "_e": e}
        if etype in FRAME_TYPES:
            rec["lengthM"] = None
        if etype in AREA_TYPES:
            rec.update({"grossAreaM2": None, "openingAreaSumM2": None, "openingDeductedAreaM2": None, "netAreaM2": None})

        def done(status, why):
            rec["status"] = status
            tr.append(f"Status: {status} - {why}")
            return rec

        # 1. duplicates
        if dup == "identical":
            return done("Excluded", "identical copy of an earlier element with the same ID (DUPLICATE_ELEMENT); only the first copy is counted.")
        if dup == "conflict":
            return done("Rejected", "another element shares this ID with different content (DUPLICATE_ELEMENT_ID); the true object is unknown.")
        # 2. role
        role = e.get("role") or "Physical"
        if role in CHILD_ROLES:
            if e.get("parentId") in physical_ids:
                self.warn("ANALYTICAL_CHILD_EXCLUDED", eid)
                return done("Excluded", f"{role} of physical element {e.get('parentId')}; the parent carries the quantity.")
            self.warn("ORPHAN_ANALYTICAL_ELEMENT", eid)
            return done("Rejected", f"{role} whose parent '{e.get('parentId')}' does not exist.")
        # 3. type
        if etype not in SUPPORTED_TYPES:
            self.warn("UNSUPPORTED_ELEMENT_TYPE", eid)
            return done("Skipped", f"element type '{etype}' is outside the quantity scope.")
        # 4. story
        story = e.get("story")
        if not story:
            self.warn("STORY_REFERENCE_MISSING", eid)
            rec["story"] = UNASSIGNED
            tr.append("Story: missing -> reported under '<unassigned>' (STORY_REFERENCE_MISSING).")
        elif story not in self.stories:
            self.warn("STORY_NOT_FOUND", eid)
            rec["story"] = UNASSIGNED
            tr.append(f"Story: '{story}' is not a declared story -> reported under '<unassigned>' (STORY_NOT_FOUND).")
        # 5. section
        sec = self.sections.get(e.get("section"))
        if sec is None:
            self.warn("SECTION_NOT_FOUND", eid)
            return done("Unquantified", f"section '{e.get('section')}' is not defined; volume is unknown (not zero).")
        f = self.f
        if etype in FRAME_TYPES:
            if sec.get("kind") != "Frame" or sec.get("shape") not in ("Rectangle", "Circle"):
                self.warn("UNSUPPORTED_SECTION", eid)
                return done("Unquantified", f"section '{sec['name']}' (kind={sec.get('kind')}, shape={sec.get('shape')}) is not supported for {etype} take-off; volume is unknown.")
            dims = [sec.get("width"), sec.get("depth")] if sec["shape"] == "Rectangle" else [sec.get("diameter")]
        else:
            if sec.get("kind") != etype:
                self.warn("UNSUPPORTED_SECTION", eid)
                return done("Unquantified", f"section '{sec['name']}' of kind {sec.get('kind')} cannot be used by a {etype}; volume is unknown.")
            dims = [sec.get("thickness")]
        if any(d is None or d <= 0 for d in dims):
            self.warn("INVALID_SECTION", eid)
            return done("Rejected", f"section '{sec['name']}' has a zero/negative/missing dimension {dims}.")
        # 6. geometry
        g = e.get("geometry") or {}
        raw_pts = []
        if etype in FRAME_TYPES:
            raw_pts = [g.get("start"), g.get("end")]
        else:
            raw_pts = list(g.get("points") or [])
            for op in g.get("openings") or []:
                raw_pts += op.get("points") or []
        cmax = self.thr("COORDINATE_MAX") if self.rule_on("COORDINATE_MAX") else math.inf
        for p in raw_pts:
            if p is None:
                self.warn("INVALID_GEOMETRY", eid)
                return done("Rejected", "missing coordinates.")
            for c in (p["x"], p["y"], p["z"]):
                if not math.isfinite(c) or abs(c * f) > cmax:
                    self.warn("COORDINATE_OUT_OF_RANGE", eid)
                    return done("Rejected", f"coordinate {c} {self.unit} is non-finite or beyond {n(cmax)} m.")

        mat_name = e.get("material")
        if etype in FRAME_TYPES:
            a, b = pt(g["start"], f), pt(g["end"], f)
            L = vlen(vsub(b, a))
            if L <= LEN_TOL_M:
                self.warn("INVALID_GEOMETRY", eid)
                return done("Rejected", "frame start and end coincide (zero length).")
            rec["lengthM"] = L
            dx, dy, dz = vsub(b, a)
            tr.append(f"Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt({n(dx)}^2 + {n(dy)}^2 + {n(dz)}^2) = {n(L)} m")
            if sec["shape"] == "Rectangle":
                bw, h = sec["width"] * f, sec["depth"] * f
                A = bw * h
                tr.append(f"Section {sec['name']}: rectangle b x h = {n(bw)} m x {n(h)} m, A = {n(A)} m2")
            else:
                D = sec["diameter"] * f
                A = math.pi * D * D / 4
                tr.append(f"Section {sec['name']}: circle D = {n(D)} m, A = pi x D^2 / 4 = {n(A)} m2")
            V = A * L
            tr.append(f"Gross volume = A x L = {n(A)} x {n(L)} = {n(V)} m3 (no openings: net = gross)")
            rec["grossConcreteM3"] = V
            rec["netConcreteM3"] = V
            self._frame_plausibility(eid, etype, sec, L, tr)
            self._geom = {"L": L, "sec": sec, "a": a, "b": b}
        else:
            pts = clean_ring([pt(p, f) for p in g.get("points") or []])
            if len(pts) < 3:
                self.warn("INVALID_GEOMETRY", eid)
                return done("Rejected", f"polygon has {len(pts)} distinct points (< 3).")
            fr = plane_frame(pts)
            if fr is None:
                self.warn("INVALID_GEOMETRY", eid)
                return done("Rejected", "polygon is degenerate (collinear or zero first edge).")
            o, u, v, nh = fr
            if any(abs(vdot(vsub(p, o), nh)) > PLANE_TOL_M for p in pts):
                self.warn("INVALID_GEOMETRY", eid)
                return done("Rejected", "polygon points are not coplanar.")
            loc = project(pts, fr)
            poly = Polygon(loc)
            if not poly.is_valid or poly.area <= AREA_TOL_M2:
                self.warn("INVALID_GEOMETRY", eid)
                return done("Rejected", "polygon is self-intersecting or has zero area.")
            t = sec["thickness"] * f
            Ag = poly.area
            tr.append(f"Host polygon ({len(pts)} vertices) area in its own plane = {n(Ag)} m2 (shoelace on local u-v coordinates)")
            ops = []
            osum = 0.0
            for op in g.get("openings") or []:
                opts = clean_ring([pt(p, f) for p in op["points"]])
                opoly = Polygon(project(opts, fr))
                inside = opoly.intersection(poly).area
                osum += opoly.area
                ops.append(opoly)
                if inside <= AREA_TOL_M2:
                    self.warn("OPENING_OUTSIDE_HOST", f"{eid}/{op['id']}")
                    tr.append(f"Opening {op['id']}: area {n(opoly.area)} m2, lies completely outside the host -> deducts 0 (OPENING_OUTSIDE_HOST)")
                elif inside < opoly.area - AREA_TOL_M2:
                    self.warn("OPENING_CLIPPED", f"{eid}/{op['id']}")
                    tr.append(f"Opening {op['id']}: area {n(opoly.area)} m2, only {n(inside)} m2 inside the host -> clipped (OPENING_CLIPPED)")
                else:
                    tr.append(f"Opening {op['id']}: area {n(opoly.area)} m2, fully inside the host")
            overlap = False
            for i in range(len(ops)):
                for j in range(i + 1, len(ops)):
                    if ops[i].intersection(ops[j]).area > AREA_TOL_M2:
                        overlap = True
            if overlap:
                self.warn("OPENING_OVERLAP", eid)
            ded = unary_union(ops).intersection(poly).area if ops else 0.0
            if ops:
                tr.append(f"Sum of opening areas = {n(osum)} m2; area of (union of openings) inside host = {n(ded)} m2" + (" - openings overlap, the union is deducted" if overlap else ""))
            An = Ag - ded
            rec.update({"grossAreaM2": Ag, "openingAreaSumM2": osum, "openingDeductedAreaM2": ded, "netAreaM2": An})
            rec["grossConcreteM3"] = Ag * t
            rec["netConcreteM3"] = An * t
            tr.append(f"Thickness t = {n(t)} m ({sec['name']})")
            tr.append(f"Gross volume = {n(Ag)} x {n(t)} = {n(Ag * t)} m3")
            tr.append(f"Net volume = ({n(Ag)} - {n(ded)}) x {n(t)} = {n(An * t)} m3")
            self._area_plausibility(eid, etype, t, tr)
            self._geom = {"poly": poly, "frame": fr, "pts": pts, "t": t, "sec": sec, "loc": loc}
        rec["_geom"] = self._geom
        # 7. material
        mat = self.materials.get(mat_name)
        rec["materialGroup"] = (self.o.get("materialAliases") or {}).get(mat_name, mat_name)
        if mat is None:
            self.warn("UNKNOWN_MATERIAL", eid)
            rec["materialGroup"] = None
            return done("Unclassified", f"material '{mat_name}' is not defined; the volume is reported but not counted as concrete.")
        if mat.get("type") == "Steel":
            self.warn("NON_CONCRETE_ELEMENT", eid)
            for k in ("grossConcreteM3", "netConcreteM3", "lengthM", "grossAreaM2", "openingAreaSumM2", "openingDeductedAreaM2", "netAreaM2"):
                if k in rec:
                    rec[k] = None
            rec["materialGroup"] = None
            return done("Skipped", f"material '{mat_name}' is structural steel - not a concrete quantity.")
        if mat.get("type") != "Concrete":
            self.warn("MATERIAL_TYPE_AMBIGUOUS", eid)
            rec["materialGroup"] = None
            return done("Unclassified", f"material '{mat_name}' has type '{mat.get('type')}', which is neither Concrete nor Steel.")
        if rec["materialGroup"] != mat_name:
            tr.append(f"Material '{mat_name}' grouped as '{rec['materialGroup']}' by explicit alias mapping in the run options.")
        rec["status"] = "Included"
        # 8. steel
        if self.o["quantifySteel"]:
            self._steel(rec, e, tr)
        tr.append("Status: Included")
        return rec

    def _frame_plausibility(self, eid, etype, sec, L, tr):
        f = self.f
        dims = [sec["width"] * f, sec["depth"] * f] if sec["shape"] == "Rectangle" else [sec["diameter"] * f]
        rid = "BEAM_SECTION_DIM_MAX" if etype == "Beam" else "COLUMN_SECTION_DIM_MAX"
        if self.rule_on(rid):
            big = [d for d in dims if d > self.thr(rid)]
            if big:
                self.warn("UNREALISTIC_DIMENSION", eid)
                tr.append(f"Plausibility: section dimension {n(max(big))} m > {n(self.thr(rid))} m ({rid}) -> UNREALISTIC_DIMENSION")
                if self.rule_on("UNIT_SCALE_SUSPECT") and any(self.thr("UNIT_SCALE_SUSPECT") <= d / 1000 <= self.thr(rid) for d in big):
                    self.warn("UNIT_PLAUSIBILITY", eid)
                    tr.append(f"Plausibility: {n(max(big))}/1000 = {n(max(big) / 1000)} m would be typical -> probable unit error (UNIT_PLAUSIBILITY). Value is NOT rescaled.")
        if self.rule_on("FRAME_LENGTH_MAX") and L > self.thr("FRAME_LENGTH_MAX"):
            self.warn("UNREALISTIC_DIMENSION", eid)
            tr.append(f"Plausibility: length {n(L)} m > {n(self.thr('FRAME_LENGTH_MAX'))} m -> UNREALISTIC_DIMENSION")

    def _area_plausibility(self, eid, etype, t, tr):
        rid = "SLAB_THICKNESS_MAX" if etype == "Slab" else "WALL_THICKNESS_MAX"
        if self.rule_on(rid) and t > self.thr(rid):
            self.warn("UNREALISTIC_DIMENSION", eid)
            tr.append(f"Plausibility: thickness {n(t)} m > {n(self.thr(rid))} m ({rid}) -> UNREALISTIC_DIMENSION")
            if self.rule_on("UNIT_SCALE_SUSPECT") and self.thr("UNIT_SCALE_SUSPECT") <= t / 1000 <= self.thr(rid):
                self.warn("UNIT_PLAUSIBILITY", eid)
                tr.append(f"Plausibility: {n(t)}/1000 = {n(t / 1000)} m would be a typical thickness -> probable unit error (UNIT_PLAUSIBILITY). Value is NOT rescaled.")

    # ------------------------------------------------------------ steel
    def _steel(self, rec, e, tr):
        eid = rec["id"]
        r = e.get("reinforcement")
        src = (r or {}).get("source", "Unavailable")
        comps = (r or {}).get("components") or []
        rec["reinforcementSource"] = src
        if r is None or (src == "Unavailable" and not comps):
            rec["reinforcementSource"] = "Unavailable"
            self.warn("MISSING_REINFORCEMENT", eid)
            tr.append("Steel: no reinforcement data -> steel UNKNOWN (not zero) (MISSING_REINFORCEMENT)")
            return
        if src not in ALLOWED_COMPONENTS or any(c.get("type") not in ALLOWED_COMPONENTS[src] for c in comps):
            self.warn("REINFORCEMENT_SOURCE_MISMATCH", eid)
            tr.append(f"Steel: source '{src}' does not match component types {[c.get('type') for c in comps]} -> steel unknown (REINFORCEMENT_SOURCE_MISMATCH)")
            return
        if not comps:
            self.warn("INVALID_REINFORCEMENT", eid)
            tr.append(f"Steel: source '{src}' but no components -> steel unknown (INVALID_REINFORCEMENT)")
            return
        exact = src in EXACT_SOURCES
        if "exact" in r and r["exact"] != exact:
            self.warn("EXACTNESS_FLAG_CONFLICT", eid)
            tr.append(f"Steel: input says exact={r['exact']} but source {src} is {'exact' if exact else 'not exact'} -> reported exact={exact} (EXACTNESS_FLAG_CONFLICT)")
        # density
        needs_density = any(c["type"] != "FixedMass" for c in comps)
        sm = r.get("steelMaterial")
        rho = None
        group = sm if sm else ("<default>" if needs_density else "<unspecified>")
        if sm is None:
            rho = self.o["defaultSteelDensityKgPerM3"]
            if needs_density:
                self.warn("DEFAULT_STEEL_DENSITY_USED", eid)
                tr.append(f"Steel density: no steel material named -> default {n(rho)} kg/m3 (DEFAULT_STEEL_DENSITY_USED)")
        elif sm not in self.materials:
            self.warn("UNKNOWN_MATERIAL", eid)
            tr.append(f"Steel: rebar material '{sm}' is not defined -> steel unknown (UNKNOWN_MATERIAL)")
            return
        elif self.materials[sm].get("type") != "Rebar":
            self.warn("INVALID_REINFORCEMENT", eid)
            tr.append(f"Steel: material '{sm}' is not of type Rebar -> steel unknown (INVALID_REINFORCEMENT)")
            return
        else:
            rho = self.rebar_density.get(sm)
            if rho is None:
                if needs_density:
                    tr.append(f"Steel: rebar material '{sm}' has an unusable density -> steel unknown")
                    return
                rho = self.o["defaultSteelDensityKgPerM3"]
        rho_check = rho
        tr.append(f"Steel source: {src} (exact = {exact}); density rho = {n(rho)} kg/m3" if needs_density else f"Steel source: {src} (exact = {exact})")
        kgs = []
        stale_done = set()
        try:
            for ci, c in enumerate(comps):
                kg = self._component(ci, c, rec, rho, tr, stale_done)
                if not math.isfinite(kg) or kg < 0:
                    raise StepError("INVALID_REINFORCEMENT", f"component {ci}: mass must be finite and nonnegative")
                kgs.append(kg)
        except StepError as ex:
            self.warn(ex.code, eid)
            tr.append(f"Steel: {ex.reason} -> steel unknown ({ex.code})")
            return
        total = sum(kgs)
        if not math.isfinite(total) or total < 0:
            self.warn("INVALID_REINFORCEMENT", eid)
            tr.append("Steel total is nonfinite or negative -> steel unknown")
            return
        if src == "EstimatedRatio":
            self.warn("ESTIMATED_REINFORCEMENT", eid)
        if src == "RequiredDesignArea":
            self.warn("REQUIRED_AREA_USED", eid)
        net = rec["netConcreteM3"]
        if net and net > 0:
            rate = total / net
            if rate > rho_check:
                self.warn("UNREALISTIC_STEEL_RATE", eid)
                tr.append(f"Steel rate = {n(total)} / {n(net)} = {n(rate)} kg/m3 > steel density {n(rho_check)} kg/m3 -> physically impossible, steel discarded (UNREALISTIC_STEEL_RATE)")
                return
            if self.rule_on("STEEL_RATE_HIGH") and rate > self.thr("STEEL_RATE_HIGH"):
                self.warn("HIGH_STEEL_RATE", eid)
                tr.append(f"Steel rate = {n(rate)} kg/m3 > {n(self.thr('STEEL_RATE_HIGH'))} kg/m3 -> HIGH_STEEL_RATE")
        rec["steelKg"] = total
        rec["steelExact"] = exact
        rec["steelComponentsKg"] = kgs
        rec["steelMaterialGroup"] = group
        tr.append(f"Element steel = {' + '.join(n(k) for k in kgs)} = {n(total)} kg" if len(kgs) > 1 else f"Element steel = {n(total)} kg")

    def _component(self, ci, c, rec, rho, tr, stale_done):
        f = self.f
        etype = rec["type"]
        is_frame = etype in FRAME_TYPES
        g = rec["_geom"]
        t = c["type"]
        label = f"[{ci}] {t}" + (f" ({c.get('role')})" if c.get("role") else "")

        def pos(name, allow_zero=False):
            v = c.get(name)
            if isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) or (v < 0 if allow_zero else v <= 0):
                raise StepError("INVALID_REINFORCEMENT", f"component {ci} {t}: '{name}' = {v} is invalid")
            return v

        def bar_area(d):
            return math.pi * d * d / 4

        def count_along(zone, off, s):
            clear = zone - 2 * off
            if clear < -LEN_TOL_M:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci} {t}: zone {n(zone)} m shorter than 2 x offset {n(off)} m")
            return math.floor(max(clear, 0.0) / s + EPS_COUNT) + 1, clear

        if t == "StraightBars":
            cnt = pos("count")
            if int(cnt) != cnt:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: count {cnt} is not an integer")
            d = pos("diameter") * f
            if "length" in c:
                L = pos("length") * f
                lsrc = "given"
            elif is_frame:
                L = g["L"]
                lsrc = "member length"
            else:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: StraightBars on a {etype} needs an explicit length")
            A = bar_area(d)
            V = cnt * A * L
            kg = V * rho
            tr.append(f"{label}: {int(cnt)} bars, d = {n(d * 1000)} mm, A_bar = pi x {n(d)}^2 / 4 = {n(A, 9)} m2; L = {n(L)} m ({lsrc})")
            tr.append(f"    V = {int(cnt)} x {n(A, 9)} x {n(L)} = {n(V, 9)} m3; mass = V x rho = {n(kg)} kg")
            return kg
        if t in ("Hoops", "Crossties"):
            d = pos("diameter") * f
            s = pos("spacing") * f
            off = pos("endOffset", allow_zero=True) * f if "endOffset" in c else 0.0
            if "zoneLength" in c:
                zone = pos("zoneLength") * f
            elif is_frame:
                zone = g["L"]
            else:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: {t} on a {etype} needs zoneLength")
            N, clear = count_along(zone, off, s)
            A = bar_area(d)
            if t == "Hoops":
                hook = pos("hookAllowance", allow_zero=True) * f if "hookAllowance" in c else 0.0
                sets = pos("setsPerLocation") if "setsPerLocation" in c else 1
                if "centerlineDiameter" in c:
                    Dc = pos("centerlineDiameter") * f
                    piece = math.pi * Dc + hook
                    pdesc = f"pi x Dc + hook = pi x {n(Dc)} + {n(hook)} = {n(piece)} m (Dc given)"
                elif "centerlineWidth" in c or "centerlineHeight" in c:
                    w = pos("centerlineWidth") * f
                    h = pos("centerlineHeight") * f
                    piece = 2 * (w + h) + hook
                    pdesc = f"2 x (w + h) + hook = 2 x ({n(w)} + {n(h)}) + {n(hook)} = {n(piece)} m (centreline dims given)"
                else:
                    if not is_frame:
                        raise StepError("INVALID_REINFORCEMENT", f"component {ci}: Hoops on a {etype} need explicit centreline dimensions")
                    cov = pos("cover", allow_zero=True) * f if "cover" in c else None
                    if cov is None:
                        raise StepError("INVALID_REINFORCEMENT", f"component {ci}: Hoops need 'cover' or centreline dimensions")
                    sec = g["sec"]
                    if sec["shape"] == "Rectangle":
                        w = sec["width"] * f - 2 * cov - d
                        h = sec["depth"] * f - 2 * cov - d
                        if w <= 0 or h <= 0:
                            raise StepError("INVALID_REINFORCEMENT", f"component {ci}: cover {n(cov)} m leaves no room for the hoop")
                        piece = 2 * (w + h) + hook
                        pdesc = (f"centreline w = b - 2c - d = {n(sec['width'] * f)} - 2 x {n(cov)} - {n(d)} = {n(w)} m; "
                                 f"h = {n(sec['depth'] * f)} - 2 x {n(cov)} - {n(d)} = {n(h)} m; piece = 2 x (w + h) + hook = 2 x ({n(w)} + {n(h)}) + {n(hook)} = {n(piece)} m")
                    else:
                        Dc = sec["diameter"] * f - 2 * cov - d
                        if Dc <= 0:
                            raise StepError("INVALID_REINFORCEMENT", f"component {ci}: cover leaves no room for the hoop")
                        piece = math.pi * Dc + hook
                        pdesc = f"centreline Dc = D - 2c - d = {n(sec['diameter'] * f)} - 2 x {n(cov)} - {n(d)} = {n(Dc)} m; piece = pi x Dc + hook = {n(piece)} m"
                mult = sets
                mdesc = f"{mult} set(s) per location"
            else:
                piece = pos("pieceLength") * f
                mult = pos("perLocation") if "perLocation" in c else 1
                pdesc = f"piece length {n(piece)} m (given)"
                mdesc = f"{mult} piece(s) per location"
            if int(mult) != mult:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: multiplier {mult} is not an integer")
            totlen = N * mult * piece
            kg = totlen * A * rho
            tr.append(f"{label}: d = {n(d * 1000)} mm @ {n(s)} m over zone {n(zone)} m, end offset {n(off)} m")
            tr.append(f"    count N = floor(({n(zone)} - 2 x {n(off)}) / {n(s)}) + 1 = floor({n(clear / s)}) + 1 = {N}")
            tr.append(f"    {pdesc}")
            tr.append(f"    total length = {N} x {mdesc.split()[0]} x {n(piece)} = {n(totlen)} m; A_bar = {n(A, 9)} m2; mass = {n(totlen)} x {n(A, 9)} x {n(rho)} = {n(kg)} kg")
            return kg
        if t == "Mesh":
            if is_frame:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: Mesh is only valid on slabs and walls")
            d = pos("diameter") * f
            s = pos("spacing") * f
            edge = pos("edgeOffset", allow_zero=True) * f if "edgeOffset" in c else 0.0
            endc = pos("endCover", allow_zero=True) * f if "endCover" in c else 0.0
            faces = pos("faces") if "faces" in c else 1
            direc = c.get("direction")
            reg = c.get("region")
            if etype == "Slab":
                if direc not in ("X", "Y"):
                    raise StepError("INVALID_REINFORCEMENT", f"component {ci}: slab mesh direction must be X or Y")
                if reg:
                    x0, x1, y0, y1 = reg["xMin"] * f, reg["xMax"] * f, reg["yMin"] * f, reg["yMax"] * f
                    rdesc = "explicit region"
                else:
                    xy = Polygon([(p[0], p[1]) for p in g["pts"]])
                    x0, y0, x1, y1 = xy.bounds
                    if abs(xy.area - (x1 - x0) * (y1 - y0)) > AREA_TOL_M2:
                        raise StepError("UNSUPPORTED_REINFORCEMENT_LAYOUT", f"component {ci}: slab is not an axis-aligned rectangle and no region is given")
                    rdesc = "host rectangle"
                along, across = (x1 - x0, y1 - y0) if direc == "X" else (y1 - y0, x1 - x0)
            else:
                if direc not in ("Horizontal", "Vertical"):
                    raise StepError("INVALID_REINFORCEMENT", f"component {ci}: wall mesh direction must be Horizontal or Vertical")
                if reg:
                    u0, u1, v0, v1 = reg["uMin"] * f, reg["uMax"] * f, reg["vMin"] * f, reg["vMax"] * f
                    rdesc = "explicit region (wall-local u,v)"
                else:
                    lp = Polygon(g["loc"])
                    u0, v0, u1, v1 = lp.bounds
                    if abs(lp.area - (u1 - u0) * (v1 - v0)) > AREA_TOL_M2:
                        raise StepError("UNSUPPORTED_REINFORCEMENT_LAYOUT", f"component {ci}: wall is not rectangular in its plane and no region is given")
                    rdesc = "host rectangle (wall-local)"
                along, across = (u1 - u0, v1 - v0) if direc == "Horizontal" else (v1 - v0, u1 - u0)
            if along <= 0 or across <= 0:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: empty mesh region")
            Lb = along - 2 * endc
            if Lb <= 0:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: end cover consumes the bar")
            clear = across - 2 * edge
            if clear < -LEN_TOL_M:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: edge offset exceeds the region")
            N = math.floor(max(clear, 0.0) / s + EPS_COUNT) + 1
            A = bar_area(d)
            totlen = faces * N * Lb
            kg = totlen * A * rho
            tr.append(f"{label}: bars along {direc}, d = {n(d * 1000)} mm @ {n(s)} m, {rdesc}: span along = {n(along)} m, span across = {n(across)} m, faces = {faces}")
            tr.append(f"    bars N = floor(({n(across)} - 2 x {n(edge)}) / {n(s)}) + 1 = floor({n(clear / s)}) + 1 = {N}; bar length = {n(along)} - 2 x {n(endc)} = {n(Lb)} m")
            tr.append(f"    total length = {faces} x {N} x {n(Lb)} = {n(totlen)} m; mass = {n(totlen)} x {n(A, 9)} x {n(rho)} = {n(kg)} kg")
            return kg
        if t == "RequiredArea":
            if not is_frame:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: RequiredArea is only supported on frames")
            ref = c.get("designResultRef")
            dr = self.design.get(ref)
            if dr is None:
                raise StepError("DESIGN_RESULT_MISSING", f"component {ci}: no design result for '{ref}'")
            if ref != rec["id"] or dr.get("elementId") != rec["id"]:
                raise StepError("DESIGN_RESULT_OWNER_MISMATCH", f"component {ci}: result '{ref}' does not belong to '{rec['id']}'")
            if ref in self.ambiguous_design:
                raise StepError("INVALID_DESIGN_RESULT", f"component {ci}: multiple results for '{ref}' require explicit reconciliation")
            if dr.get("status") not in {"Current", "Stale"}:
                raise StepError("DESIGN_RESULT_NOT_DESIGNED", f"component {ci}: result status '{dr.get('status')}' cannot supply demand")
            raw_stations = dr.get("stations")
            fields = ("station", "topRequiredArea", "bottomRequiredArea", "shearRequiredAreaPerLength")
            if not isinstance(raw_stations, list) or any(
                not isinstance(q, dict) or any(isinstance(q.get(k), bool) or not isinstance(q.get(k), (int, float))
                    or not math.isfinite(q[k]) or q[k] < 0 for k in fields) for q in raw_stations
            ):
                raise StepError("INVALID_DESIGN_RESULT", f"component {ci}: station positions and demands must be finite and nonnegative")
            if dr.get("status") == "Stale" and ref not in stale_done:
                stale_done.add(ref)
                self.warn("STALE_DESIGN_RESULT", rec["id"])
                tr.append(f"Design result '{ref}' is flagged Stale (STALE_DESIGN_RESULT) - used, but finalization is blocked.")
            st = sorted(raw_stations, key=lambda q: q["station"])
            L = g["L"]
            if len(st) < 2 or abs(st[0]["station"] * f) > STATION_TOL_M or abs(st[-1]["station"] * f - L) > STATION_TOL_M:
                raise StepError("DESIGN_RESULT_MISSING", f"component {ci}: design stations do not cover 0..L")
            xs = [q["station"] * f for q in st]
            top = [q["topRequiredArea"] * f * f for q in st]
            bot = [q["bottomRequiredArea"] * f * f for q in st]
            shr = [q["shearRequiredAreaPerLength"] * f for q in st]
            if any(not math.isfinite(value) for values in (xs, top, bot, shr) for value in values) or any(
                b <= a for a, b in zip(xs, xs[1:])
            ):
                raise StepError("INVALID_DESIGN_RESULT", f"component {ci}: duplicate stations or conversion overflow requires review")
            env = c.get("envelope")

            def integ(vals):
                if env == "MaxOfStations":
                    return max(vals) * L
                if env == "TrapezoidalIntegration":
                    return sum((vals[i] + vals[i + 1]) / 2 * (xs[i + 1] - xs[i]) for i in range(len(xs) - 1))
                if env == "SegmentStepMax":
                    return sum(max(vals[i], vals[i + 1]) * (xs[i + 1] - xs[i]) for i in range(len(xs) - 1))
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: unknown envelope '{env}'")
            Vt, Vb = integ(top), integ(bot)
            tr.append(f"{label}: envelope = {env}; stations x = [{', '.join(n(x) for x in xs)}] m")
            tr.append(f"    top required  (m2) = [{', '.join(n(a, 9) for a in top)}] -> integral = {n(Vt, 9)} m3")
            tr.append(f"    bottom required (m2) = [{', '.join(n(a, 9) for a in bot)}] -> integral = {n(Vb, 9)} m3")
            Vs = 0.0
            if c.get("includeShear", True):
                hl = pos("hoopLength") * f
                legs = pos("legs")
                S = integ(shr)
                Vs = S * hl / legs
                tr.append(f"    shear Av/s (m2/m) = [{', '.join(n(a, 9) for a in shr)}] -> integral = {n(S, 9)} m2 of leg area")
                tr.append(f"    shear steel volume = {n(S, 9)} x hoopLength / legs = {n(S, 9)} x {n(hl)} / {legs} = {n(Vs, 9)} m3")
            V = Vt + Vb + Vs
            kg = V * rho
            tr.append(f"    V = {n(Vt, 9)} + {n(Vb, 9)} + {n(Vs, 9)} = {n(V, 9)} m3; mass = {n(V, 9)} x {n(rho)} = {n(kg)} kg")
            return kg
        if t == "Ratio":
            unit = c.get("rateUnit", "kg/m3")
            if unit not in DENSITY_TO_KG_PER_M3:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: unknown rate unit '{unit}'")
            rate = pos("rate", allow_zero=True) * DENSITY_TO_KG_PER_M3[unit]
            kg = rate * rec["netConcreteM3"]
            tr.append(f"{label}: rate {n(rate)} kg/m3 x net concrete {n(rec['netConcreteM3'])} m3 = {n(kg)} kg (ESTIMATE)")
            return kg
        if t == "FixedMass":
            unit = c.get("massUnit", "kg")
            if unit not in MASS_TO_KG:
                raise StepError("INVALID_REINFORCEMENT", f"component {ci}: unknown mass unit '{unit}'")
            kg = pos("mass", allow_zero=True) * MASS_TO_KG[unit]
            tr.append(f"{label}: manual override mass = {n(kg)} kg")
            return kg
        raise StepError("REINFORCEMENT_SOURCE_MISMATCH", f"unknown component type '{t}'")

    # ------------------------------------------------------------ model checks
    def _model_checks(self):
        inc = [r for r in self.recs if r["status"] == "Included"]
        # slab overlap
        slabs = [r for r in inc if r["type"] == "Slab"]
        for i in range(len(slabs)):
            for j in range(i + 1, len(slabs)):
                a, b = slabs[i], slabs[j]
                if a["story"] != b["story"]:
                    continue
                ga, gb = a["_geom"], b["_geom"]
                if abs(abs(ga["frame"][3][2]) - 1) > 1e-9 or abs(abs(gb["frame"][3][2]) - 1) > 1e-9:
                    continue
                if abs(ga["pts"][0][2] - gb["pts"][0][2]) > PLANE_TOL_M:
                    continue
                pa = Polygon([(p[0], p[1]) for p in ga["pts"]])
                pb = Polygon([(p[0], p[1]) for p in gb["pts"]])
                ov = pa.intersection(pb).area
                if ov > AREA_TOL_M2:
                    self.warn("SLAB_OVERLAP", f"{a['id']}+{b['id']}")
                    self.trace.append(f"Slabs {a['id']} and {b['id']} overlap by {n(ov)} m2 in plan -> SLAB_OVERLAP (both counted).")
        # duplicate geometry
        for i in range(len(inc)):
            for j in range(i + 1, len(inc)):
                a, b = inc[i], inc[j]
                if a["id"] == b["id"] or a["type"] != b["type"] or a["_e"].get("section") != b["_e"].get("section"):
                    continue
                if self._same_geom(a, b):
                    self.warn("DUPLICATE_GEOMETRY", f"{a['id']}+{b['id']}")
                    self.trace.append(f"Elements {a['id']} and {b['id']} have identical geometry -> DUPLICATE_GEOMETRY (both counted).")
        # wall-column overlap
        cols = [r for r in inc if r["type"] == "Column"]
        walls = [r for r in inc if r["type"] == "Wall"]
        for c in cols:
            m = vscale(tuple(x + y for x, y in zip(c["_geom"]["a"], c["_geom"]["b"])), 0.5)
            for w in walls:
                gw = w["_geom"]
                o, u, v, nh = gw["frame"]
                dist = abs(vdot(vsub(m, o), nh))
                if dist > gw["t"] / 2 + LEN_TOL_M:
                    continue
                q = (vdot(vsub(m, o), u), vdot(vsub(m, o), v))
                if gw["poly"].covers(Point(q)):
                    self.warn("WALL_COLUMN_OVERLAP", f"{c['id']}+{w['id']}")
                    self.trace.append(f"Column {c['id']} axis mid-point lies inside wall {w['id']} -> WALL_COLUMN_OVERLAP (no deduction).")

    def _same_geom(self, a, b):
        def key(p):
            return (round(p[0], 9), round(p[1], 9), round(p[2], 9))
        ga, gb = a["_geom"], b["_geom"]
        if a["type"] in FRAME_TYPES:
            return {key(ga["a"]), key(ga["b"])} == {key(gb["a"]), key(gb["b"])}
        return len(ga["pts"]) == len(gb["pts"]) and sorted(map(key, ga["pts"])) == sorted(map(key, gb["pts"]))

    # ------------------------------------------------------------ assembly
    def _warn_list(self):
        return [{"code": c, "severity": catalog.severity(c), "subject": s} for c, s in self.warnings]

    def _finalization(self):
        codes = sorted({c for c, _ in self.warnings if catalog.blocks(c)})
        return {"allowed": not codes, "blockingCodes": codes}

    def _rejected(self):
        return {
            "snapshotStatus": "Rejected",
            "finalization": self._finalization(),
            "modelTotals": None, "completeness": None,
            "byStory": {}, "byElementType": {}, "byMaterial": {}, "steelByMaterial": {},
            "elements": [], "warnings": self._warn_list(), "tolerance": dict(TOLERANCE),
        }

    def _assemble(self):
        qs = self.o["quantifySteel"]
        z = lambda: {"grossConcreteM3": 0.0, "netConcreteM3": 0.0, "steelKg": 0.0 if qs else None}
        by_story = {st: z() for st in self.stories}
        by_type = {t: z() for t in ["Beam", "Column", "Slab", "Wall"]}
        by_mat = {}
        steel_mat = {}
        src_tot = {k: 0.0 for k in SOURCES}
        tot = {"grossConcreteM3": 0.0, "openingDeductionM3": 0.0, "netConcreteM3": 0.0, "unclassifiedVolumeM3": 0.0}
        steel = 0.0
        for r in self.recs:
            if r["status"] == "Unclassified":
                tot["unclassifiedVolumeM3"] += r["netConcreteM3"]
                continue
            if r["status"] != "Included":
                continue
            st = r["story"]
            if st not in by_story:
                by_story[st] = z()
            g, nv = r["grossConcreteM3"], r["netConcreteM3"]
            tot["grossConcreteM3"] += g
            tot["netConcreteM3"] += nv
            tot["openingDeductionM3"] += g - nv
            for bucket in (by_story[st], by_type[r["type"]]):
                bucket["grossConcreteM3"] += g
                bucket["netConcreteM3"] += nv
            mg = by_mat.setdefault(r["materialGroup"], {"grossConcreteM3": 0.0, "netConcreteM3": 0.0})
            mg["grossConcreteM3"] += g
            mg["netConcreteM3"] += nv
            if qs and r["steelKg"] is not None:
                k = r["steelKg"]
                steel += k
                by_story[st]["steelKg"] += k
                by_type[r["type"]]["steelKg"] += k
                src_tot[r["reinforcementSource"]] += k
                steel_mat[r["steelMaterialGroup"]] = steel_mat.get(r["steelMaterialGroup"], 0.0) + k
        exact = src_tot["ProvidedBars"] + src_tot["ManualOverride"]
        approx = src_tot["RequiredDesignArea"] + src_tot["EstimatedRatio"]
        in_scope = [r for r in self.recs if r["status"] in ("Included", "Rejected", "Unquantified", "Unclassified")]
        c_unknown = []
        for r in in_scope:
            if r["status"] != "Included" and r["id"] not in c_unknown:
                c_unknown.append(r["id"])
        s_unknown = []
        if qs:
            for r in in_scope:
                if r["steelKg"] is None and r["id"] not in s_unknown:
                    s_unknown.append(r["id"])
        model = dict(tot)
        model.update({
            "steelKg": steel if qs else None,
            "steelExactKg": exact if qs else None,
            "steelApproximateKg": approx if qs else None,
            "steelKgBySource": src_tot if qs else None,
        })
        elements = []
        for r in self.recs:
            out = {k: v for k, v in r.items() if not k.startswith("_") and k != "steelMaterialGroup"}
            elements.append(out)
        return {
            "snapshotStatus": "Accepted",
            "finalization": self._finalization(),
            "modelTotals": model,
            "completeness": {
                "concrete": "Incomplete" if c_unknown else "Complete",
                "steel": ("Incomplete" if s_unknown else "Complete") if qs else "NotEvaluated",
                "concreteUnknownElementIds": c_unknown,
                "steelUnknownElementIds": s_unknown,
            },
            "byStory": by_story,
            "byElementType": by_type,
            "byMaterial": by_mat,
            "steelByMaterial": steel_mat,
            "elements": elements,
            "warnings": self._warn_list(),
            "tolerance": dict(TOLERANCE),
        }


def evaluate(snapshot, options=None):
    ev = Evaluator(snapshot, options)
    result = ev.run()
    return result, ev
