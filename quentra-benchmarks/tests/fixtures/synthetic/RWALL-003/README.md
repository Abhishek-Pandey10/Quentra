# RWALL-003 — Wall with boundary-zone reinforcement

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Special-boundary-element style wall: 0.6 m boundary zones at each end with 6T20 + closed hoops T10@100; web mesh confined to the region between them.

**Expected Quentra behaviour:** include

## A. Structural truth

- W1: 6.0 × 3.5 m, t = 0.30
- Boundary zones u 0–0.6 and 5.4–6.0: 6T20 × 3.5 m each, hoops T10@100 centre-line 0.22 × 0.50
- Web: vertical T12@200 both faces over u 0.6–5.4 (region); horizontal T10@200 both faces over full length

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 21 m2 (shoelace on local u-v coordinates)
Thickness t = 0.3 m (W)
Gross volume = 21 x 0.3 = 6.3 m3
Net volume = (21 - 0) x 0.3 = 6.3 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (web-vertical): bars along Vertical, d = 12 mm @ 0.2 m, explicit region (wall-local u,v): span along = 3.5 m, span across = 4.8 m, faces = 2
    bars N = floor((4.8 - 2 x 0.05) / 0.2) + 1 = floor(23.5) + 1 = 24; bar length = 3.5 - 2 x 0.025 = 3.45 m
    total length = 2 x 24 x 3.45 = 165.6 m; mass = 165.6 x 0.000113097 x 7850 = 147.022012 kg
[1] Mesh (web-horizontal): bars along Horizontal, d = 10 mm @ 0.2 m, host rectangle (wall-local): span along = 6 m, span across = 3.5 m, faces = 2
    bars N = floor((3.5 - 2 x 0.05) / 0.2) + 1 = floor(17) + 1 = 18; bar length = 6 - 2 x 0.025 = 5.95 m
    total length = 2 x 18 x 5.95 = 214.2 m; mass = 214.2 x 7.853982e-05 x 7850 = 132.062345 kg
[2] StraightBars (boundary-left): 6 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (given)
    V = 6 x 0.000314159 x 3.5 = 0.006597345 m3; mass = V x rho = 51.789155 kg
[3] StraightBars (boundary-right): 6 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (given)
    V = 6 x 0.000314159 x 3.5 = 0.006597345 m3; mass = V x rho = 51.789155 kg
[4] Hoops (boundary-left-hoops): d = 10 mm @ 0.1 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.1) + 1 = floor(34) + 1 = 35
    2 x (w + h) + hook = 2 x (0.22 + 0.5) + 0.2 = 1.64 m (centreline dims given)
    total length = 35 x 1 x 1.64 = 57.4 m; A_bar = 7.853982e-05 m2; mass = 57.4 x 7.853982e-05 x 7850 = 35.389256 kg
[5] Hoops (boundary-right-hoops): d = 10 mm @ 0.1 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.1) + 1 = floor(34) + 1 = 35
    2 x (w + h) + hook = 2 x (0.22 + 0.5) + 0.2 = 1.64 m (centreline dims given)
    total length = 35 x 1 x 1.64 = 57.4 m; A_bar = 7.853982e-05 m2; mass = 57.4 x 7.853982e-05 x 7850 = 35.389256 kg
Element steel = 147.022012 + 132.062345 + 51.789155 + 51.789155 + 35.389256 + 35.389256 = 453.441179 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.W1.steelComponentsKg.0` | web vertical: 2 × 24 × 3.45 m | 147.022012 |
| `elements.W1.steelComponentsKg.2` | 6 × A(20) × 3.5 × 7850 | 51.789155 |
| `elements.W1.steelComponentsKg.4` | N = floor(3.4/0.1) + 1 = 35 (exact division); piece 2 × 0.72 + 0.2 = 1.64 m | 35.389256 |
| `modelTotals.steelKg` | web + 2 × boundary | 453.441179 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 6.3 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 6.3 m³ |
| Steel (known) | 453.441179 kg |
| &nbsp;&nbsp;of which ProvidedBars | 453.441179 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
