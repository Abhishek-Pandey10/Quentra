# FRAME-007 — Sloped beam

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** frame · **Fixture set:** synthetic

## Purpose

Sloped (raking) beam. Length is the true 3-D length, not the plan length.

**Expected Quentra behaviour:** include

## A. Structural truth

- RB1: 0.30 × 0.50 from (0,0,3) to (6,0,5.5): plan 6 m, rise 2.5 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Beam `RB1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 2.5^2) = 6.5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 6.5 = 0.975 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.RB1.lengthM` | √(6² + 2.5²) = √42.25 = 6.5 (plan length 6 would be wrong) | 6.5 |
| `modelTotals.netConcreteM3` | 0.15 × 6.5 = 0.975 | 0.975 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 0.975 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 0.975 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
