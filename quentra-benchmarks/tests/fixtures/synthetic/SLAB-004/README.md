# SLAB-004 — Slab with circular-equivalent opening

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

A circular shaft opening. ETABS-style models describe circles as polygons, so the snapshot carries a 32-sided inscribed polygon and the expected deduction is the polygon area, not πr².

**Expected Quentra behaviour:** include, deduct polygon area

## A. Structural truth

- S1: 8 × 6 m, t = 0.20 m
- O1: 32-gon inscribed in a circle r = 0.60 m centred at (4, 3)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 48 m2 (shoelace on local u-v coordinates)
Opening O1: area 1.12372 m2, fully inside the host
Sum of opening areas = 1.12372 m2; area of (union of openings) inside host = 1.12372 m2
Thickness t = 0.2 m (S200)
Gross volume = 48 x 0.2 = 9.6 m3
Net volume = (48 - 1.12372) x 0.2 = 9.375256 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.openingDeductedAreaM2` | ½ × n × r² × sin(2π/n) = ½ × 32 × 0.36 × sin(π/16) = 1.123720 | 1.12372 |
| `modelTotals.netConcreteM3` | (48 − 1.123720) × 0.2 = 9.375256 | 9.375256 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 9.6 m³ |
| Opening deduction | 0.224744 m³ |
| Net concrete | 9.375256 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

## Notes and assumptions

- A true circle would deduct πr² = 1.130973 m² — 0.64 % more. Quentra must deduct the polygon it was given; it must not 'upgrade' polygons to circles.

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
