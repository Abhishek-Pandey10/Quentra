# RSTIR-004 — Beam stirrups with spacing zones

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Confinement zones at 100 mm near supports, 200 mm mid-span. Each zone is counted on its own; both counts are exact divisions and test the floor() tolerance.

**Expected Quentra behaviour:** include

## A. Structural truth

- End zones: 1.5 m each, T10@100, 50 mm offset → (1.5 − 0.1)/0.1 = 14 exactly → 15 stirrups
- Mid zone: 3.0 m, T10@200, 100 mm offset → (3.0 − 0.2)/0.2 = 14 exactly → 15 stirrups
- Zones do not share stirrups: the author chose offsets so that zone boundaries are not double-populated

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
[0] Hoops (end-zone-left): d = 10 mm @ 0.1 m over zone 1.5 m, end offset 0.05 m
    count N = floor((1.5 - 2 x 0.05) / 0.1) + 1 = floor(14) + 1 = 15
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 15 x 1 x 1.76 = 26.4 m; A_bar = 7.853982e-05 m2; mass = 26.4 x 7.853982e-05 x 7850 = 16.276592 kg
[1] Hoops (mid-zone): d = 10 mm @ 0.2 m over zone 3 m, end offset 0.1 m
    count N = floor((3 - 2 x 0.1) / 0.2) + 1 = floor(14) + 1 = 15
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 15 x 1 x 1.76 = 26.4 m; A_bar = 7.853982e-05 m2; mass = 26.4 x 7.853982e-05 x 7850 = 16.276592 kg
[2] Hoops (end-zone-right): d = 10 mm @ 0.1 m over zone 1.5 m, end offset 0.05 m
    count N = floor((1.5 - 2 x 0.05) / 0.1) + 1 = floor(14) + 1 = 15
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 15 x 1 x 1.76 = 26.4 m; A_bar = 7.853982e-05 m2; mass = 26.4 x 7.853982e-05 x 7850 = 16.276592 kg
Element steel = 16.276592 + 16.276592 + 16.276592 = 48.829775 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelComponentsKg.0` | 15 × 1.76 m × A(10) × 7850 | 16.276592 |
| `elements.B1.steelComponentsKg.1` | 15 × 1.76 m × A(10) × 7850 | 16.276592 |
| `modelTotals.steelKg` | 45 stirrups × 1.76 m × A(10) × 7850 | 48.829775 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 48.829775 kg |
| &nbsp;&nbsp;of which ProvidedBars | 48.829775 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

## Notes and assumptions

- In floating point (1.5 − 0.1)/0.1 = 13.999999999999998; without the 1e-9 guard the count would be 14 instead of 15.

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
