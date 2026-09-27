# UNITS-CM — Unit equivalence — cm

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** units · **Fixture set:** synthetic · **Equivalence group:** `UNITS-A`

## Purpose

One physical model (column, beam, slab with opening, wall with door, provided bars) expressed in **cm**. All five UNITS-* cases describe the same structure and must give identical physical quantities.

**Expected Quentra behaviour:** identical physical quantities in every unit system

## A. Structural truth

- Model defined in inches (multiples of 0.375 in): column 18×18 in × 144 in, beam 12×24 in × 240 in, slab 240×180×6 in with a 36×48 in opening, wall 120×144×9 in with a 36×84 in door
- Bars: #6 = 0.75 in, #3 = 0.375 in; density 7850 kg/m³ (metric files) or its exact lb/ft³ equivalent (imperial files)
- This file expresses every length in cm (1 in = 2.54 cm)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `cm`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 4, materials: 2, sections: 4, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: cm (1 cm = 0.01 m).
- Rebar material A615-60: density = 7850 kg/m3.

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.6576^2) = 3.6576 m
Section COL18x18: rectangle b x h = 0.4572 m x 0.4572 m, A = 0.209032 m2
Gross volume = A x L = 0.209032 x 3.6576 = 0.764555 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 19.05 mm, A_bar = pi x 0.01905^2 / 4 = 0.000285023 m2; L = 3.6576 m (member length)
    V = 4 x 0.000285023 x 3.6576 = 0.00417 m3; mass = V x rho = 32.734499 kg
[1] Hoops (ties): d = 9.525 mm @ 0.2286 m over zone 3.6576 m, end offset 0.0762 m
    count N = floor((3.6576 - 2 x 0.0762) / 0.2286) + 1 = floor(15.333333) + 1 = 16
    centreline w = b - 2c - d = 0.4572 - 2 x 0.0381 - 0.009525 = 0.371475 m; h = 0.4572 - 2 x 0.0381 - 0.009525 = 0.371475 m; piece = 2 x (w + h) + hook = 2 x (0.371475 + 0.371475) + 0.1524 = 1.6383 m
    total length = 16 x 1 x 1.6383 = 26.2128 m; A_bar = 7.125574e-05 m2; mass = 26.2128 x 7.125574e-05 x 7850 = 14.662328 kg
Element steel = 32.734499 + 14.662328 = 47.396827 kg
Status: Included
```

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6.096^2 + 0^2 + 0^2) = 6.096 m
Section BM12x24: rectangle b x h = 0.3048 m x 0.6096 m, A = 0.185806 m2
Gross volume = A x L = 0.185806 x 6.096 = 1.132674 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 19.05 mm, A_bar = pi x 0.01905^2 / 4 = 0.000285023 m2; L = 6.096 m (member length)
    V = 3 x 0.000285023 x 6.096 = 0.0052125 m3; mass = V x rho = 40.918124 kg
[1] StraightBars (top): 2 bars, d = 19.05 mm, A_bar = pi x 0.01905^2 / 4 = 0.000285023 m2; L = 6.096 m (member length)
    V = 2 x 0.000285023 x 6.096 = 0.003475 m3; mass = V x rho = 27.278749 kg
[2] Hoops (ties): d = 9.525 mm @ 0.1524 m over zone 6.096 m, end offset 0.0762 m
    count N = floor((6.096 - 2 x 0.0762) / 0.1524) + 1 = floor(39) + 1 = 40
    centreline w = b - 2c - d = 0.3048 - 2 x 0.0381 - 0.009525 = 0.219075 m; h = 0.6096 - 2 x 0.0381 - 0.009525 = 0.523875 m; piece = 2 x (w + h) + hook = 2 x (0.219075 + 0.523875) + 0.1524 = 1.6383 m
    total length = 40 x 1 x 1.6383 = 65.532 m; A_bar = 7.125574e-05 m2; mass = 65.532 x 7.125574e-05 x 7850 = 36.655819 kg
Element steel = 40.918124 + 27.278749 + 36.655819 = 104.852692 kg
Status: Included
```

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 27.870912 m2 (shoelace on local u-v coordinates)
Opening O1: area 1.114836 m2, fully inside the host
Sum of opening areas = 1.114836 m2; area of (union of openings) inside host = 1.114836 m2
Thickness t = 0.1524 m (SLAB6)
Gross volume = 27.870912 x 0.1524 = 4.247527 m3
Net volume = (27.870912 - 1.114836) x 0.1524 = 4.077626 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along X, d = 9.525 mm @ 0.2286 m, host rectangle: span along = 6.096 m, span across = 4.572 m, faces = 1
    bars N = floor((4.572 - 2 x 0.0762) / 0.2286) + 1 = floor(19.333333) + 1 = 20; bar length = 6.096 - 2 x 0.0381 = 6.0198 m
    total length = 1 x 20 x 6.0198 = 120.396 m; mass = 120.396 x 7.125574e-05 x 7850 = 67.344412 kg
