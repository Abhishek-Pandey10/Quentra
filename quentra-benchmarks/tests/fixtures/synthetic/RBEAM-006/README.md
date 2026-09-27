# RBEAM-006 — Additional support bars

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Provided longitudinal bars on a 300 × 600 × 6000 mm beam. Steel volume = n × πd²/4 × L; mass = volume × 7850 kg/m³.

**Expected Quentra behaviour:** include, exact steel

## A. Structural truth

- B1: 0.30 × 0.60 m, L = 6.00 m, C30
- Bars: 3T20 bottom L = 6.0 m; 2T16 top L = 6.0 m; 2T20 support-top-left L = 2.0 m; 2T20 support-top-right L = 2.0 m
- A(d) = π d² / 4; A(16) = 2.010619e-4 m², A(20) = 3.141593e-4 m², A(25) = 4.908739e-4 m²

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 2 x 0.000201062 x 6 = 0.002412743 m3; mass = V x rho = 18.940034 kg
[2] StraightBars (support-top-left): 2 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 2 m (given)
    V = 2 x 0.000314159 x 2 = 0.001256637 m3; mass = V x rho = 9.864601 kg
[3] StraightBars (support-top-right): 2 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 2 m (given)
    V = 2 x 0.000314159 x 2 = 0.001256637 m3; mass = V x rho = 9.864601 kg
Element steel = 44.390704 + 18.940034 + 9.864601 + 9.864601 = 83.05994 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelComponentsKg.0` | bottom 3T20 × 6 | 44.390704 |
| `elements.B1.steelComponentsKg.1` | top 2T16 × 6 | 18.940034 |
| `elements.B1.steelComponentsKg.2` | left support 2T20 × 2.0 | 9.864601 |
| `elements.B1.steelComponentsKg.3` | right support 2T20 × 2.0 | 9.864601 |
| `modelTotals.steelKg` | bottom 3T20 × 6 + top 2T16 × 6 + left support 2T20 × 2.0 + right support 2T20 × 2.0 | 83.05994 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 83.05994 kg |
| &nbsp;&nbsp;of which ProvidedBars | 83.05994 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
