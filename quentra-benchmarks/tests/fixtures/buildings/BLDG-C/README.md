# BLDG-C — Building C — 10-story wall-frame

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** building · **Fixture set:** buildings

## Purpose

Ten stories, 24 columns per floor with two section steps, two six-metre cores with doors, wall thickness and concrete grade step at L6, two rebar grades.

**Expected Quentra behaviour:** include all

## A. Structural truth

- Grid 5 × 3 bays @ 8 m (40 × 24 m); L1 4.5 m, L2–L10 3.2 m
- Columns 0.8 (L1–L3, 16T32), 0.7 (L4–L6, 12T28), 0.6 (L7–L10, 12T25); C50 to L5, C40 above; T12@150 hoop + 2 crossties
- Beams 0.4 × 0.75 × 8 m both ways (C35), 4T25 + 4T25 + T12@150
- Slabs 40 × 24 × 0.25 (C35) with two 6 × 6 core openings; T12@150 bottom, T12@200 top, both ways
- Cores: walls 0.35 (L1–L5) / 0.25 (L6–L10), door 1.0 × 2.4 in each south wall; mesh both faces + 8T25 boundary bars

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 710, materials: 5, sections: 7, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.
- Rebar material B500-LARGE: density = 7850 kg/m3.

_This model has 710 elements; the per-element working is shown for the first element of each type/section combination. Every element follows the same formulas; totals are cross-checked against the closed-form hand check below._

### Column `C0_0.L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 4.5^2) = 4.5 m
Section C800: rectangle b x h = 0.8 m x 0.8 m, A = 0.64 m2
Gross volume = A x L = 0.64 x 4.5 = 2.88 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 16 bars, d = 32 mm, A_bar = pi x 0.032^2 / 4 = 0.000804248 m2; L = 4.5 m (member length)
    V = 16 x 0.000804248 x 4.5 = 0.057905836 m3; mass = V x rho = 454.560811 kg
[1] Hoops (ties): d = 12 mm @ 0.15 m over zone 4.5 m, end offset 0.05 m
    count N = floor((4.5 - 2 x 0.05) / 0.15) + 1 = floor(29.333333) + 1 = 30
    centreline w = b - 2c - d = 0.8 - 2 x 0.04 - 0.012 = 0.708 m; h = 0.8 - 2 x 0.04 - 0.012 = 0.708 m; piece = 2 x (w + h) + hook = 2 x (0.708 + 0.708) + 0.24 = 3.072 m
    total length = 30 x 1 x 3.072 = 92.16 m; A_bar = 0.000113097 m2; mass = 92.16 x 0.000113097 x 7850 = 81.820946 kg
[2] Crossties (crossties): d = 12 mm @ 0.15 m over zone 4.5 m, end offset 0.05 m
    count N = floor((4.5 - 2 x 0.05) / 0.15) + 1 = floor(29.333333) + 1 = 30
    piece length 0.948 m (given)
    total length = 30 x 2 x 0.948 = 56.88 m; A_bar = 0.000113097 m2; mass = 56.88 x 0.000113097 x 7850 = 50.498865 kg
Element steel = 454.560811 + 81.820946 + 50.498865 = 586.880622 kg
Status: Included
```

### Beam `BX0_0.L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(8^2 + 0^2 + 0^2) = 8 m
Section B400x750: rectangle b x h = 0.4 m x 0.75 m, A = 0.3 m2
Gross volume = A x L = 0.3 x 8 = 2.4 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 4 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 8 m (member length)
    V = 4 x 0.000490874 x 8 = 0.015707963 m3; mass = V x rho = 123.307512 kg
[1] StraightBars (top): 4 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 8 m (member length)
    V = 4 x 0.000490874 x 8 = 0.015707963 m3; mass = V x rho = 123.307512 kg
