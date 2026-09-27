# DGUARD-008 — Missing design-result origin

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

Regression for design-result validation; no live ETABS data.

**Expected Quentra behaviour:** reject snapshot

## A. Structural truth

- Missing design-result origin

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 1.

## C. Expected Quentra output — calculation

- Snapshot rejected by fatal checks - no quantities are produced.

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `finalization.allowed` | Invalid design evidence cannot be finalized | False |
| `snapshotStatus` | Result provenance is inconsistent | Rejected |

## Expected totals

Snapshot status: **Rejected** — no quantities are produced (all totals `null`).
## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `PROVENANCE_INCONSISTENT` | Fatal | `None` | yes | Provenance mixes synthetic and ETABS-verified categories (e.g. synthetic with etabs_verified=true, or a capture without an ETABS version). |

**Finalization:** BLOCKED by PROVENANCE_INCONSISTENT

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
