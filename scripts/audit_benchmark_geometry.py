#!/usr/bin/env python3
"""Review-only bridge from the external benchmark format to Quentra schema 2.

Checks concrete geometry, NOT steel, warning parity, classification, finalization,
material aliases or raw ETABS extraction. It never edits benchmark source files
or derives inputs from expected outputs. Unsupported ingestion contracts are
reported as NOT COMPARED, not passed. Requires a built Quentra CLI and Python 3.
"""
import argparse
import collections
from decimal import Decimal
import json
import math
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
LENGTH = {"m": 1, "mm": .001, "cm": .01, "ft": .3048, "in": .0254}
NATIVE = {"m": "Metre", "mm": "Millimetre", "ft": "Foot", "in": "Inch"}


class NotComparable(Exception):
    pass


def convert(source, case_id):
    if source.get("schemaVersion") != "1.0":
        raise NotComparable("Source schema rejection needs a separate ingestion test")
    provenance = source.get("provenance", {})
    if provenance.get("type") != "synthetic_engineering_benchmark" or provenance.get("etabs_verified") is not False:
        raise NotComparable("Provenance rejection needs a separate ingestion test")
    unit = source["model"]["units"]["length"]
    if unit not in LENGTH:
        raise NotComparable("Unsupported source unit")
    # Centimetres are not a native Quentra unit. This adapter explicitly converts
    # them; the case therefore does not demonstrate native centimetre support.
    scale = LENGTH[unit] if unit not in NATIVE else 1
    target_unit = NATIVE.get(unit, "Metre")
    factor = LENGTH[unit]

    def point(p):
        return {axis: p[axis] * scale for axis in ("x", "y", "z")}

    def length(value):
        return {"value": value * scale, "unit": target_unit}

    materials = {m["name"]: m for m in source["materials"]}
    sections = {s["name"]: s for s in source["sections"]}
    if len(materials) != len(source["materials"]) or len(sections) != len(source["sections"]):
        raise NotComparable("Duplicate material/section names need ingestion policy")
    frames, areas, metadata, ids = [], [], [], set()
    # Compute the benchmark's top-minus-height decimal convention before casting
    # to binary doubles: 9.6 - 3.2 must not create a spurious overlap with 6.4.
    decimal_factor = Decimal(str(factor))
    stories = [{"id": s["name"],
                "lowerElevationM": float((Decimal(str(s["elevation"])) - Decimal(str(s["height"]))) * decimal_factor),
                "upperElevationM": float(Decimal(str(s["elevation"])) * decimal_factor)} for s in source["model"]["stories"]]
    story_ids = {s["id"] for s in stories}
    for e in source["elements"]:
        eid = e["id"]
        if eid in ids:
            raise NotComparable("Duplicate identities: benchmark and production rejection policies differ")
        ids.add(eid)
        if e.get("role", "Physical") != "Physical":
            raise NotComparable("Analytical/mesh-parent normalization is outside this geometry bridge")
        if e["type"] not in {"Beam", "Column", "Slab", "Wall"}:
            raise NotComparable("Unsupported element type needs ingestion classification")
        if e.get("material") not in materials or materials[e["material"]]["type"] != "Concrete":
            raise NotComparable("Unknown/non-concrete material semantics are outside this bridge")
        section = sections.get(e.get("section"))
        if section is None:
            raise NotComparable("Missing section needs ingestion classification")
        if e.get("story") not in story_ids:
            raise NotComparable("Unknown story reference needs an explicit policy mapping")
        reference = f"benchmark:{case_id}/elements/{eid}; SYNTHETIC; not ETABS"
        metadata.append({"objectId": eid, "materialName": e["material"], "sectionName": e["section"],
                         "assignedStoryId": e["story"], "requiredSteelComponents": ["Longitudinal", "Transverse"]
                         if e["type"] in {"Beam", "Column"} else ["Web"]})
        g = e["geometry"]
        if e["type"] in {"Beam", "Column"}:
            if section["kind"] != "Frame" or section.get("shape") not in {"Rectangle", "Circle"}:
                raise NotComparable("Unsupported section outside geometry-only comparison")
            if e["type"] == "Beam" and section["shape"] == "Circle":
                raise NotComparable("Benchmark supports circular beams; production MVP does not")
            shape = {"shape": section["shape"]}
            for dimension in (["width", "depth"] if section["shape"] == "Rectangle" else ["diameter"]):
                if dimension not in section:
                    raise NotComparable("Missing section dimension")
                shape[dimension] = length(section[dimension])
            frames.append({"objectId": eid, "sourceReference": reference, "kind": e["type"], "material": "Concrete",
                           "isStraight": True, "isPrismatic": True, "start": point(g["start"]), "end": point(g["end"]), "section": shape})
        else:
            if section["kind"] != e["type"] or "thickness" not in section:
                raise NotComparable("Unsupported/missing area section")
            # Verification flags mean the synthetic fixture explicitly specifies the
            # host and physical thickness. They never attest to an ETABS capture.
            areas.append({"objectId": eid, "sourceReference": reference, "kind": e["type"], "material": "Concrete",
                          "boundary": [point(p) for p in g["points"]],
                          "openings": [[point(p) for p in o["points"]] for o in g.get("openings", [])],
                          "openingsVerified": True, "physicalThicknessVerified": True, "thickness": length(section["thickness"])})
    return {"schemaVersion": 2,
            "model": {"schemaVersion": 1, "modelId": case_id, "origin": "Synthetic",
                      "sourceDescription": "Geometry-only review conversion; no steel or ETABS verification. " + source["model"]["name"],
                      "capturedAt": "2026-09-27T00:00:00Z", "coordinateUnit": target_unit, "frames": frames},
            "areas": areas, "stories": stories, "metadata": metadata, "reinforcement": [],
            "policy": {"id": "benchmark-geometry-review-unapproved", "intendedUse": "Synthetic geometry comparison only",
                       "approvedBy": None, "approvedAt": None, "steelDensityKgM3": 7850,
                       "linearToleranceM": 1e-6, "planarityToleranceM": 1e-4}}