[2] Hoops (ties): d = 12 mm @ 0.15 m over zone 8 m, end offset 0.05 m
    count N = floor((8 - 2 x 0.05) / 0.15) + 1 = floor(52.666667) + 1 = 53
    centreline w = b - 2c - d = 0.4 - 2 x 0.025 - 0.012 = 0.338 m; h = 0.75 - 2 x 0.025 - 0.012 = 0.688 m; piece = 2 x (w + h) + hook = 2 x (0.338 + 0.688) + 0.24 = 2.292 m
    total length = 53 x 1 x 2.292 = 121.476 m; A_bar = 0.000113097 m2; mass = 121.476 x 0.000113097 x 7850 = 107.848104 kg
Element steel = 123.307512 + 123.307512 + 107.848104 = 354.463127 kg
Status: Included
```

### Slab `SL.L1`

```text
Host polygon (4 vertices) area in its own plane = 960 m2 (shoelace on local u-v coordinates)
Opening CORE1: area 36 m2, fully inside the host
Opening CORE2: area 36 m2, fully inside the host
Sum of opening areas = 72 m2; area of (union of openings) inside host = 72 m2
Thickness t = 0.25 m (S250)
Gross volume = 960 x 0.25 = 240 m3
Net volume = (960 - 72) x 0.25 = 222 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along X, d = 12 mm @ 0.15 m, host rectangle: span along = 40 m, span across = 24 m, faces = 1
    bars N = floor((24 - 2 x 0.05) / 0.15) + 1 = floor(159.333333) + 1 = 160; bar length = 40 - 2 x 0.025 = 39.95 m
    total length = 1 x 160 x 39.95 = 6392 m; mass = 6392 x 0.000113097 x 7850 = 5674.907624 kg
[1] Mesh (bottom): bars along Y, d = 12 mm @ 0.15 m, host rectangle: span along = 24 m, span across = 40 m, faces = 1
    bars N = floor((40 - 2 x 0.05) / 0.15) + 1 = floor(266) + 1 = 267; bar length = 24 - 2 x 0.025 = 23.95 m
    total length = 1 x 267 x 23.95 = 6394.65 m; mass = 6394.65 x 0.000113097 x 7850 = 5677.260332 kg
[2] Mesh (top): bars along X, d = 12 mm @ 0.2 m, host rectangle: span along = 40 m, span across = 24 m, faces = 1
    bars N = floor((24 - 2 x 0.05) / 0.2) + 1 = floor(119.5) + 1 = 120; bar length = 40 - 2 x 0.025 = 39.95 m
    total length = 1 x 120 x 39.95 = 4794 m; mass = 4794 x 0.000113097 x 7850 = 4256.180718 kg
[3] Mesh (top): bars along Y, d = 12 mm @ 0.2 m, host rectangle: span along = 24 m, span across = 40 m, faces = 1
    bars N = floor((40 - 2 x 0.05) / 0.2) + 1 = floor(199.5) + 1 = 200; bar length = 24 - 2 x 0.025 = 23.95 m
    total length = 1 x 200 x 23.95 = 4790 m; mass = 4790 x 0.000113097 x 7850 = 4252.629462 kg
Element steel = 5674.907624 + 5677.260332 + 4256.180718 + 4252.629462 = 19860.978136 kg
Status: Included
```

### Wall `CORE1-S.L1`

```text
Host polygon (4 vertices) area in its own plane = 27 m2 (shoelace on local u-v coordinates)
Opening DOOR: area 2.4 m2, fully inside the host
Sum of opening areas = 2.4 m2; area of (union of openings) inside host = 2.4 m2
Thickness t = 0.35 m (CW350)
Gross volume = 27 x 0.35 = 9.45 m3
Net volume = (27 - 2.4) x 0.35 = 8.61 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along Vertical, d = 16 mm @ 0.2 m, host rectangle (wall-local): span along = 4.5 m, span across = 6 m, faces = 2
    bars N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30; bar length = 4.5 - 2 x 0.025 = 4.45 m
    total length = 2 x 30 x 4.45 = 267 m; mass = 267 x 0.000201062 x 7850 = 421.415752 kg
