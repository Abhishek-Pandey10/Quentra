# RCOL-003 — Column 12 bars + double hoops

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

12 bars with an outer hoop and two inner hoops per level (setsPerLocation = 2).

**Expected Quentra behaviour:** include

## A. Structural truth

- C1: 0.60 × 0.60 × 3.0 m
- 12T25
- Outer hoop T10@150 (cover 40)
- 2 inner hoops T10@150, centre-line 0.26 × 0.26 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C600: rectangle b x h = 0.6 m x 0.6 m, A = 0.36 m2
Gross volume = A x L = 0.36 x 3 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 12 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 3 m (member length)
    V = 12 x 0.000490874 x 3 = 0.017671459 m3; mass = V x rho = 138.720951 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3 m, end offset 0.05 m
    count N = floor((3 - 2 x 0.05) / 0.15) + 1 = floor(19.333333) + 1 = 20
    centreline w = b - 2c - d = 0.6 - 2 x 0.04 - 0.01 = 0.51 m; h = 0.6 - 2 x 0.04 - 0.01 = 0.51 m; piece = 2 x (w + h) + hook = 2 x (0.51 + 0.51) + 0.2 = 2.24 m
    total length = 20 x 1 x 2.24 = 44.8 m; A_bar = 7.853982e-05 m2; mass = 44.8 x 7.853982e-05 x 7850 = 27.620883 kg
[2] Hoops (inner-diamond-equivalent): d = 10 mm @ 0.15 m over zone 3 m, end offset 0.05 m
    count N = floor((3 - 2 x 0.05) / 0.15) + 1 = floor(19.333333) + 1 = 20
    2 x (w + h) + hook = 2 x (0.26 + 0.26) + 0.2 = 1.24 m (centreline dims given)
    total length = 20 x 2 x 1.24 = 49.6 m; A_bar = 7.853982e-05 m2; mass = 49.6 x 7.853982e-05 x 7850 = 30.580263 kg
Element steel = 138.720951 + 27.620883 + 30.580263 = 196.922096 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.C1.steelComponentsKg.0` | 12 × A(25) × 3 × 7850 = 138.72 kg | 138.720951 |
| `elements.C1.steelComponentsKg.2` | 20 levels × 2 sets × (2 × (0.26 + 0.26) + 0.2) m | 30.580263 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 196.922096 kg |
| &nbsp;&nbsp;of which ProvidedBars | 196.922096 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
