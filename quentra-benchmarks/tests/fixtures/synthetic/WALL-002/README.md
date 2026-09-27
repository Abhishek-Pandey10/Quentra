# WALL-002 — Wall with door opening

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** wall · **Fixture set:** synthetic

## Purpose

A door opening whose sill coincides with the wall base. Touching the host edge is not 'outside': no clipping warning.

**Expected Quentra behaviour:** include

## A. Structural truth

- W1: 5 × 3 m, t = 0.20
- D1: 1.0 m wide × 2.1 m high at u 1–2, from the base

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 15 m2 (shoelace on local u-v coordinates)
Opening D1: area 2.1 m2, fully inside the host
Sum of opening areas = 2.1 m2; area of (union of openings) inside host = 2.1 m2
Thickness t = 0.2 m (W200)
Gross volume = 15 x 0.2 = 3 m3
Net volume = (15 - 2.1) x 0.2 = 2.58 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.W1.openingDeductedAreaM2` | 1.0 × 2.1 | 2.1 |
| `modelTotals.grossConcreteM3` | 15 × 0.2 | 3 |
| `modelTotals.netConcreteM3` | (15 − 2.1) × 0.2 | 2.58 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3 m³ |
| Opening deduction | 0.42 m³ |
| Net concrete | 2.58 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
