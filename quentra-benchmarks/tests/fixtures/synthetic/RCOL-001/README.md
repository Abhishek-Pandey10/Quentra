# RCOL-001 — Column 4 bars + ties

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Corner bars only. Longitudinal and tie steel are reported as separate components.

**Expected Quentra behaviour:** include

## A. Structural truth

- C1: 0.40 × 0.40 × 3.0 m
- 4T16 full height
- Ties T8@200, cover 40 mm, 50 mm end offset, hook allowance 0.16 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3 = 0.48 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (longitudinal): 4 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 3 m (member length)
    V = 4 x 0.000201062 x 3 = 0.002412743 m3; mass = V x rho = 18.940034 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3 m, end offset 0.05 m
    count N = floor((3 - 2 x 0.05) / 0.2) + 1 = floor(14.5) + 1 = 15
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 15 x 1 x 1.408 = 21.12 m; A_bar = 5.026548e-05 m2; mass = 21.12 x 5.026548e-05 x 7850 = 8.333615 kg
Element steel = 18.940034 + 8.333615 = 27.273649 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.C1.steelComponentsKg.0` | 4 × A(16) × 3.0 × 7850 = 18.940 kg | 18.940034 |
| `elements.C1.steelComponentsKg.1` | N = floor(2.9/0.2) + 1 = 15; centre-line 0.4 − 0.08 − 0.008 = 0.312; piece = 4 × 0.312 + 0.16 = 1.408 m | 8.333615 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 0.48 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 0.48 m³ |
| Steel (known) | 27.273649 kg |
| &nbsp;&nbsp;of which ProvidedBars | 27.273649 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
