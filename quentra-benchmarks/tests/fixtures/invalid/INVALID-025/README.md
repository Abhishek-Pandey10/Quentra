# INVALID-025 — Empty model

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

No elements at all. Totals are zero (legitimately known), but finalizing an empty take-off is blocked.

**Expected Quentra behaviour:** warn, block finalization

## A. Structural truth

- no elements

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 0, materials: 2, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` |  | 0 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 0 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 0 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `EMPTY_MODEL` | Warning | `None` | yes | The snapshot contains no elements. |

**Finalization:** BLOCKED by EMPTY_MODEL

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
