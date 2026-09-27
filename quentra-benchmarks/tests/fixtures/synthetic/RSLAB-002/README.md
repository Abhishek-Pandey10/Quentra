# RSLAB-002 — Slab top and bottom mesh

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Adds a top mesh T10@200 both ways. Provided-reinforcement benchmark (drawn bars), not ETABS design output.

**Expected Quentra behaviour:** include

## A. Structural truth

- As RSLAB-001 plus top T10@200 X and Y

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
[2] Mesh (top): bars along X, d = 10 mm @ 0.2 m, host rectangle: span along = 5 m, span across = 4 m, faces = 1
    bars N = floor((4 - 2 x 0.05) / 0.2) + 1 = floor(19.5) + 1 = 20; bar length = 5 - 2 x 0.025 = 4.95 m
    total length = 1 x 20 x 4.95 = 99 m; mass = 99 x 7.853982e-05 x 7850 = 61.037218 kg
[3] Mesh (top): bars along Y, d = 10 mm @ 0.2 m, host rectangle: span along = 4 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.05) / 0.2) + 1 = floor(24.5) + 1 = 25; bar length = 4 - 2 x 0.025 = 3.95 m
    total length = 1 x 25 x 3.95 = 98.75 m; mass = 98.75 x 7.853982e-05 x 7850 = 60.883084 kg
Element steel = 87.893594 + 87.671641 + 61.037218 + 60.883084 = 297.485537 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.steelKg` | bottom (RSLAB-001) + top X + top Y | 297.485537 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 4 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 4 m³ |
| Steel (known) | 297.485537 kg |
| &nbsp;&nbsp;of which ProvidedBars | 297.485537 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
