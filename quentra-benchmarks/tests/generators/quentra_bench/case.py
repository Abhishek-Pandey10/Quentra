"""Case definition, hand-check verification and README rendering."""
import math
from dataclasses import dataclass, field

from . import catalog
from .evaluator import evaluate, n


@dataclass
class Hand:
    """An independent hand calculation that the evaluator result must reproduce.

    path   : where the value lives in expected.json (see resolve()).
    value  : the number (or string/bool) worked out by hand, *not* by the evaluator.
    working: the arithmetic, shown in the README.
    """
    path: str
    value: object
    working: str = ""


@dataclass
class Case:
    id: str
    name: str
    category: str            # frame, slab, wall, material, units, reinforcement, source, required, aggregation, double-counting, invalid, naming, building
    kind: str                # synthetic | invalid | buildings
    purpose: str
    truth: list
    snapshot: dict
    options: dict = field(default_factory=dict)
    hand: list = field(default_factory=list)
    notes: list = field(default_factory=list)
    tags: list = field(default_factory=list)
    equivalence_group: str = None
    expected_behaviour: str = ""   # include / exclude / warn / reject / skip / block finalization
    expected_status: str = "pass"


def resolve(result, path):
    cur = result
    parts = path.split(".")
    i = 0
    while i < len(parts):
        p = parts[i]
        if isinstance(cur, dict) and p == "elements" and "elements" in cur and cur is result:
            key = parts[i + 1]
            eid, occ = (key.split("#") + ["0"])[:2]
            matches = [e for e in cur["elements"] if e["id"] == eid and e["occurrence"] == int(occ)]
            if not matches:
                raise KeyError(f"element {key} not found")
            cur = matches[0]
            i += 2
            continue
        if isinstance(cur, list):
            cur = cur[int(p)]
        else:
            cur = cur[p]
        i += 1
    return cur


def verify_hand(case, result):
    for h in case.hand:
        got = resolve(result, h.path)
        if isinstance(h.value, float) or (isinstance(h.value, int) and not isinstance(h.value, bool) and isinstance(got, float)):
            if got is None or not math.isclose(got, h.value, rel_tol=1e-9, abs_tol=1e-9):
                raise AssertionError(f"{case.id}: hand check {h.path}: evaluator={got} hand={h.value}")
        elif got != h.value:
            raise AssertionError(f"{case.id}: hand check {h.path}: evaluator={got!r} hand={h.value!r}")


def _fmt_q(v, unit=""):
    if v is None:
        return "unknown (null)"
    return f"{n(v)}{(' ' + unit) if unit else ''}"


