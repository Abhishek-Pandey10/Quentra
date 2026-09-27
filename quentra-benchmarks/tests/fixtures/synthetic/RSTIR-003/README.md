# RSTIR-003 — Beam stirrups 3-leg (hoop + crosstie)

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Odd leg count: closed hoop plus one straight crosstie at every stirrup location.

**Expected Quentra behaviour:** include

## A. Structural truth

- Hoop as RSTIR-001
- Crosstie T10, piece length 0.54 + 0.20 hooks = 0.74 m, same spacing

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
[0] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
[1] Crossties (crossties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    piece length 0.74 m (given)
    total length = 40 x 1 x 0.74 = 29.6 m; A_bar = 7.853982e-05 m2; mass = 29.6 x 7.853982e-05 x 7850 = 18.249512 kg
Element steel = 43.404244 + 18.249512 = 61.653756 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelComponentsKg.1` | 40 × 0.74 = 29.6 m × A(10) × 7850 | 18.249512 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 61.653756 kg |
| &nbsp;&nbsp;of which ProvidedBars | 61.653756 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