[1] Mesh (bottom): bars along Y, d = 9.525 mm @ 0.2286 m, host rectangle: span along = 4.572 m, span across = 6.096 m, faces = 1
    bars N = floor((6.096 - 2 x 0.0762) / 0.2286) + 1 = floor(26) + 1 = 27; bar length = 4.572 - 2 x 0.0381 = 4.4958 m
    total length = 1 x 27 x 4.4958 = 121.3866 m; mass = 121.3866 x 7.125574e-05 x 7850 = 67.898512 kg
Element steel = 67.344412 + 67.898512 = 135.242924 kg
Status: Included
```

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 11.148365 m2 (shoelace on local u-v coordinates)
Opening D1: area 1.950964 m2, fully inside the host
Sum of opening areas = 1.950964 m2; area of (union of openings) inside host = 1.950964 m2
Thickness t = 0.2286 m (WALL9)
Gross volume = 11.148365 x 0.2286 = 2.548516 m3
Net volume = (11.148365 - 1.950964) x 0.2286 = 2.102526 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (vertical): bars along Vertical, d = 9.525 mm @ 0.3048 m, host rectangle (wall-local): span along = 3.6576 m, span across = 3.048 m, faces = 2
    bars N = floor((3.048 - 2 x 0.0762) / 0.3048) + 1 = floor(9.5) + 1 = 10; bar length = 3.6576 - 2 x 0.0381 = 3.5814 m
    total length = 2 x 10 x 3.5814 = 71.628 m; mass = 71.628 x 7.125574e-05 x 7850 = 40.065663 kg
[1] Mesh (horizontal): bars along Horizontal, d = 9.525 mm @ 0.3048 m, host rectangle (wall-local): span along = 3.048 m, span across = 3.6576 m, faces = 2
    bars N = floor((3.6576 - 2 x 0.0762) / 0.3048) + 1 = floor(11.5) + 1 = 12; bar length = 3.048 - 2 x 0.0381 = 2.9718 m
    total length = 2 x 12 x 2.9718 = 71.3232 m; mass = 71.3232 x 7.125574e-05 x 7850 = 39.895171 kg
Element steel = 40.065663 + 39.895171 = 79.960833 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byElementType.Column.netConcreteM3` | (18 × 0.0254)² × 144 × 0.0254 | 0.764555 |
| `byElementType.Beam.netConcreteM3` | (12 × 24 × 240) in³ × 0.0254³ | 1.132674 |
| `byElementType.Slab.netConcreteM3` | (240 × 180 − 36 × 48) × 6 in³ × 0.0254³ | 4.077626 |
| `byElementType.Wall.netConcreteM3` | (120 × 144 − 36 × 84) × 9 in³ × 0.0254³ | 2.102526 |
| `modelTotals.netConcreteM3` | sum | 8.07738 |
| `elements.S1.steelComponentsKg.1` | Y bars: floor((240 − 6)/9) + 1 = 26 + 1 = 27 — an exact division; the count must not drop to 26 from floating-point error | 67.898512 |
| `modelTotals.steelKg` | column 4#6 + 16 hoops; beam 5#6 + 40 hoops; slab 20 + 27 bars; wall 2 × 10 + 2 × 12 bars | 367.453276 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 8.693272 m³ |
| Opening deduction | 0.615891 m³ |
| Net concrete | 8.07738 m³ |
| Steel (known) | 367.453276 kg |
| &nbsp;&nbsp;of which ProvidedBars | 367.453276 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

## Notes and assumptions

- Exact-division bar counts are protected by adding 1e-9 before floor() (see CONVENTIONS.md §5).
- The imperial density value is 7850 kg/m³ ÷ 16.018463373960138 (exact lb/ft³ → kg/m³ factor), written with full double precision.

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
