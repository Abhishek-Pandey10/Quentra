# SLAB-003 — Slab with one rectangular opening

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

One rectangular opening fully inside the slab.

**Expected Quentra behaviour:** include, deduct opening

## A. Structural truth

- S1: 8 × 6 m, t = 0.25 m
- O1: 2.0 × 1.5 m at x 3–5, y 2–3.5

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 48 m2 (shoelace on local u-v coordinates)
Opening O1: area 3 m2, fully inside the host
Sum of opening areas = 3 m2; area of (union of openings) inside host = 3 m2
Thickness t = 0.25 m (S250)
Gross volume = 48 x 0.25 = 12 m3
Net volume = (48 - 3) x 0.25 = 11.25 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.openingDeductedAreaM2` | 2 × 1.5 | 3 |
| `elements.S1.netAreaM2` | 48 − 3 | 45 |
| `modelTotals.grossConcreteM3` | 48 × 0.25 | 12 |
| `modelTotals.netConcreteM3` | 45 × 0.25 | 11.25 |
| `modelTotals.openingDeductionM3` | 3 × 0.25 | 0.75 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 12 m³ |
| Opening deduction | 0.75 m³ |
| Net concrete | 11.25 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
