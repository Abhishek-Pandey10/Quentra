# BLDG-D — Building D — irregular multi-story

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** building · **Fixture set:** buildings

## Purpose

Irregular geometry: L-shaped lower floors, 8 × 8 setback above, circular columns on one grid line, an inclined column, a retaining wall outside the grid with a vent, sloped roof rafters. Messy names throughout; slab steel defined per rectangular region.

**Expected Quentra behaviour:** include all

## A. Structural truth

- GF/1F: L-shape 16 × 8 + 8 × 8 (192 m²); 2F: 8 × 8 (64 m²)
- Circular D 0.5 columns on x = 0; square 0.5 elsewhere
- 1F column at (16,8) inclined: base (16,8), top (15,8), rise 3.5 m → L = √(1² + 3.5²)
- Roof: two rafters 0.3 × 0.5 rising 3 m over 8 m → L = √(8² + 3²)
- Retaining wall 16 × 4 × 0.25 at y = −1 with 1.2 × 0.6 m vent

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 52, materials: 3, sections: 7, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500B: density = 7850 kg/m3.

_This model has 52 elements; the per-element working is shown for the first element of each type/section combination. Every element follows the same formulas; totals are cross-checked against the closed-form hand check below._

### Column `COL_001`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 4^2) = 4 m
Section COL-D500: circle D = 0.5 m, A = pi x D^2 / 4 = 0.19635 m2
Gross volume = A x L = 0.19635 x 4 = 0.785398 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 4 m (member length)
    V = 8 x 0.000314159 x 4 = 0.010053096 m3; mass = V x rho = 78.916807 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 4 m, end offset 0.05 m
    count N = floor((4 - 2 x 0.05) / 0.15) + 1 = floor(26) + 1 = 27
    centreline Dc = D - 2c - d = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; piece = pi x Dc + hook = 1.488053 m
    total length = 27 x 1 x 1.488053 = 40.177431 m; A_bar = 7.853982e-05 m2; mass = 40.177431 x 7.853982e-05 x 7850 = 24.770895 kg
Element steel = 78.916807 + 24.770895 = 103.687702 kg
Status: Included
```

### Column `COL_002`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 4^2) = 4 m
Section COL-500SQ: rectangle b x h = 0.5 m x 0.5 m, A = 0.25 m2
Gross volume = A x L = 0.25 x 4 = 1 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 4 m (member length)
    V = 8 x 0.000314159 x 4 = 0.010053096 m3; mass = V x rho = 78.916807 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 4 m, end offset 0.05 m
    count N = floor((4 - 2 x 0.05) / 0.15) + 1 = floor(26) + 1 = 27
    centreline w = b - 2c - d = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; h = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; piece = 2 x (w + h) + hook = 2 x (0.41 + 0.41) + 0.2 = 1.84 m
    total length = 27 x 1 x 1.84 = 49.68 m; A_bar = 7.853982e-05 m2; mass = 49.68 x 7.853982e-05 x 7850 = 30.629586 kg
Element steel = 78.916807 + 30.629586 = 109.546393 kg
Status: Included
```

### Beam `Beam_GF_01`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(8^2 + 0^2 + 0^2) = 8 m
Section BM 300X650: rectangle b x h = 0.3 m x 0.65 m, A = 0.195 m2
Gross volume = A x L = 0.195 x 8 = 1.56 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 8 m (member length)
    V = 3 x 0.000314159 x 8 = 0.007539822 m3; mass = V x rho = 59.187606 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 8 m (member length)
    V = 3 x 0.000314159 x 8 = 0.007539822 m3; mass = V x rho = 59.187606 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 8 m, end offset 0.05 m
    count N = floor((8 - 2 x 0.05) / 0.15) + 1 = floor(52.666667) + 1 = 53
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.65 - 2 x 0.025 - 0.01 = 0.59 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.59) + 0.2 = 1.86 m
    total length = 53 x 1 x 1.86 = 98.58 m; A_bar = 7.853982e-05 m2; mass = 98.58 x 7.853982e-05 x 7850 = 60.778272 kg
Element steel = 59.187606 + 59.187606 + 60.778272 = 179.153484 kg
Status: Included
```

### Slab `SLAB-GF-A`

```text
Host polygon (6 vertices) area in its own plane = 192 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (SLAB 200)
Gross volume = 192 x 0.2 = 38.4 m3
Net volume = (192 - 0) x 0.2 = 38.4 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along X, d = 12 mm @ 0.2 m, explicit region: span along = 16 m, span across = 8 m, faces = 1
    bars N = floor((8 - 2 x 0.05) / 0.2) + 1 = floor(39.5) + 1 = 40; bar length = 16 - 2 x 0.025 = 15.95 m
    total length = 1 x 40 x 15.95 = 638 m; mass = 638 x 0.000113097 x 7850 = 566.425386 kg
