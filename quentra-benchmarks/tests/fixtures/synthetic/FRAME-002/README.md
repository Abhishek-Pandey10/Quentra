# FRAME-002 — Single rectangular column

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** frame · **Fixture set:** synthetic

## Purpose

One vertical rectangular column; length is the story height.

**Expected Quentra behaviour:** include

## A. Structural truth

- Column C1: 0.40 m × 0.50 m, from z = 0 to z = 3 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C400x500: rectangle b x h = 0.4 m x 0.5 m, A = 0.2 m2
Gross volume = A x L = 0.2 x 3 = 0.6 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 0.40 × 0.50 × 3.00 = 0.60 | 0.6 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 0.6 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 0.6 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
