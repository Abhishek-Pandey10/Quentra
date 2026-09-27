# RCOL-004 — Circular column with circular hoops

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Circular hoops: piece length = π × centre-line diameter + hook allowance.

**Expected Quentra behaviour:** include

## A. Structural truth

- C1: D = 0.60 m, L = 3.5 m
- 8T20
- Circular hoops T10@150, cover 40 mm

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C600D: circle D = 0.6 m, A = pi x D^2 / 4 = 0.282743 m2
Gross volume = A x L = 0.282743 x 3.5 = 0.989602 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline Dc = D - 2c - d = 0.6 - 2 x 0.04 - 0.01 = 0.51 m; piece = pi x Dc + hook = 1.802212 m
    total length = 23 x 1 x 1.802212 = 41.450882 m; A_bar = 7.853982e-05 m2; mass = 41.450882 x 7.853982e-05 x 7850 = 25.556025 kg
Element steel = 69.052207 + 25.556025 = 94.608232 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.C1.steelComponentsKg.1` | N = floor(3.4/0.15) + 1 = 23; Dc = 0.6 − 0.08 − 0.01 = 0.51; piece = π × 0.51 + 0.2 = 1.8022 m | 25.556025 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 0.989602 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 0.989602 m³ |
| Steel (known) | 94.608232 kg |
| &nbsp;&nbsp;of which ProvidedBars | 94.608232 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