[1] Mesh (bottom): bars along Y, d = 12 mm @ 0.2 m, explicit region: span along = 8 m, span across = 16 m, faces = 1
    bars N = floor((16 - 2 x 0.05) / 0.2) + 1 = floor(79.5) + 1 = 80; bar length = 8 - 2 x 0.025 = 7.95 m
    total length = 1 x 80 x 7.95 = 636 m; mass = 636 x 0.000113097 x 7850 = 564.649757 kg
[2] Mesh (bottom): bars along X, d = 12 mm @ 0.2 m, explicit region: span along = 8 m, span across = 8 m, faces = 1
    bars N = floor((8 - 2 x 0.05) / 0.2) + 1 = floor(39.5) + 1 = 40; bar length = 8 - 2 x 0.025 = 7.95 m
    total length = 1 x 40 x 7.95 = 318 m; mass = 318 x 0.000113097 x 7850 = 282.324879 kg
[3] Mesh (bottom): bars along Y, d = 12 mm @ 0.2 m, explicit region: span along = 8 m, span across = 8 m, faces = 1
    bars N = floor((8 - 2 x 0.05) / 0.2) + 1 = floor(39.5) + 1 = 40; bar length = 8 - 2 x 0.025 = 7.95 m
    total length = 1 x 40 x 7.95 = 318 m; mass = 318 x 0.000113097 x 7850 = 282.324879 kg
Element steel = 566.425386 + 564.649757 + 282.324879 + 282.324879 = 1695.7249 kg
Status: Included
```

### Wall `RW-01`

```text
Host polygon (4 vertices) area in its own plane = 64 m2 (shoelace on local u-v coordinates)
Opening VENT: area 0.72 m2, fully inside the host
Sum of opening areas = 0.72 m2; area of (union of openings) inside host = 0.72 m2
Thickness t = 0.25 m (RW 250)
Gross volume = 64 x 0.25 = 16 m3
Net volume = (64 - 0.72) x 0.25 = 15.82 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along Vertical, d = 16 mm @ 0.15 m, host rectangle (wall-local): span along = 4 m, span across = 16 m, faces = 2
    bars N = floor((16 - 2 x 0.05) / 0.15) + 1 = floor(106) + 1 = 107; bar length = 4 - 2 x 0.025 = 3.95 m
    total length = 2 x 107 x 3.95 = 845.3 m; mass = 845.3 x 0.000201062 x 7850 = 1334.167547 kg
[1] Mesh (bottom): bars along Horizontal, d = 12 mm @ 0.2 m, host rectangle (wall-local): span along = 16 m, span across = 4 m, faces = 2
    bars N = floor((4 - 2 x 0.05) / 0.2) + 1 = floor(19.5) + 1 = 20; bar length = 16 - 2 x 0.025 = 15.95 m
    total length = 2 x 20 x 15.95 = 638 m; mass = 638 x 0.000113097 x 7850 = 566.425386 kg
Element steel = 1334.167547 + 566.425386 = 1900.592932 kg
Status: Included
```

### Beam `RAFTER_0`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 8^2 + 3^2) = 8.544004 m
Section BM-SLOPE 300X500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 8.544004 = 1.281601 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 8.544004 m (member length)
    V = 4 x 0.000201062 x 8.544004 = 0.006871496 m3; mass = V x rho = 53.94124 kg
[1] Hoops (ties): d = 10 mm @ 0.2 m over zone 8.544004 m, end offset 0.05 m
    count N = floor((8.544004 - 2 x 0.05) / 0.2) + 1 = floor(42.220019) + 1 = 43
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 43 x 1 x 1.56 = 67.08 m; A_bar = 7.853982e-05 m2; mass = 67.08 x 7.853982e-05 x 7850 = 41.357339 kg
Element steel = 53.94124 + 41.357339 = 95.298579 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | closed-form Σ over element groups | 163.692667 |
| `byStory.GF.netConcreteM3` | closed form | 77.176194 |
| `byStory.1F.netConcreteM3` | closed form | 60.288824 |
| `byStory.2F.netConcreteM3` | closed form | 22.164447 |
| `byStory.RF.netConcreteM3` | closed form | 4.063201 |
| `byElementType.Beam.netConcreteM3` | closed form | 39.820341 |
| `byElementType.Column.netConcreteM3` | closed form | 18.452325 |
| `byElementType.Slab.netConcreteM3` | closed form | 89.6 |
| `byElementType.Wall.netConcreteM3` | closed form | 15.82 |
| `modelTotals.steelKg` | closed-form Σ of bar formulas | 12411.396452 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 163.872667 m³ |
| Opening deduction | 0.18 m³ |
| Net concrete | 163.692667 m³ |
| Steel (known) | 12411.396452 kg |
| &nbsp;&nbsp;of which ProvidedBars | 12411.396452 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| GF | 77.356194 | 77.176194 | 6246.647744 |
| 1F | 60.288824 | 60.288824 | 4217.728257 |
| 2F | 22.164447 | 22.164447 | 1651.858523 |
| RF | 4.063201 | 4.063201 | 295.161928 |

**By concrete material group:** `C40/50` = 34.272325 m³ net, `C30/37` = 129.420341 m³ net

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
