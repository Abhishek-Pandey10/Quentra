# RSLAB-005 — Slab with two bottom layers in X

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Two reinforcement layers in the same direction (second layer staggered 100 mm, i.e. larger edge offset). Provided-reinforcement benchmark (drawn bars), not ETABS design output.

**Expected Quentra behaviour:** include

## A. Structural truth

- S1: 6 × 5 × 0.25 m
- Layer 1: T16@200 X
- Layer 2: T12@200 X, first bar 150 mm from edge
- Y: T12@200

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 30 m2 (shoelace on local u-v coordinates)
Thickness t = 0.25 m (S)
Gross volume = 30 x 0.25 = 7.5 m3
Net volume = (30 - 0) x 0.25 = 7.5 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom-layer-1): bars along X, d = 16 mm @ 0.2 m, host rectangle: span along = 6 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.05) / 0.2) + 1 = floor(24.5) + 1 = 25; bar length = 6 - 2 x 0.025 = 5.95 m
    total length = 1 x 25 x 5.95 = 148.75 m; mass = 148.75 x 0.000201062 x 7850 = 234.777502 kg
[1] Mesh (bottom-layer-2): bars along X, d = 12 mm @ 0.2 m, host rectangle: span along = 6 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.15) / 0.2) + 1 = floor(23.5) + 1 = 24; bar length = 6 - 2 x 0.025 = 5.95 m
    total length = 1 x 24 x 5.95 = 142.8 m; mass = 142.8 x 0.000113097 x 7850 = 126.779851 kg
[2] Mesh (bottom): bars along Y, d = 12 mm @ 0.2 m, host rectangle: span along = 5 m, span across = 6 m, faces = 1
    bars N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30; bar length = 5 - 2 x 0.025 = 4.95 m
    total length = 1 x 30 x 4.95 = 148.5 m; mass = 148.5 x 0.000113097 x 7850 = 131.840391 kg
Element steel = 234.777502 + 126.779851 + 131.840391 = 493.397745 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.steelComponentsKg.1` | N = floor((5 − 0.3)/0.2) + 1 = 24 | 126.779851 |
| `modelTotals.steelKg` | sum of layers | 493.397745 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 7.5 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 7.5 m³ |
| Steel (known) | 493.397745 kg |
| &nbsp;&nbsp;of which ProvidedBars | 493.397745 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
