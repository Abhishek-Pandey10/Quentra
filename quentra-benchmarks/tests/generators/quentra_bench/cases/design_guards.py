"""Regression fixtures for the independent evaluator's design-evidence guards."""
from copy import deepcopy
from ..case import Case, Hand
from . import reinforcement


def all_cases():
    base = next(c for c in reinforcement.all_cases() if c.id == "RREQ-003")
    out = []
    names = ["Not designed", "Negative demand", "Captured result in synthetic snapshot",
             "Wrong member result", "Duplicate stations", "Negative station", "Duplicate result records",
             "Missing design-result origin"]
    for i, name in enumerate(names, 1):
        cid = f"DGUARD-{i:03d}"
        s = deepcopy(base.snapshot)
        s["model"]["name"] = name
        s["provenance"]["caseId"] = cid
        d = s["designResults"][0]
        if i == 1:
            d["status"] = "NotDesigned"
        elif i == 2:
            for station in d["stations"]:
                station.update(topRequiredArea=-.001, bottomRequiredArea=-.001, shearRequiredAreaPerLength=0)
        elif i == 3:
            d["origin"] = "captured"
        elif i == 4:
            d["elementId"] = "ANOTHER_MEMBER"
            s["elements"][0]["reinforcement"]["components"][0]["designResultRef"] = "ANOTHER_MEMBER"
        elif i == 5:
            d["stations"].insert(1, deepcopy(d["stations"][0]))
        elif i == 6:
            d["stations"][0]["station"] = -1
        elif i == 7:
            s["designResults"].append(deepcopy(d))
        else:
            del d["origin"]
        fatal = i in (3, 8)
        hand = [Hand("finalization.allowed", False, "Invalid design evidence cannot be finalized")]
        hand += [Hand("snapshotStatus", "Rejected", "Result provenance is inconsistent")] if fatal else [
            Hand("elements.B1.steelKg", None, "Untrusted demand is unknown, never zero or a negative mass"),
            Hand("modelTotals.netConcreteM3", 1.08, "0.3 × 0.6 × 6; invalid design evidence does not invalidate geometry")]
        out.append(Case(cid, name, "invalid", "invalid",
            "Regression for design-result validation; no live ETABS data.", [name], s, {}, hand,
            expected_behaviour="reject snapshot" if fatal else "steel unknown; block finalization"))
    return out