[1] Mesh (bottom): bars along Horizontal, d = 12 mm @ 0.2 m, host rectangle (wall-local): span along = 6 m, span across = 4.5 m, faces = 2
    bars N = floor((4.5 - 2 x 0.05) / 0.2) + 1 = floor(22) + 1 = 23; bar length = 6 - 2 x 0.025 = 5.95 m
    total length = 2 x 23 x 5.95 = 273.7 m; mass = 273.7 x 0.000113097 x 7850 = 242.994715 kg
[2] StraightBars (boundary-both-ends): 8 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 4.5 m (given)
    V = 8 x 0.000490874 x 4.5 = 0.017671459 m3; mass = V x rho = 138.720951 kg
Element steel = 421.415752 + 242.994715 + 138.720951 = 803.131417 kg
Status: Included
```

### Column `C0_0.L4`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C700: rectangle b x h = 0.7 m x 0.7 m, A = 0.49 m2
Gross volume = A x L = 0.49 x 3.2 = 1.568 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 12 bars, d = 28 mm, A_bar = pi x 0.028^2 / 4 = 0.000615752 m2; L = 3.2 m (member length)
    V = 12 x 0.000615752 x 3.2 = 0.023644883 m3; mass = V x rho = 185.612331 kg
[1] Hoops (ties): d = 12 mm @ 0.15 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.15) + 1 = floor(20.666667) + 1 = 21
    centreline w = b - 2c - d = 0.7 - 2 x 0.04 - 0.012 = 0.608 m; h = 0.7 - 2 x 0.04 - 0.012 = 0.608 m; piece = 2 x (w + h) + hook = 2 x (0.608 + 0.608) + 0.24 = 2.672 m
    total length = 21 x 1 x 2.672 = 56.112 m; A_bar = 0.000113097 m2; mass = 56.112 x 0.000113097 x 7850 = 49.817024 kg
[2] Crossties (crossties): d = 12 mm @ 0.15 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.15) + 1 = floor(20.666667) + 1 = 21
    piece length 0.848 m (given)
    total length = 21 x 2 x 0.848 = 35.616 m; A_bar = 0.000113097 m2; mass = 35.616 x 0.000113097 x 7850 = 31.620386 kg
Element steel = 185.612331 + 49.817024 + 31.620386 = 267.049741 kg
Status: Included
```

### Wall `CORE1-S.L6`

```text
Host polygon (4 vertices) area in its own plane = 19.2 m2 (shoelace on local u-v coordinates)
Opening DOOR: area 2.4 m2, fully inside the host
Sum of opening areas = 2.4 m2; area of (union of openings) inside host = 2.4 m2
Thickness t = 0.25 m (CW250)
Gross volume = 19.2 x 0.25 = 4.8 m3
Net volume = (19.2 - 2.4) x 0.25 = 4.2 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along Vertical, d = 12 mm @ 0.2 m, host rectangle (wall-local): span along = 3.2 m, span across = 6 m, faces = 2
    bars N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30; bar length = 3.2 - 2 x 0.025 = 3.15 m
    total length = 2 x 30 x 3.15 = 189 m; mass = 189 x 0.000113097 x 7850 = 167.796862 kg
[1] Mesh (bottom): bars along Horizontal, d = 12 mm @ 0.2 m, host rectangle (wall-local): span along = 6 m, span across = 3.2 m, faces = 2
    bars N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16; bar length = 6 - 2 x 0.025 = 5.95 m
    total length = 2 x 16 x 5.95 = 190.4 m; mass = 190.4 x 0.000113097 x 7850 = 169.039802 kg
[2] StraightBars (boundary-both-ends): 8 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 3.2 m (given)
    V = 8 x 0.000490874 x 3.2 = 0.012566371 m3; mass = V x rho = 98.646009 kg
Element steel = 167.796862 + 169.039802 + 98.646009 = 435.482673 kg
Status: Included
```

