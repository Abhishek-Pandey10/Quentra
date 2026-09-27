# RCOL-005 — Column reinforcement varying by story

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Story heights 4.0 m and 3.2 m, reinforcement reduces up the building. Bar lengths include an explicit lap allowance.

**Expected Quentra behaviour:** include

## A. Structural truth

- C1@L1: 0.5 × 0.5 × 4.0 m, 8T25 × 5.0 m (incl. 1.0 m lap), ties T10@150
- C1@L2: 0.5 × 0.5 × 3.2 m, 8T20 × 4.0 m (incl. 0.8 m lap), ties T8@200

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Column `C1@L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 4^2) = 4 m
Section C500: rectangle b x h = 0.5 m x 0.5 m, A = 0.25 m2
Gross volume = A x L = 0.25 x 4 = 1 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (longitudinal+lap): 8 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 5 m (given)
    V = 8 x 0.000490874 x 5 = 0.019634954 m3; mass = V x rho = 154.13439 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 4 m, end offset 0.05 m
    count N = floor((4 - 2 x 0.05) / 0.15) + 1 = floor(26) + 1 = 27
    centreline w = b - 2c - d = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; h = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; piece = 2 x (w + h) + hook = 2 x (0.41 + 0.41) + 0.2 = 1.84 m
    total length = 27 x 1 x 1.84 = 49.68 m; A_bar = 7.853982e-05 m2; mass = 49.68 x 7.853982e-05 x 7850 = 30.629586 kg
Element steel = 154.13439 + 30.629586 = 184.763975 kg
Status: Included
```

### Column `C1@L2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C500: rectangle b x h = 0.5 m x 0.5 m, A = 0.25 m2
Gross volume = A x L = 0.25 x 3.2 = 0.8 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (longitudinal+lap): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 4 m (given)
    V = 8 x 0.000314159 x 4 = 0.010053096 m3; mass = V x rho = 78.916807 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.5 - 2 x 0.04 - 0.008 = 0.412 m; h = 0.5 - 2 x 0.04 - 0.008 = 0.412 m; piece = 2 x (w + h) + hook = 2 x (0.412 + 0.412) + 0.16 = 1.808 m
    total length = 16 x 1 x 1.808 = 28.928 m; A_bar = 5.026548e-05 m2; mass = 28.928 x 5.026548e-05 x 7850 = 11.414527 kg
Element steel = 78.916807 + 11.414527 = 90.331334 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byStory.L1.steelKg` | 8 × A(25) × 5.0 × 7850 + 27 × 1.84 × A(10) × 7850 (3.9/0.15 = 26 exactly → 27) | 184.763975 |
| `byStory.L2.steelKg` | 8 × A(20) × 4.0 × 7850 + 16 × 1.808 × A(8) × 7850 | 90.331334 |
| `modelTotals.steelKg` | sum | 275.09531 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.8 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.8 m³ |
| Steel (known) | 275.09531 kg |
| &nbsp;&nbsp;of which ProvidedBars | 275.09531 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| L1 | 1 | 1 | 184.763975 |
| L2 | 0.8 | 0.8 | 90.331334 |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
