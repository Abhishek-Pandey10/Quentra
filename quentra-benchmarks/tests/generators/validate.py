#!/usr/bin/env python3
"""Validate every fixture on disk, independently of any engine.

  * snapshot.json against schemas/snapshot.schema.json (fixtures flagged schemaValid=false must FAIL it)
  * expected.json against schemas/expected.schema.json
  * provenance: synthetic fixtures are never etabs_verified; captures always name an ETABS version
  * global invariants on every expected.json (the same ones the C# harness applies to engine output)

Exit code 1 on any problem.
"""
import json
import math
import os
import sys

from jsonschema import Draft202012Validator
from referencing import Registry, Resource

HERE = os.path.dirname(os.path.abspath(__file__))
TESTS = os.path.dirname(HERE)
FIX = os.path.join(TESTS, "fixtures")
SCH = os.path.join(TESTS, "schemas")


def load(p):
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def validators():
    schemas = {n: load(os.path.join(SCH, n)) for n in os.listdir(SCH) if n.endswith(".json")}
    reg = Registry()
    for name, s in schemas.items():
        res = Resource.from_contents(s)
        reg = reg.with_resource(s["$id"], res).with_resource(name, res)
    return {n: Draft202012Validator(s, registry=reg) for n, s in schemas.items()}


def close(a, b, tol, rel=1e-9):
    return abs(a - b) <= max(tol, rel * max(abs(a), abs(b)))


def invariants(exp):
    errs = []
    if exp["snapshotStatus"] == "Rejected":
        if exp["modelTotals"] is not None or exp["elements"]:
            errs.append("rejected snapshot must have null totals and no elements")
        if exp["finalization"]["allowed"]:
            errs.append("rejected snapshot cannot be finalizable")
        return errs
    t = exp["tolerance"]
    mt = exp["modelTotals"]
    vt, st = t["volumeM3"], t["steelKg"]
    if mt["netConcreteM3"] > mt["grossConcreteM3"] + vt:
        errs.append("net > gross")
    if not close(mt["grossConcreteM3"] - mt["openingDeductionM3"], mt["netConcreteM3"], vt):
        errs.append("gross - openings != net")
    for k in ("grossConcreteM3", "netConcreteM3", "openingDeductionM3", "unclassifiedVolumeM3"):
        if mt[k] < -vt:
            errs.append(f"negative {k}")
    for key in ("grossConcreteM3", "netConcreteM3"):
        for grp in ("byStory", "byElementType", "byMaterial"):
            s = sum(b[key] for b in exp[grp].values())
            if not close(s, mt[key], vt):
                errs.append(f"sum {grp}.{key} = {s} != model {mt[key]}")
    inc = [e for e in exp["elements"] if e["status"] == "Included"]
    if not close(sum(e["netConcreteM3"] for e in inc), mt["netConcreteM3"], vt):
        errs.append("sum of included elements != model net")
    for e in exp["elements"]:
        if e["status"] in ("Excluded", "Skipped", "Rejected", "Unquantified"):
            if e.get("netConcreteM3") is not None or e.get("steelKg") is not None:
                errs.append(f"{e['id']}: {e['status']} element carries a quantity")
        if e.get("netConcreteM3") is not None and e["netConcreteM3"] < -vt:
            errs.append(f"{e['id']}: negative volume")
        if e.get("netConcreteM3") is not None and e["netConcreteM3"] > e["grossConcreteM3"] + vt:
            errs.append(f"{e['id']}: net > gross")
        if e.get("openingDeductedAreaM2") is not None and e["openingDeductedAreaM2"] > min(e["grossAreaM2"], e["openingAreaSumM2"]) + t["areaM2"]:
            errs.append(f"{e['id']}: deducted opening area exceeds host or opening sum")
        if e.get("steelKg") is not None and e["steelKg"] < -st:
            errs.append(f"{e['id']}: negative steel")
    comp = exp["completeness"]
    if mt["steelKg"] is None:
        if comp["steel"] != "NotEvaluated":
            errs.append("steel null but completeness not NotEvaluated")
    else:
        if mt["steelKg"] < -st:
            errs.append("negative model steel")
        if not close(sum(mt["steelKgBySource"].values()), mt["steelKg"], st):
            errs.append("steel by source != steel")
        if not close(mt["steelExactKg"] + mt["steelApproximateKg"], mt["steelKg"], st):
            errs.append("exact + approximate != steel")
        for grp in ("byStory", "byElementType"):
            if not close(sum(b["steelKg"] for b in exp[grp].values()), mt["steelKg"], st):
                errs.append(f"sum {grp}.steelKg != model steel")
        if not close(sum(exp["steelByMaterial"].values()), mt["steelKg"], st):
            errs.append("steelByMaterial != model steel")
        unknown = [e["id"] for e in exp["elements"] if e["status"] in ("Included", "Rejected", "Unquantified", "Unclassified") and e["steelKg"] is None]
        if sorted(set(unknown)) != sorted(comp["steelUnknownElementIds"]):
            errs.append("steelUnknownElementIds inconsistent (unknown must not silently become zero)")
        if (comp["steel"] == "Complete") != (not comp["steelUnknownElementIds"]):
            errs.append("steel completeness flag inconsistent")
    blocking = exp["finalization"]["blockingCodes"]
    if exp["finalization"]["allowed"] == bool(blocking):
        errs.append("finalization.allowed inconsistent with blockingCodes")
    codes = {w["code"] for w in exp["warnings"]}
    if not set(blocking) <= codes:
        errs.append("blocking code without matching warning")
    keys = [(w["code"], w["subject"]) for w in exp["warnings"]]
    if len(keys) != len(set(keys)):
        errs.append("duplicate (code, subject) warning")
    return errs


