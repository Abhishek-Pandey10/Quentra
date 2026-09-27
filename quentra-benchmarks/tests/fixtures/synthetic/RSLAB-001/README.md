# RSLAB-001 — Slab bottom mesh T12@200 both ways

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

The prompt's reference slab: 5 × 4 m, bottom T12@200 in X and Y. Provided-reinforcement benchmark (drawn bars), not ETABS design output.

**Expected Quentra behaviour:** include, exact steel

## A. Structural truth

- S1: 5 × 4 m, t = 0.20
- Bottom X and Y: T12@200
- First bar 50 mm from the slab edge (edgeOffset); bar ends 25 mm from the edges (endCover)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 20 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S)
Gross volume = 20 x 0.2 = 4 m3
Net volume = (20 - 0) x 0.2 = 4 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along X, d = 12 mm @ 0.2 m, host rectangle: span along = 5 m, span across = 4 m, faces = 1
    bars N = floor((4 - 2 x 0.05) / 0.2) + 1 = floor(19.5) + 1 = 20; bar length = 5 - 2 x 0.025 = 4.95 m
    total length = 1 x 20 x 4.95 = 99 m; mass = 99 x 0.000113097 x 7850 = 87.893594 kg
[1] Mesh (bottom): bars along Y, d = 12 mm @ 0.2 m, host rectangle: span along = 4 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.05) / 0.2) + 1 = floor(24.5) + 1 = 25; bar length = 4 - 2 x 0.025 = 3.95 m
    total length = 1 x 25 x 3.95 = 98.75 m; mass = 98.75 x 0.000113097 x 7850 = 87.671641 kg
Element steel = 87.893594 + 87.671641 = 175.565235 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.steelComponentsKg.0` | X bars: N = floor((4 − 0.1)/0.2) + 1 = 20; length 5 − 0.05 = 4.95 m; 20 × 4.95 × A(12) × 7850 | 87.893594 |
| `elements.S1.steelComponentsKg.1` | Y bars: N = floor((5 − 0.1)/0.2) + 1 = 25; length 3.95 m | 87.671641 |
| `modelTotals.steelKg` | sum | 175.565235 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 4 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 4 m³ |
| Steel (known) | 175.565235 kg |
| &nbsp;&nbsp;of which ProvidedBars | 175.565235 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
