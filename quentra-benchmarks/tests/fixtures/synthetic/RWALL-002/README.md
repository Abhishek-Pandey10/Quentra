# RWALL-002 — Wall two faces, different spacing

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Both faces reinforced; faces = 2 doubles each layer.

**Expected Quentra behaviour:** include

## A. Structural truth

- W1: 5 × 3 m, t = 0.25
- Vertical T12@150 each face; horizontal T10@200 each face

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 15 m2 (shoelace on local u-v coordinates)
Thickness t = 0.25 m (W)
Gross volume = 15 x 0.25 = 3.75 m3
Net volume = (15 - 0) x 0.25 = 3.75 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (vertical): bars along Vertical, d = 12 mm @ 0.15 m, host rectangle (wall-local): span along = 3 m, span across = 5 m, faces = 2
    bars N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33; bar length = 3 - 2 x 0.025 = 2.95 m
    total length = 2 x 33 x 2.95 = 194.7 m; mass = 194.7 x 0.000113097 x 7850 = 172.857402 kg
[1] Mesh (horizontal): bars along Horizontal, d = 10 mm @ 0.2 m, host rectangle (wall-local): span along = 5 m, span across = 3 m, faces = 2
    bars N = floor((3 - 2 x 0.05) / 0.2) + 1 = floor(14.5) + 1 = 15; bar length = 5 - 2 x 0.025 = 4.95 m
    total length = 2 x 15 x 4.95 = 148.5 m; mass = 148.5 x 7.853982e-05 x 7850 = 91.555827 kg
Element steel = 172.857402 + 91.555827 = 264.41323 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.W1.steelComponentsKg.0` | 2 faces × 33 bars × 2.95 m | 172.857402 |
| `elements.W1.steelComponentsKg.1` | 2 faces × 15 bars × 4.95 m | 91.555827 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3.75 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 3.75 m³ |
| Steel (known) | 264.41323 kg |
| &nbsp;&nbsp;of which ProvidedBars | 264.41323 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