def compare(actual, expected):
    mismatches = []
    tol = expected["tolerance"]

    def check(path, a, b, absolute):
        if (a is None) != (b is None) or (a is not None and not math.isclose(a, b, rel_tol=tol["relative"], abs_tol=absolute)):
            mismatches.append({"field": path, "actual": a, "expected": b, "absoluteTolerance": absolute})

    summary = actual["summary"]
    for key, source_key in [("knownGrossM3", "grossConcreteM3"), ("knownOpeningAdjustedM3", "netConcreteM3")]:
        check("summary." + key, summary[key], expected["modelTotals"][source_key], tol["volumeM3"])
    by_id = {e["objectId"]: e for e in actual["elements"]}
    if set(by_id) != {e["id"] for e in expected["elements"]}:
        mismatches.append({"field": "elementIds", "actual": sorted(by_id), "expected": sorted(e["id"] for e in expected["elements"])})
    for e in expected["elements"]:
        if e["id"] not in by_id:
            continue
        a = by_id[e["id"]]
        for key, source_key, tolerance in [("grossM3", "grossConcreteM3", "volumeM3"),
                                           ("openingAdjustedM3", "netConcreteM3", "volumeM3"),
                                           ("surfaceAreaM2", "netAreaM2", "areaM2"), ("axisLengthM", "lengthM", "lengthM")]:
            if source_key in e:
                check(e["id"] + "." + key, a[key], e[source_key], tol[tolerance])
    return mismatches


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=str(ROOT / ".tools/dotnet/dotnet"))
    parser.add_argument("--cli", type=Path, default=ROOT / "src/Quentra.Cli/bin/Release/net10.0/Quentra.Cli.dll")
    parser.add_argument("--output", type=Path, required=True, help="New directory for converted snapshots and comparison evidence")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    fixture_root = ROOT / "quentra-benchmarks/tests/fixtures"
    manifest = json.loads((fixture_root / "manifest.json").read_text())
    rows = []
    for case in manifest["cases"]:
        folder = fixture_root / case["path"]
        source = json.loads((folder / "snapshot.json").read_text())
        expected = json.loads((folder / "expected.json").read_text())
        row = {"id": case["id"], "category": case["category"], "scope": "concrete geometry only"}
        try:
            converted = convert(source, case["id"])
            if expected["modelTotals"] is None:
                raise NotComparable("Whole-snapshot rejection is outside geometry-only audit")
        except (NotComparable, KeyError) as error:
            rows.append({**row, "status": "NOT_COMPARED", "reason": str(error)})
            continue
        input_file = args.output / (case["id"] + "-input.json")
        output_file = args.output / (case["id"] + "-run.json")
        input_file.write_text(json.dumps(converted, ensure_ascii=False, allow_nan=False, indent=2) + "\n")
        started = time.perf_counter()
        result = subprocess.run([args.dotnet, str(args.cli), "calculate", str(input_file), str(output_file)],
                                capture_output=True, text=True, timeout=120)
        row["cliElapsedSeconds"] = round(time.perf_counter() - started, 4)
        if result.returncode:
            rows.append({**row, "status": "ENGINE_REJECTED", "reason": result.stderr.strip()})
            continue
        actual = json.loads(output_file.read_text())["result"]
        differences = compare(actual, expected)
        rows.append({**row, "status": "MISMATCH" if differences else "GEOMETRY_MATCH", "differences": differences,
                     "elementCount": len(actual["elements"]), "engineVersion": actual["calculationVersion"]})
    totals = dict(collections.Counter(row["status"] for row in rows))
    report = {"scope": "Geometry-only comparison; NOT full-suite compatibility. No steel, warning, material-alias, story-policy or finalization assertions.",
              "unitNote": "cm normalized to metres by review adapter; other supported source units preserved",
              "counts": totals, "cases": rows}
    (args.output / "comparison.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(totals, indent=2))
    for row in rows:
        if row["status"] in {"MISMATCH", "ENGINE_REJECTED"}:
            print(row["id"], row.get("reason", row.get("differences")))
    return 1 if totals.get("MISMATCH", 0) or totals.get("ENGINE_REJECTED", 0) else 0


if __name__ == "__main__":
    raise SystemExit(main())