def main():
    v = validators()
    manifest = load(os.path.join(FIX, "manifest.json"))
    errs = list(v["manifest.schema.json"].iter_errors(manifest))
    problems = [f"manifest: {e.message}" for e in errs]
    catalog = load(os.path.join(TESTS, "config", "warning-codes.json"))
    problems += [f"warning-codes.json: {e.message}" for e in v["warnings.schema.json"].iter_errors(catalog)]
    known_codes = {c["code"]: c for c in catalog["codes"]}
    n = 0
    for case in manifest["cases"]:
        d = os.path.join(FIX, case["path"])
        snap = load(os.path.join(d, "snapshot.json"))
        exp = load(os.path.join(d, "expected.json"))
        n += 1
        serr = list(v["snapshot.schema.json"].iter_errors(snap))
        if case["schemaValid"] and serr:
            problems += [f"{case['id']} snapshot: {e.message} at {list(e.absolute_path)}" for e in serr[:3]]
        if not case["schemaValid"] and not serr:
            problems.append(f"{case['id']}: flagged schemaValid=false but passes the schema")
        problems += [f"{case['id']} expected: {e.message} at {list(e.absolute_path)}" for e in list(v["expected.schema.json"].iter_errors(exp))[:3]]
        prov = snap.get("provenance") or {}
        if case["schemaValid"] and not (prov.get("type") == "synthetic_engineering_benchmark" and prov.get("etabs_verified") is False):
            problems.append(f"{case['id']}: fixture is not labelled synthetic/unverified")
        for w in exp["warnings"]:
            c = known_codes.get(w["code"])
            if c is None:
                problems.append(f"{case['id']}: unknown warning code {w['code']}")
            elif c["severity"] != w["severity"]:
                problems.append(f"{case['id']}: {w['code']} severity {w['severity']} != catalog {c['severity']}")
        problems += [f"{case['id']}: {m}" for m in invariants(exp)]
    # captured ETABS snapshots, if any
    cap_root = os.path.join(FIX, "captured-etabs")
    caps = 0
    for root, _, fnames in os.walk(cap_root):
        for fn in fnames:
            if fn.endswith("_snapshot.json") or fn == "snapshot.json":
                caps += 1
                snap = load(os.path.join(root, fn))
                prov = snap.get("provenance") or {}
                if prov.get("type") != "captured_etabs_snapshot" or prov.get("etabs_verified") is not True or not prov.get("etabs_version"):
                    problems.append(f"{os.path.relpath(os.path.join(root, fn), FIX)}: captured snapshot without valid ETABS provenance")
    if problems:
        print("\n".join(problems))
        print(f"\nFAILED: {len(problems)} problem(s) across {n} cases.")
        sys.exit(1)
    print(f"OK: {n} cases schema-valid and invariant-clean; {caps} captured ETABS snapshot(s) checked.")


if __name__ == "__main__":
    main()
