# RCOL-002 — Column 8 bars + ties + crossties

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

8 bars (corners + mid-faces) restrained by a perimeter hoop and two crossties per level.

**Expected Quentra behaviour:** include

## A. Structural truth

- C1: 0.50 × 0.50 × 3.0 m
- 8T20
- Hoop T10@150, cover 40 mm
- 2 crossties T10 per level, piece 0.41 + 0.2 = 0.61 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C500: rectangle b x h = 0.5 m x 0.5 m, A = 0.25 m2
Gross volume = A x L = 0.25 x 3 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3 m (member length)
    V = 8 x 0.000314159 x 3 = 0.007539822 m3; mass = V x rho = 59.187606 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3 m, end offset 0.05 m
    count N = floor((3 - 2 x 0.05) / 0.15) + 1 = floor(19.333333) + 1 = 20
    centreline w = b - 2c - d = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; h = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; piece = 2 x (w + h) + hook = 2 x (0.41 + 0.41) + 0.2 = 1.84 m
    total length = 20 x 1 x 1.84 = 36.8 m; A_bar = 7.853982e-05 m2; mass = 36.8 x 7.853982e-05 x 7850 = 22.688582 kg
[2] Crossties (crossties): d = 10 mm @ 0.15 m over zone 3 m, end offset 0.05 m
    count N = floor((3 - 2 x 0.05) / 0.15) + 1 = floor(19.333333) + 1 = 20
    piece length 0.61 m (given)
    total length = 20 x 2 x 0.61 = 24.4 m; A_bar = 7.853982e-05 m2; mass = 24.4 x 7.853982e-05 x 7850 = 15.043516 kg
Element steel = 59.187606 + 22.688582 + 15.043516 = 96.919704 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.C1.steelComponentsKg.0` | 8 × A(20) × 3 × 7850 = 59.188 kg | 59.187606 |
| `elements.C1.steelComponentsKg.1` | N = 20; piece 4 × 0.41 + 0.2 = 1.84 m | 22.688582 |
| `elements.C1.steelComponentsKg.2` | 20 × 2 × 0.61 m × A(10) × 7850 | 15.043516 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 0.75 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 0.75 m³ |
| Steel (known) | 96.919704 kg |
| &nbsp;&nbsp;of which ProvidedBars | 96.919704 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
