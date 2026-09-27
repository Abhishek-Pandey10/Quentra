#!/usr/bin/env python3
"""Read-only adversarial review of the benchmark expected-value calculator.

Requires shapely and jsonschema in the Python environment. Mutates copies of
RREQ-003 in memory, never the supplied corpus. Prints JSON evidence and exits 1
if the reference calculator accepts unsafe inputs. This is an audit tool, not
the Quentra regression suite.
"""
import copy
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "quentra-benchmarks/tests/generators"))

from quentra_bench.evaluator import evaluate  # noqa: E402
from validate import validators  # noqa: E402


def main():
    base = json.loads((ROOT / "quentra-benchmarks/tests/fixtures/synthetic/RREQ-003/snapshot.json").read_text())
    validator = validators()["snapshot.schema.json"]
    rows = []
    for name in ("NotDesigned", "NegativeArea", "CapturedWithinSynthetic", "WrongElementResult"):
        snapshot = copy.deepcopy(base)
        result = snapshot["designResults"][0]
        if name == "NotDesigned":
            result["status"] = "NotDesigned"
        elif name == "NegativeArea":
            for station in result["stations"]:
                station.update(topRequiredArea=-.001, bottomRequiredArea=-.001, shearRequiredAreaPerLength=0)
        elif name == "CapturedWithinSynthetic":
            result["origin"] = "captured"
        else:
            result["elementId"] = "ANOTHER_MEMBER"
            snapshot["elements"][0]["reinforcement"]["components"][0]["designResultRef"] = "ANOTHER_MEMBER"
        evaluated, _ = evaluate(snapshot)
        element = next((e for e in evaluated["elements"] if e["id"] == "B1"), None)
        # The benchmark may reject structurally or semantically, but must not
        # count these results as trusted component quantities.
        rejected = evaluated["snapshotStatus"] == "Rejected" or element is None or element.get("steelKg") is None
        rows.append({"probe": name, "schemaValid": validator.is_valid(snapshot),
                     "referenceRejectedSteel": rejected,
                     "steelKg": element.get("steelKg") if element else None,
                     "finalization": evaluated["finalization"], "warnings": evaluated["warnings"]})
    print(json.dumps({"scope": "Benchmark oracle only; not the Quentra engine", "probes": rows}, indent=2))
    return 0 if all(row["referenceRejectedSteel"] for row in rows) else 1


if __name__ == "__main__":
    raise SystemExit(main())
