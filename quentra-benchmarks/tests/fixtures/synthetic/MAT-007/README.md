# MAT-007 — Default steel density

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** material · **Fixture set:** synthetic

## Purpose

Reinforcement with no steel material named. The configured default (7850 kg/m³) is used and the fact is recorded.

**Expected Quentra behaviour:** compute with default, info warning

## A. Structural truth

- B1: 4T20 × 6 m, no steelMaterial

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel density: no steel material named -> default 7850 kg/m3 (DEFAULT_STEEL_DENSITY_USED)
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
| `modelTotals.steelKg` | 4 × π × 0.02²/4 × 6 × 7850 = 59.1876 | 59.187606 |
| `steelByMaterial.<default>` |  | 59.187606 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 59.187606 kg |
| &nbsp;&nbsp;of which ProvidedBars | 59.187606 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `DEFAULT_STEEL_DENSITY_USED` | Info | `B1` | no | No steel material was named; the configured default density was used. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