def render_readme(case, result, ev):
    s = case.snapshot
    L = []
    L.append(f"# {case.id} — {case.name}")
    L.append("")
    prov = s.get("provenance") or {}
    L.append(f"> **Provenance:** `{prov.get('type')}` · `etabs_verified: {str(prov.get('etabs_verified')).lower()}` — "
             "this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.")
    L.append("")
    L.append(f"**Category:** {case.category} · **Fixture set:** {case.kind}" + (f" · **Equivalence group:** `{case.equivalence_group}`" if case.equivalence_group else ""))
    L.append("")
    L.append("## Purpose")
    L.append("")
    L.append(case.purpose)
    L.append("")
    if case.expected_behaviour:
        L.append(f"**Expected Quentra behaviour:** {case.expected_behaviour}")
        L.append("")
    L.append("## A. Structural truth")
    L.append("")
    for t in case.truth:
        L.append(f"- {t}")
    L.append("")
    unit = ((s.get("model") or {}).get("units") or {}).get("length")
    L.append("## B. ETABS-like input (`snapshot.json`)")
    L.append("")
    L.append(f"- Length unit in the snapshot: `{unit}`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.")
    L.append(f"- Elements: {len(s.get('elements', []))}, materials: {len(s.get('materials', []))}, sections: {len(s.get('sections', []))}, design results: {len(s.get('designResults', []))}.")
    if case.options:
        L.append(f"- Run options (from `manifest.json`): `{_compact(case.options)}`")
    L.append("")
    L.append("## C. Expected Quentra output — calculation")
    L.append("")
    if ev.trace:
        for t in ev.trace:
            L.append(f"- {t}")
        L.append("")
    big = len(result.get("elements", [])) > 40
    if big:
        L.append(f"_This model has {len(result['elements'])} elements; the per-element working is shown for the first element of each type/section combination. Every element follows the same formulas; totals are cross-checked against the closed-form hand check below._")
        L.append("")
    shown = set()
    for idx, e in enumerate(result.get("elements", [])):
        key = f"{e['id']}#{e['occurrence']}"
        if big:
            sig = (e["type"], ev.recs[idx]["_e"].get("section"), e["status"], e.get("reinforcementSource"))
            if sig in shown:
                continue
            shown.add(sig)
        occ = f" (copy {e['occurrence'] + 1})" if e["occurrence"] else ""
        L.append(f"### {e['type']} `{e['id']}`{occ}")
        L.append("")
        L.append("```text")
        for line in ev.el_trace.get(key, []):
            L.append(line)
        L.append("```")
        L.append("")
    if case.hand:
        L.append("## Independent hand check")
        L.append("")
        L.append("These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).")
        L.append("")
        L.append("| Quantity | Working | Value |")
        L.append("|---|---|---|")
        for h in case.hand:
            val = h.value if not isinstance(h.value, float) else n(h.value)
            L.append(f"| `{h.path}` | {h.working.replace('|', '/')} | {val} |")
        L.append("")
    L.append("## Expected totals")
    L.append("")
    if result["snapshotStatus"] == "Rejected":
        L.append("Snapshot status: **Rejected** — no quantities are produced (all totals `null`).")
    else:
        mt = result["modelTotals"]
        L.append("| Total | Value |")
        L.append("|---|---|")
        L.append(f"| Gross concrete | {_fmt_q(mt['grossConcreteM3'], 'm³')} |")
        L.append(f"| Opening deduction | {_fmt_q(mt['openingDeductionM3'], 'm³')} |")
        L.append(f"| Net concrete | {_fmt_q(mt['netConcreteM3'], 'm³')} |")
        if mt["unclassifiedVolumeM3"]:
            L.append(f"| Unclassified volume (not concrete) | {_fmt_q(mt['unclassifiedVolumeM3'], 'm³')} |")
        L.append(f"| Steel (known) | {_fmt_q(mt['steelKg'], 'kg')} |")
        if mt["steelKg"] is not None:
            for k, v in mt["steelKgBySource"].items():
                if v:
                    L.append(f"| &nbsp;&nbsp;of which {k} | {_fmt_q(v, 'kg')} |")
        c = result["completeness"]
        L.append(f"| Concrete completeness | {c['concrete']}" + (f" (unknown: {', '.join(c['concreteUnknownElementIds'])})" if c['concreteUnknownElementIds'] else "") + " |")
        L.append(f"| Steel completeness | {c['steel']}" + (f" (unknown: {', '.join(c['steelUnknownElementIds'])})" if c['steelUnknownElementIds'] else "") + " |")
        L.append("")
        stories = [(k, v) for k, v in result["byStory"].items()]
        if len(stories) > 1:
            L.append("**By story** (Σ equals the model total):")
            L.append("")
            L.append("| Story | Gross m³ | Net m³ | Steel kg |")
            L.append("|---|---|---|---|")
            for k, v in stories:
                L.append(f"| {k} | {n(v['grossConcreteM3'])} | {n(v['netConcreteM3'])} | {_fmt_q(v['steelKg'])} |")
            L.append("")
        if len(result["byMaterial"]) > 1:
            L.append("**By concrete material group:** " + ", ".join(f"`{k}` = {n(v['netConcreteM3'])} m³ net" for k, v in result["byMaterial"].items()))
            L.append("")
        if len(result["steelByMaterial"]) > 1:
            L.append("**By steel material:** " + ", ".join(f"`{k}` = {n(v)} kg" for k, v in result["steelByMaterial"].items()))
            L.append("")
    L.append("## Expected warnings")
    L.append("")
    if not result["warnings"]:
        L.append("None. Quentra must emit **no** warnings for this case (an extra warning is a failure).")
    else:
        L.append("| Code | Severity | Subject | Blocks finalization | Meaning |")
        L.append("|---|---|---|---|---|")
        for w in result["warnings"]:
            sev, blk, _, desc = catalog.WARNING_CODES[w["code"]]
            L.append(f"| `{w['code']}` | {sev} | `{w['subject']}` | {'yes' if blk else 'no'} | {desc} |")
    L.append("")
    fz = result["finalization"]
    L.append(f"**Finalization:** {'allowed' if fz['allowed'] else 'BLOCKED by ' + ', '.join(fz['blockingCodes'])}")
    L.append("")
    if case.notes:
        L.append("## Notes and assumptions")
        L.append("")
        for t in case.notes:
            L.append(f"- {t}")
        L.append("")
    L.append("See `docs/CONVENTIONS.md` for every detailing and counting convention used above.")
    L.append("")
    return "\n".join(L)


def _compact(o):
    import json
    return json.dumps(o, separators=(",", ":"))


def run_case(case):
    result, ev = evaluate(case.snapshot, case.options)
    verify_hand(case, result)
    result = dict(result)
    result = {"caseId": case.id, "schemaVersion": "1.0", "provenanceType": (case.snapshot.get("provenance") or {}).get("type"), **result}
    return result, ev
