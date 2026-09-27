# MAT-002 — Multiple steel grades

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** material · **Fixture set:** synthetic

## Purpose

Identical reinforcement in two steel grades. Steel mass is equal, but must be reported per grade.

**Expected Quentra behaviour:** group steel by grade

## A. Structural truth

- B1: 4T20 × 6 m, grade B500
- B2: 4T20 × 6 m, grade Fe415
- both 7850 kg/m³

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 3, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.
- Rebar material Fe415: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 4 x 0.000314159 x 6 = 0.007539822 m3; mass = V x rho = 59.187606 kg
Element steel = 59.187606 kg
Status: Included
```

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 4 x 0.000314159 x 6 = 0.007539822 m3; mass = V x rho = 59.187606 kg
Element steel = 59.187606 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `steelByMaterial.B500` | 4 × π × 0.020²/4 × 6 × 7850 = 59.1876 | 59.187606 |
| `steelByMaterial.Fe415` | same | 59.187606 |
| `modelTotals.steelKg` | 2 × 59.1876 | 118.375211 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 2.16 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 2.16 m³ |
| Steel (known) | 118.375211 kg |
| &nbsp;&nbsp;of which ProvidedBars | 118.375211 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

**By steel material:** `B500` = 59.187606 kg, `Fe415` = 59.187606 kg

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
