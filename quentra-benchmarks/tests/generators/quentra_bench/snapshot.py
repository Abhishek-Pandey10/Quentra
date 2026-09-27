"""Builder for normalized Quentra snapshot dictionaries (schema 1.0).

Field names describe a *normalized Quentra snapshot layer*. They are NOT claimed to be
ETABS API names. The `source` block on each element is where a real extractor records
ETABS object names/GUIDs/API methods; the synthetic generator leaves those null.
"""
import copy

GENERATOR = "quentra_bench"
GENERATOR_VERSION = "1.1.0"


def P(x, y, z):
    return {"x": x, "y": y, "z": z}


def synthetic_provenance(case_id, notes=None):
    return {
        "type": "synthetic_engineering_benchmark",
        "etabs_verified": False,
        "etabs_version": None,
        "generator": GENERATOR,
        "generatorVersion": GENERATOR_VERSION,
        "caseId": case_id,
        "notes": notes or "Hand-designed synthetic benchmark. Not captured from ETABS.",
    }


class Snap:
    def __init__(self, case_id, name, unit="m", stories=None, description=None):
        self.case_id = case_id
        self.d = {
            "schemaVersion": "1.0",
            "model": {
                "name": name,
                "description": description,
                "units": {"length": unit, "force": "kN" if unit in ("m", "mm", "cm") else "kip", "temperature": "C" if unit in ("m", "mm", "cm") else "F"},
                "source": "synthetic",
                "stories": [],
            },
            "materials": [],
            "sections": [],
            "elements": [],
            "designResults": [],
            "warnings": [],
            "provenance": synthetic_provenance(case_id),
        }
        for s in stories or []:
            self.story(*s)

    # ---- model ----
    def story(self, name, elevation, height):
        self.d["model"]["stories"].append({"name": name, "elevation": elevation, "height": height})
        return self

    def material(self, name, mtype, density=None, density_unit="kg/m3", grade=None, fck_mpa=None, fy_mpa=None):
        m = {"name": name, "type": mtype, "grade": grade}
        if density is not None:
            m["density"] = density
            m["densityUnit"] = density_unit
        if fck_mpa is not None:
            m["fckMPa"] = fck_mpa
        if fy_mpa is not None:
            m["fyMPa"] = fy_mpa
        self.d["materials"].append(m)
        return self

    def concrete(self, name, fck=None):
        return self.material(name, "Concrete", grade=name, fck_mpa=fck)

    def rebar(self, name, density=7850.0, density_unit="kg/m3", fy=None):
        return self.material(name, "Rebar", density=density, density_unit=density_unit, grade=name, fy_mpa=fy)

    def rect(self, name, width, depth):
        self.d["sections"].append({"name": name, "kind": "Frame", "shape": "Rectangle", "width": width, "depth": depth})
        return self

    def circle(self, name, diameter):
        self.d["sections"].append({"name": name, "kind": "Frame", "shape": "Circle", "diameter": diameter})
        return self

    def slab_section(self, name, thickness):
        self.d["sections"].append({"name": name, "kind": "Slab", "thickness": thickness})
        return self

    def wall_section(self, name, thickness):
        self.d["sections"].append({"name": name, "kind": "Wall", "thickness": thickness})
        return self

    def raw_section(self, sec):
        self.d["sections"].append(sec)
        return self

    # ---- elements ----
    def _element(self, eid, etype, story, material, section, geometry, name=None, rebar=None,
                 role="Physical", parent=None, extra=None):
        e = {
            "id": eid,
            "name": name if name is not None else eid,
            "type": etype,
            "role": role,
            "parentId": parent,
            "story": story,
            "material": material,
            "section": section,
            "geometry": geometry,
        }
        if rebar is not None:
            e["reinforcement"] = rebar
        e["source"] = {"application": GENERATOR, "version": GENERATOR_VERSION, "objectName": e["name"],
                       "objectGuid": None, "apiMethod": None}
        if extra:
            e.update(extra)
        self.d["elements"].append(e)
        return e

    def frame(self, eid, etype, story, section, material, start, end, **kw):
        g = {"kind": "Line", "start": P(*start), "end": P(*end)}
        return self._element(eid, etype, story, material, section, g, **kw)

    def beam(self, eid, story, section, material, start, end, **kw):
        return self.frame(eid, "Beam", story, section, material, start, end, **kw)

    def column(self, eid, story, section, material, start, end, **kw):
        return self.frame(eid, "Column", story, section, material, start, end, **kw)

    def area(self, eid, etype, story, section, material, points, openings=None, **kw):
        g = {"kind": "Area", "points": [P(*p) for p in points],
             "openings": [{"id": oid, "points": [P(*p) for p in pts]} for oid, pts in (openings or [])]}
        return self._element(eid, etype, story, material, section, g, **kw)

    def slab(self, eid, story, section, material, points, openings=None, **kw):
        return self.area(eid, "Slab", story, section, material, points, openings, **kw)

    def wall(self, eid, story, section, material, points, openings=None, **kw):
        return self.area(eid, "Wall", story, section, material, points, openings, **kw)

    def raw_element(self, e):
        self.d["elements"].append(e)
        return e

    def design(self, element_id, stations, status="Current", result_type="ConcreteFrameDesign"):
        self.d["designResults"].append({
            "elementId": element_id,
            "resultType": result_type,
            "status": status,
            "origin": "simulated",
            "stations": [{"station": s, "topRequiredArea": t, "bottomRequiredArea": b, "shearRequiredAreaPerLength": v}
                         for (s, t, b, v) in stations],
        })
        return self

    def source_warning(self, code, message, subject=None):
        self.d["warnings"].append({"code": code, "severity": "Warning", "subject": subject, "message": message})
        return self

    def to_dict(self):
        return copy.deepcopy(self.d)


