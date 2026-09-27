# RSLAB-004 — Slab with additional support strips

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Bottom mesh plus top support bars over the two supports, defined by explicit rectangular regions. Provided-reinforcement benchmark (drawn bars), not ETABS design output.

**Expected Quentra behaviour:** include

## A. Structural truth

- S1: 6 × 5 m; bottom T12@200 both ways
- Top strips 1.25 m wide at x = 0 and x = 6, T12@150 running in X

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
[0] Mesh (bottom): bars along X, d = 12 mm @ 0.2 m, host rectangle: span along = 6 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.05) / 0.2) + 1 = floor(24.5) + 1 = 25; bar length = 6 - 2 x 0.025 = 5.95 m
    total length = 1 x 25 x 5.95 = 148.75 m; mass = 148.75 x 0.000113097 x 7850 = 132.062345 kg
[1] Mesh (bottom): bars along Y, d = 12 mm @ 0.2 m, host rectangle: span along = 5 m, span across = 6 m, faces = 1
    bars N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30; bar length = 5 - 2 x 0.025 = 4.95 m
    total length = 1 x 30 x 4.95 = 148.5 m; mass = 148.5 x 0.000113097 x 7850 = 131.840391 kg
[2] Mesh (top-support-left): bars along X, d = 12 mm @ 0.15 m, explicit region: span along = 1.25 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33; bar length = 1.25 - 2 x 0.025 = 1.2 m
    total length = 1 x 33 x 1.2 = 39.6 m; mass = 39.6 x 0.000113097 x 7850 = 35.157438 kg
[3] Mesh (top-support-right): bars along X, d = 12 mm @ 0.15 m, explicit region: span along = 1.25 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33; bar length = 1.25 - 2 x 0.025 = 1.2 m
    total length = 1 x 33 x 1.2 = 39.6 m; mass = 39.6 x 0.000113097 x 7850 = 35.157438 kg
Element steel = 132.062345 + 131.840391 + 35.157438 + 35.157438 = 334.217612 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.steelComponentsKg.2` | strip: N = floor((5 − 0.1)/0.15) + 1 = 33; length 1.25 − 0.05 = 1.20 m | 35.157438 |
| `modelTotals.steelKg` | bottom X + bottom Y + 2 strips | 334.217612 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 6 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 6 m³ |
| Steel (known) | 334.217612 kg |
| &nbsp;&nbsp;of which ProvidedBars | 334.217612 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
