# RWALL-001 — Wall single-layer mesh

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Central layer of vertical and horizontal bars. Vertical bars are distributed along the wall length; horizontal bars up the height.

**Expected Quentra behaviour:** include

## A. Structural truth

- W1: 5 × 3 m, t = 0.20
- Vertical T12@200, horizontal T10@250, one layer

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 15 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (W)
Gross volume = 15 x 0.2 = 3 m3
Net volume = (15 - 0) x 0.2 = 3 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (vertical): bars along Vertical, d = 12 mm @ 0.2 m, host rectangle (wall-local): span along = 3 m, span across = 5 m, faces = 1
    bars N = floor((5 - 2 x 0.05) / 0.2) + 1 = floor(24.5) + 1 = 25; bar length = 3 - 2 x 0.025 = 2.95 m
    total length = 1 x 25 x 2.95 = 73.75 m; mass = 73.75 x 0.000113097 x 7850 = 65.476289 kg
[1] Mesh (horizontal): bars along Horizontal, d = 10 mm @ 0.25 m, host rectangle (wall-local): span along = 5 m, span across = 3 m, faces = 1
    bars N = floor((3 - 2 x 0.05) / 0.25) + 1 = floor(11.6) + 1 = 12; bar length = 5 - 2 x 0.025 = 4.95 m
    total length = 1 x 12 x 4.95 = 59.4 m; mass = 59.4 x 7.853982e-05 x 7850 = 36.622331 kg
Element steel = 65.476289 + 36.622331 = 102.09862 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.W1.steelComponentsKg.0` | vertical: N = floor((5 − 0.1)/0.2) + 1 = 25; length 3 − 0.05 = 2.95 m | 65.476289 |
| `elements.W1.steelComponentsKg.1` | horizontal: N = floor((3 − 0.1)/0.25) + 1 = 12; length 4.95 m | 36.622331 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 3 m³ |
| Steel (known) | 102.09862 kg |
| &nbsp;&nbsp;of which ProvidedBars | 102.09862 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
