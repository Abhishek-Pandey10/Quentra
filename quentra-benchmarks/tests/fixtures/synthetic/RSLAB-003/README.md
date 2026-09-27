# RSLAB-003 — Slab with different X/Y spacing

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Main steel T12@150 in X, distribution T10@250 in Y. Provided-reinforcement benchmark (drawn bars), not ETABS design output.

**Expected Quentra behaviour:** include

## A. Structural truth

- S1: 6 × 5 m
- X: T12@150
- Y: T10@250

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 30 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S)
Gross volume = 30 x 0.2 = 6 m3
Net volume = (30 - 0) x 0.2 = 6 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along X, d = 12 mm @ 0.15 m, host rectangle: span along = 6 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33; bar length = 6 - 2 x 0.025 = 5.95 m
    total length = 1 x 33 x 5.95 = 196.35 m; mass = 196.35 x 0.000113097 x 7850 = 174.322295 kg
[1] Mesh (bottom): bars along Y, d = 10 mm @ 0.25 m, host rectangle: span along = 5 m, span across = 6 m, faces = 1
    bars N = floor((6 - 2 x 0.05) / 0.25) + 1 = floor(23.6) + 1 = 24; bar length = 5 - 2 x 0.025 = 4.95 m
    total length = 1 x 24 x 4.95 = 118.8 m; mass = 118.8 x 7.853982e-05 x 7850 = 73.244662 kg
Element steel = 174.322295 + 73.244662 = 247.566957 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.steelComponentsKg.0` | N = floor(4.9/0.15) + 1 = 33; length 5.95 m | 174.322295 |
| `elements.S1.steelComponentsKg.1` | N = floor(5.9/0.25) + 1 = 24; length 4.95 m | 73.244662 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 6 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 6 m³ |
| Steel (known) | 247.566957 kg |
| &nbsp;&nbsp;of which ProvidedBars | 247.566957 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
