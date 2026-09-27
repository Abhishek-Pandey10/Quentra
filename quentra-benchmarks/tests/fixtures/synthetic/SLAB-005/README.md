# SLAB-005 — Slab with two separate openings

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

Two disjoint openings.

**Expected Quentra behaviour:** include

## A. Structural truth

- S1: 10 × 8 m, t = 0.20 m
- O1: 2 × 2 m; O2: 1 × 3 m; disjoint

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 80 m2 (shoelace on local u-v coordinates)
Opening O1: area 4 m2, fully inside the host
Opening O2: area 3 m2, fully inside the host
Sum of opening areas = 7 m2; area of (union of openings) inside host = 7 m2
Thickness t = 0.2 m (S200)
Gross volume = 80 x 0.2 = 16 m3
Net volume = (80 - 7) x 0.2 = 14.6 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.openingAreaSumM2` | 4 + 3 | 7 |
| `elements.S1.openingDeductedAreaM2` | disjoint → union = sum | 7 |
| `modelTotals.netConcreteM3` | (80 − 7) × 0.2 = 14.6 | 14.6 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 16 m³ |
| Opening deduction | 1.4 m³ |
| Net concrete | 14.6 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
