# WALL-004 — Wall with overlapping openings

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** wall · **Fixture set:** synthetic

## Purpose

Two overlapping wall openings: deduct the union.

**Expected Quentra behaviour:** include, deduct union, info warning

## A. Structural truth

- W1: 6 × 3 m, t = 0.20
- O1: u 1–3, v 0.5–2 (3 m²)
- O2: u 2–4, v 1–2.5 (3 m²)
- overlap u 2–3, v 1–2 (1 m²)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 18 m2 (shoelace on local u-v coordinates)
Opening O1: area 3 m2, fully inside the host
Opening O2: area 3 m2, fully inside the host
Sum of opening areas = 6 m2; area of (union of openings) inside host = 5 m2 - openings overlap, the union is deducted
Thickness t = 0.2 m (W200)
Gross volume = 18 x 0.2 = 3.6 m3
Net volume = (18 - 5) x 0.2 = 2.6 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.W1.openingDeductedAreaM2` | 3 + 3 − 1 | 5 |
| `modelTotals.netConcreteM3` | (18 − 5) × 0.2 = 2.6 | 2.6 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3.6 m³ |
| Opening deduction | 1 m³ |
| Net concrete | 2.6 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `OPENING_OVERLAP` | Info | `W1` | no | Openings in one host overlap each other. Their union is deducted, not their sum. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
