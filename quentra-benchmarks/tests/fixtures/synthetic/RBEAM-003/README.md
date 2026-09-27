# RBEAM-003 — Beam longitudinal 6T25

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Provided longitudinal bars on a 300 × 600 × 6000 mm beam. Steel volume = n × πd²/4 × L; mass = volume × 7850 kg/m³.

**Expected Quentra behaviour:** include, exact steel

## A. Structural truth

- B1: 0.30 × 0.60 m, L = 6.00 m, C30
- Bars: 6T25 bottom L = 6.0 m
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
[0] StraightBars (bottom): 6 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 6 m (member length)
    V = 6 x 0.000490874 x 6 = 0.017671459 m3; mass = V x rho = 138.720951 kg
Element steel = 138.720951 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelComponentsKg.0` | 6 × A(25) × 6 × 7850 | 138.720951 |
| `modelTotals.steelKg` | 6 × A(25) × 6 × 7850 | 138.720951 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 138.720951 kg |
| &nbsp;&nbsp;of which ProvidedBars | 138.720951 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
