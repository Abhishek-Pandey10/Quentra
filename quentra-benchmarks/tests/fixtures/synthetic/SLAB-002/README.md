# SLAB-002 — Irregular polygon slab

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

L-shaped (non-convex) slab. Area must come from the polygon, not its bounding box.

**Expected Quentra behaviour:** include

## A. Structural truth

- S1: L-shape (0,0)-(8,0)-(8,4)-(4,4)-(4,7)-(0,7), t = 0.20 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (6 vertices) area in its own plane = 44 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 44 x 0.2 = 8.8 m3
Net volume = (44 - 0) x 0.2 = 8.8 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.grossAreaM2` | 8 × 4 + 4 × 3 = 44 (bounding box 56 would be wrong) | 44 |
| `modelTotals.netConcreteM3` | 44 × 0.2 = 8.8 | 8.8 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 8.8 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 8.8 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