# ---------- reinforcement helpers ----------

def provided(components, steel="B500", exact=None):
    r = {"source": "ProvidedBars", "steelMaterial": steel, "components": components}
    if exact is not None:
        r["exact"] = exact
    return r


def bars(count, dia, length=None, role="main"):
    c = {"type": "StraightBars", "role": role, "count": count, "diameter": dia}
    if length is not None:
        c["length"] = length
    return c


def hoops(dia, spacing, cover=None, end_offset=0.0, zone_length=None, hook=0.0, sets=1,
          cl_width=None, cl_height=None, cl_diameter=None, role="ties"):
    c = {"type": "Hoops", "role": role, "diameter": dia, "spacing": spacing, "endOffset": end_offset,
         "hookAllowance": hook, "setsPerLocation": sets}
    if cover is not None:
        c["cover"] = cover
    if zone_length is not None:
        c["zoneLength"] = zone_length
    if cl_width is not None:
        c["centerlineWidth"] = cl_width
    if cl_height is not None:
        c["centerlineHeight"] = cl_height
    if cl_diameter is not None:
        c["centerlineDiameter"] = cl_diameter
    return c


def crossties(dia, spacing, piece_length, end_offset=0.0, zone_length=None, per_location=1, role="crossties"):
    c = {"type": "Crossties", "role": role, "diameter": dia, "spacing": spacing, "pieceLength": piece_length,
         "endOffset": end_offset, "perLocation": per_location}
    if zone_length is not None:
        c["zoneLength"] = zone_length
    return c


def mesh(direction, dia, spacing, edge_offset, end_cover, layer="bottom", faces=1, region=None):
    c = {"type": "Mesh", "role": layer, "direction": direction, "diameter": dia, "spacing": spacing,
         "edgeOffset": edge_offset, "endCover": end_cover, "faces": faces}
    if region is not None:
        c["region"] = region
    return c


def required(ref, envelope, hoop_length=None, legs=None, include_shear=True):
    c = {"type": "RequiredArea", "designResultRef": ref, "envelope": envelope, "includeShear": include_shear}
    if hoop_length is not None:
        c["hoopLength"] = hoop_length
    if legs is not None:
        c["legs"] = legs
    return c


def ratio(rate, unit="kg/m3"):
    return {"type": "Ratio", "rate": rate, "rateUnit": unit}


def fixed_mass(mass, unit="kg"):
    return {"type": "FixedMass", "mass": mass, "massUnit": unit}