### Column `C0_0.L7`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C600: rectangle b x h = 0.6 m x 0.6 m, A = 0.36 m2
Gross volume = A x L = 0.36 x 3.2 = 1.152 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 12 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 3.2 m (member length)
    V = 12 x 0.000490874 x 3.2 = 0.018849556 m3; mass = V x rho = 147.969014 kg
[1] Hoops (ties): d = 12 mm @ 0.15 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.15) + 1 = floor(20.666667) + 1 = 21
    centreline w = b - 2c - d = 0.6 - 2 x 0.04 - 0.012 = 0.508 m; h = 0.6 - 2 x 0.04 - 0.012 = 0.508 m; piece = 2 x (w + h) + hook = 2 x (0.508 + 0.508) + 0.24 = 2.272 m
    total length = 21 x 1 x 2.272 = 47.712 m; A_bar = 0.000113097 m2; mass = 47.712 x 0.000113097 x 7850 = 42.359386 kg
[2] Crossties (crossties): d = 12 mm @ 0.15 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.15) + 1 = floor(20.666667) + 1 = 21
    piece length 0.748 m (given)
    total length = 21 x 2 x 0.748 = 31.416 m; A_bar = 0.000113097 m2; mass = 31.416 x 0.000113097 x 7850 = 27.891567 kg
Element steel = 147.969014 + 42.359386 + 27.891567 = 218.219967 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | closed-form Σ over element groups | 3991.152 |
| `byStory.L1.netConcreteM3` | closed form | 456.24 |
| `byStory.L2.netConcreteM3` | closed form | 414.432 |
| `byStory.L3.netConcreteM3` | closed form | 414.432 |
| `byStory.L4.netConcreteM3` | closed form | 402.912 |
| `byStory.L5.netConcreteM3` | closed form | 402.912 |
| `byStory.L6.netConcreteM3` | closed form | 388.032 |
| `byStory.L7.netConcreteM3` | closed form | 378.048 |
| `byStory.L8.netConcreteM3` | closed form | 378.048 |
| `byStory.L9.netConcreteM3` | closed form | 378.048 |
| `byStory.L10.netConcreteM3` | closed form | 378.048 |
| `byElementType.Beam.netConcreteM3` | closed form | 912 |
| `byElementType.Column.netConcreteM3` | closed form | 390.912 |
| `byElementType.Slab.netConcreteM3` | closed form | 2220 |
| `byElementType.Wall.netConcreteM3` | closed form | 468.24 |
| `modelTotals.steelKg` | closed-form Σ of bar formulas | 449485.305292 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 4185.552 m³ |
| Opening deduction | 194.4 m³ |
| Net concrete | 3991.152 m³ |
| Steel (known) | 449485.305292 kg |
| &nbsp;&nbsp;of which ProvidedBars | 449485.305292 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| L1 | 475.92 | 456.24 | 53840.763227 |
| L2 | 434.112 | 414.432 | 47839.318372 |
| L3 | 434.112 | 414.432 | 47839.318372 |
| L4 | 422.592 | 402.912 | 44267.7015 |
| L5 | 422.592 | 402.912 | 44267.7015 |
| L6 | 407.232 | 388.032 | 43223.632137 |
| L7 | 397.248 | 378.048 | 42051.717546 |
| L8 | 397.248 | 378.048 | 42051.717546 |
| L9 | 397.248 | 378.048 | 42051.717546 |
| L10 | 397.248 | 378.048 | 42051.717546 |

**By concrete material group:** `C50` = 524.928 m³ net, `C35` = 3132 m³ net, `C40` = 334.224 m³ net

**By steel material:** `B500-LARGE` = 53274.337644 kg, `B500` = 396210.967648 kg

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
