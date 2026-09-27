# PLAUS-003 — High steel rate

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** plausibility · **Fixture set:** synthetic

## Purpose

12T32 in a 300 × 300 column: ~841 kg/m³, above the 600 kg/m³ band but below steel density. Warn, keep the value.

**Expected Quentra behaviour:** include, warn

## A. Structural truth

- C1: 0.3 × 0.3 × 3 m (0.27 m³), 12T32

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C300: rectangle b x h = 0.3 m x 0.3 m, A = 0.09 m2
Gross volume = A x L = 0.09 x 3 = 0.27 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 12 bars, d = 32 mm, A_bar = pi x 0.032^2 / 4 = 0.000804248 m2; L = 3 m (member length)
    V = 12 x 0.000804248 x 3 = 0.028952918 m3; mass = V x rho = 227.280405 kg
Steel rate = 841.77928 kg/m3 > 600 kg/m3 -> HIGH_STEEL_RATE
Element steel = 227.280405 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.steelKg` | 12 × A(32) × 3 × 7850 = 227.3 kg → 841.8 kg/m³ | 227.280405 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 0.27 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 0.27 m³ |
| Steel (known) | 227.280405 kg |
| &nbsp;&nbsp;of which ProvidedBars | 227.280405 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `HIGH_STEEL_RATE` | Warning | `C1` | no | Element steel rate exceeds the configurable high-rate band. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
