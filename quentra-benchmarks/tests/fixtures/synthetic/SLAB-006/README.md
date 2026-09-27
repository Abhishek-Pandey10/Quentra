# SLAB-006 — Slab with overlapping openings

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

Two openings overlap. Deducting the sum would remove the 1 m² overlap twice; the union must be deducted.

**Expected Quentra behaviour:** include, deduct union, info warning

## A. Structural truth

- S1: 10 × 8 m, t = 0.20 m
- O1: x 2–5, y 2–5 (9 m²)
- O2: x 4–7, y 4–6 (6 m²)
- Overlap: x 4–5, y 4–5 (1 m²)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 80 m2 (shoelace on local u-v coordinates)
Opening O1: area 9 m2, fully inside the host
Opening O2: area 6 m2, fully inside the host
Sum of opening areas = 15 m2; area of (union of openings) inside host = 14 m2 - openings overlap, the union is deducted
Thickness t = 0.2 m (S200)
Gross volume = 80 x 0.2 = 16 m3
Net volume = (80 - 14) x 0.2 = 13.2 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.openingAreaSumM2` | 9 + 6 | 15 |
| `elements.S1.openingDeductedAreaM2` | 9 + 6 − 1 (union) | 14 |
| `modelTotals.netConcreteM3` | (80 − 14) × 0.2 = 13.2 (sum would give 13.0) | 13.2 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 16 m³ |
| Opening deduction | 2.8 m³ |
| Net concrete | 13.2 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `OPENING_OVERLAP` | Info | `S1` | no | Openings in one host overlap each other. Their union is deducted, not their sum. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
