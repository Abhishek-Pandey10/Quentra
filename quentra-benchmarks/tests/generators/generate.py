#!/usr/bin/env python3
"""Generate every Quentra golden benchmark fixture from first principles.

    python3 tests/generators/generate.py            # (re)write fixtures, manifest, config, docs tables
    python3 tests/generators/generate.py --check    # regenerate in memory and fail if anything on disk differs

For each case this:
  1. builds the snapshot (structural truth -> ETABS-like input),
  2. runs the independent calculator (evaluator.py),
  3. refuses to continue unless every hand-worked value in the case matches,
  4. for equivalence groups, refuses unless all members give identical physical quantities,
  5. writes snapshot.json, expected.json and README.md.

Nothing here reads a Quentra result. Expected values flow only from the engineering definition.
"""
import argparse
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from quentra_bench import catalog, buildings  # noqa: E402
from quentra_bench.case import run_case, render_readme  # noqa: E402
from quentra_bench.cases import geometry, materials_units, reinforcement, model_checks, design_guards  # noqa: E402
from quentra_bench.comparison import expected_equal  # noqa: E402
from validate import invariants  # noqa: E402
from quentra_bench.snapshot import GENERATOR, GENERATOR_VERSION  # noqa: E402

TESTS = os.path.dirname(HERE)
FIX = os.path.join(TESTS, "fixtures")
CONFIG = os.path.join(TESTS, "config")
SUITE_VERSION = "1.1.0"
SCHEMA_INVALID = {"INVALID-020", "INVALID-021", "INVALID-026", "INVALID-027"}


def all_cases():
    cases = geometry.all_cases() + materials_units.all_cases() + reinforcement.all_cases() + model_checks.all_cases() + buildings.all_cases() + design_guards.all_cases()
    ids = [c.id for c in cases]
    dupes = {i for i in ids if ids.count(i) > 1}
    if dupes:
        raise SystemExit(f"duplicate case ids: {dupes}")
    return cases


def dumps(o):
    return json.dumps(o, indent=2, ensure_ascii=False, allow_nan=False) + "\n"


def physical_signature(result):
    """Quantities that must be identical across an equivalence group."""
    mt = result["modelTotals"]
    sig = {k: mt[k] for k in ("grossConcreteM3", "netConcreteM3", "openingDeductionM3", "steelKg")}
    for t, b in result["byElementType"].items():
        sig[f"type.{t}.net"] = b["netConcreteM3"]
        sig[f"type.{t}.steel"] = b["steelKg"]
    for e in result["elements"]:
        sig[f"el.{e['id']}.net"] = e["netConcreteM3"]
        sig[f"el.{e['id']}.steel"] = e["steelKg"]
    return sig


def check_equivalence(results):
    groups = {}
    for case, res in results:
        if case.equivalence_group:
            groups.setdefault(case.equivalence_group, []).append((case, res))
    for g, members in groups.items():
        ref_case, ref = members[0]
        rs = physical_signature(ref)
        for case, res in members[1:]:
            s = physical_signature(res)
            for k, v in rs.items():
                w = s[k]
                if (v is None) != (w is None) or (v is not None and not math.isclose(v, w, rel_tol=1e-9, abs_tol=1e-9)):
                    raise AssertionError(f"equivalence group {g}: {case.id}.{k} = {w} differs from {ref_case.id} = {v}")
    return groups


def build(write=True):
    cases = all_cases()
    results = []
    files = {}
    manifest = {"suiteVersion": SUITE_VERSION, "schemaVersion": "1.0", "generatedBy": f"{GENERATOR} {GENERATOR_VERSION}", "cases": []}
    for c in cases:
        res, ev = run_case(c)
        problems = invariants(res)
        if problems:
            raise AssertionError(f"{c.id}: generated output violates invariants: {problems}")
        res["derivation"] = {"method": "independent_first_principles", "generator": GENERATOR, "generatorVersion": GENERATOR_VERSION,
                             "handChecks": len(c.hand), "closedFormCrossCheck": c.kind == "buildings"}
        results.append((c, res))
        folder = os.path.join(c.kind, c.id)
        files[os.path.join(folder, "snapshot.json")] = dumps(c.snapshot)
        files[os.path.join(folder, "expected.json")] = dumps(res)
        files[os.path.join(folder, "README.md")] = render_readme(c, res, ev)
        manifest["cases"].append({
            "id": c.id, "name": c.name, "category": c.category, "fixtureSet": c.kind, "path": folder.replace(os.sep, "/"),
            "expectedStatus": c.expected_status, "reason": None, "equivalenceGroup": c.equivalence_group, "options": c.options,
            "schemaValid": c.id not in SCHEMA_INVALID, "snapshotStatus": res["snapshotStatus"], "expectedBehaviour": c.expected_behaviour,
            "tags": c.tags, "captures": [],
        })
    groups = check_equivalence(results)
    files["manifest.json"] = dumps(manifest)
    cfg = {
        os.path.join("..", "config", "warning-codes.json"): dumps({
            "catalogVersion": SUITE_VERSION,
            "severityMeaning": {"Fatal": "snapshot rejected, no quantities", "Error": "element or its steel not trusted; blocks finalization",
                                "Warning": "quantities produced, human review; some block finalization", "Info": "traceability only"},
            "codes": [{"code": k, "severity": v[0], "blocksFinalization": v[1], "category": v[2], "description": v[3]} for k, v in catalog.WARNING_CODES.items()]}),
        os.path.join("..", "config", "plausibility-rules.json"): dumps({"rulesVersion": SUITE_VERSION, "rules": catalog.PLAUSIBILITY_RULES}),
    }
    files.update(cfg)
    return cases, results, files, groups


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="verify fixtures on disk are up to date; write nothing")
    args = ap.parse_args()
    cases, results, files, groups = build()
    if args.check:
        stale = []
        for rel, content in files.items():
            p = os.path.normpath(os.path.join(FIX, rel))
            if not os.path.exists(p):
                stale.append(rel)
                continue
            with open(p, encoding="utf-8") as f:
                stored = f.read()
            if rel.endswith("expected.json"):
                try:
                    matches = expected_equal(json.loads(stored), json.loads(content))
                except (ValueError, KeyError, TypeError, AttributeError):
                    matches = False
            else:
                matches = stored == content
            if not matches:
                stale.append(rel)
        if stale:
            print(f"{len(stale)} fixture file(s) are stale, e.g. {stale[:5]}. Run generate.py.")
            sys.exit(1)
        print(f"OK: {len(files)} files up to date (expected quantities within declared tolerances; other content exact).")
        return
    for rel, content in files.items():
        p = os.path.normpath(os.path.join(FIX, rel))
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
    hand = sum(len(c.hand) for c in cases)
    by_set = {}
    for c in cases:
        by_set[c.kind] = by_set.get(c.kind, 0) + 1
    print(f"Wrote {len(cases)} cases ({', '.join(f'{k}: {v}' for k, v in sorted(by_set.items()))}); "
          f"{hand} hand checks verified; {len(groups)} equivalence groups verified.")


if __name__ == "__main__":
    main()
