# BLDG-A — Building A — 1-story RC frame

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** building · **Fixture set:** buildings

## Purpose

A complete single-story frame: every element carries provided bars. Totals are cross-checked against a closed-form tally (12 columns + 9 X-beams + 8 Y-beams + slab + wall).

**Expected Quentra behaviour:** include all

## A. Structural truth

- Grid x = 0, 6, 12, 18 m; y = 0, 5, 10 m; story height 3.5 m
- Columns 12 × 0.45 × 0.45 (C40), 8T20 + T10@150 ties
- X-beams 9 × 0.3 × 0.6 × 6 m (C30), 3T20 + 3T20 + T10@150
- Y-beams 8 × 0.3 × 0.5 × 5 m (C30), 3T16 + 2T16 + T10@150
- Slab 18 × 10 × 0.2 with 2.5 × 3 m stair opening, T12@200 both ways
- Stair wall 3 × 3.5 × 0.2 with 1.0 × 2.1 m door, T10@200 both ways both faces

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 31, materials: 3, sections: 5, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C4`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C5`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C6`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C7`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C8`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C9`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C10`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C11`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Column `C12`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C450: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; h = 0.45 - 2 x 0.04 - 0.01 = 0.36 m; piece = 2 x (w + h) + hook = 2 x (0.36 + 0.36) + 0.2 = 1.64 m
    total length = 23 x 1 x 1.64 = 37.72 m; A_bar = 7.853982e-05 m2; mass = 37.72 x 7.853982e-05 x 7850 = 23.255797 kg
Element steel = 69.052207 + 23.255797 = 92.308003 kg
Status: Included
```

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B4`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B5`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B6`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B7`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B8`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B9`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 3 x 0.000314159 x 6 = 0.005654867 m3; mass = V x rho = 44.390704 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.15) + 1 = floor(39.333333) + 1 = 40
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 40 x 1 x 1.76 = 70.4 m; A_bar = 7.853982e-05 m2; mass = 70.4 x 7.853982e-05 x 7850 = 43.404244 kg
Element steel = 44.390704 + 44.390704 + 43.404244 = 132.185652 kg
Status: Included
```

### Beam `B10`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 5 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 3 x 0.000201062 x 5 = 0.003015929 m3; mass = V x rho = 23.675042 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 2 x 0.000201062 x 5 = 0.002010619 m3; mass = V x rho = 15.783361 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 5 m, end offset 0.05 m
    count N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 33 x 1 x 1.56 = 51.48 m; A_bar = 7.853982e-05 m2; mass = 51.48 x 7.853982e-05 x 7850 = 31.739353 kg
Element steel = 23.675042 + 15.783361 + 31.739353 = 71.197757 kg
Status: Included
```

### Beam `B11`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 5 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 3 x 0.000201062 x 5 = 0.003015929 m3; mass = V x rho = 23.675042 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 2 x 0.000201062 x 5 = 0.002010619 m3; mass = V x rho = 15.783361 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 5 m, end offset 0.05 m
    count N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 33 x 1 x 1.56 = 51.48 m; A_bar = 7.853982e-05 m2; mass = 51.48 x 7.853982e-05 x 7850 = 31.739353 kg
Element steel = 23.675042 + 15.783361 + 31.739353 = 71.197757 kg
Status: Included
```

### Beam `B12`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 5 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 3 x 0.000201062 x 5 = 0.003015929 m3; mass = V x rho = 23.675042 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 2 x 0.000201062 x 5 = 0.002010619 m3; mass = V x rho = 15.783361 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 5 m, end offset 0.05 m
    count N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 33 x 1 x 1.56 = 51.48 m; A_bar = 7.853982e-05 m2; mass = 51.48 x 7.853982e-05 x 7850 = 31.739353 kg
Element steel = 23.675042 + 15.783361 + 31.739353 = 71.197757 kg
Status: Included
```

### Beam `B13`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 5 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 3 x 0.000201062 x 5 = 0.003015929 m3; mass = V x rho = 23.675042 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 2 x 0.000201062 x 5 = 0.002010619 m3; mass = V x rho = 15.783361 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 5 m, end offset 0.05 m
    count N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 33 x 1 x 1.56 = 51.48 m; A_bar = 7.853982e-05 m2; mass = 51.48 x 7.853982e-05 x 7850 = 31.739353 kg
Element steel = 23.675042 + 15.783361 + 31.739353 = 71.197757 kg
Status: Included
```

### Beam `B14`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 5 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 3 x 0.000201062 x 5 = 0.003015929 m3; mass = V x rho = 23.675042 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 2 x 0.000201062 x 5 = 0.002010619 m3; mass = V x rho = 15.783361 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 5 m, end offset 0.05 m
    count N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 33 x 1 x 1.56 = 51.48 m; A_bar = 7.853982e-05 m2; mass = 51.48 x 7.853982e-05 x 7850 = 31.739353 kg
Element steel = 23.675042 + 15.783361 + 31.739353 = 71.197757 kg
Status: Included
```

### Beam `B15`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 5 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 3 x 0.000201062 x 5 = 0.003015929 m3; mass = V x rho = 23.675042 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 2 x 0.000201062 x 5 = 0.002010619 m3; mass = V x rho = 15.783361 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 5 m, end offset 0.05 m
    count N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 33 x 1 x 1.56 = 51.48 m; A_bar = 7.853982e-05 m2; mass = 51.48 x 7.853982e-05 x 7850 = 31.739353 kg
Element steel = 23.675042 + 15.783361 + 31.739353 = 71.197757 kg
Status: Included
```

### Beam `B16`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 5 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 3 x 0.000201062 x 5 = 0.003015929 m3; mass = V x rho = 23.675042 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 2 x 0.000201062 x 5 = 0.002010619 m3; mass = V x rho = 15.783361 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 5 m, end offset 0.05 m
    count N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 33 x 1 x 1.56 = 51.48 m; A_bar = 7.853982e-05 m2; mass = 51.48 x 7.853982e-05 x 7850 = 31.739353 kg
Element steel = 23.675042 + 15.783361 + 31.739353 = 71.197757 kg
Status: Included
```

### Beam `B17`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 5 = 0.75 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 3 x 0.000201062 x 5 = 0.003015929 m3; mass = V x rho = 23.675042 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 5 m (member length)
    V = 2 x 0.000201062 x 5 = 0.002010619 m3; mass = V x rho = 15.783361 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 5 m, end offset 0.05 m
    count N = floor((5 - 2 x 0.05) / 0.15) + 1 = floor(32.666667) + 1 = 33
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.5 - 2 x 0.025 - 0.01 = 0.44 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.44) + 0.2 = 1.56 m
    total length = 33 x 1 x 1.56 = 51.48 m; A_bar = 7.853982e-05 m2; mass = 51.48 x 7.853982e-05 x 7850 = 31.739353 kg
Element steel = 23.675042 + 15.783361 + 31.739353 = 71.197757 kg
Status: Included
```

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 180 m2 (shoelace on local u-v coordinates)
Opening STAIR: area 7.5 m2, fully inside the host
Sum of opening areas = 7.5 m2; area of (union of openings) inside host = 7.5 m2
Thickness t = 0.2 m (S200)
Gross volume = 180 x 0.2 = 36 m3
Net volume = (180 - 7.5) x 0.2 = 34.5 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along X, d = 12 mm @ 0.2 m, host rectangle: span along = 18 m, span across = 10 m, faces = 1
    bars N = floor((10 - 2 x 0.05) / 0.2) + 1 = floor(49.5) + 1 = 50; bar length = 18 - 2 x 0.025 = 17.95 m
    total length = 1 x 50 x 17.95 = 897.5 m; mass = 897.5 x 0.000113097 x 7850 = 796.81314 kg
[1] Mesh (bottom): bars along Y, d = 12 mm @ 0.2 m, host rectangle: span along = 10 m, span across = 18 m, faces = 1
    bars N = floor((18 - 2 x 0.05) / 0.2) + 1 = floor(89.5) + 1 = 90; bar length = 10 - 2 x 0.025 = 9.95 m
    total length = 1 x 90 x 9.95 = 895.5 m; mass = 895.5 x 0.000113097 x 7850 = 795.037512 kg
Element steel = 796.81314 + 795.037512 = 1591.850652 kg
Status: Included
```

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 10.5 m2 (shoelace on local u-v coordinates)
Opening D1: area 2.1 m2, fully inside the host
Sum of opening areas = 2.1 m2; area of (union of openings) inside host = 2.1 m2
Thickness t = 0.2 m (W200)
Gross volume = 10.5 x 0.2 = 2.1 m3
Net volume = (10.5 - 2.1) x 0.2 = 1.68 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along Vertical, d = 10 mm @ 0.2 m, host rectangle (wall-local): span along = 3.5 m, span across = 3 m, faces = 2
    bars N = floor((3 - 2 x 0.05) / 0.2) + 1 = floor(14.5) + 1 = 15; bar length = 3.5 - 2 x 0.025 = 3.45 m
    total length = 2 x 15 x 3.45 = 103.5 m; mass = 103.5 x 7.853982e-05 x 7850 = 63.811637 kg
[1] Mesh (bottom): bars along Horizontal, d = 10 mm @ 0.2 m, host rectangle (wall-local): span along = 3 m, span across = 3.5 m, faces = 2
    bars N = floor((3.5 - 2 x 0.05) / 0.2) + 1 = floor(17) + 1 = 18; bar length = 3 - 2 x 0.025 = 2.95 m
    total length = 2 x 18 x 2.95 = 106.2 m; mass = 106.2 x 7.853982e-05 x 7850 = 65.476289 kg
Element steel = 63.811637 + 65.476289 = 129.287926 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | closed-form Σ over element groups | 60.405 |
| `byStory.L1.netConcreteM3` | closed form | 60.405 |
| `byElementType.Beam.netConcreteM3` | closed form | 15.72 |
| `byElementType.Column.netConcreteM3` | closed form | 8.505 |
| `byElementType.Slab.netConcreteM3` | closed form | 34.5 |
| `byElementType.Wall.netConcreteM3` | closed form | 1.68 |
| `modelTotals.steelKg` | closed-form Σ of bar formulas | 4588.087547 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 62.325 m³ |
| Opening deduction | 1.92 m³ |
| Net concrete | 60.405 m³ |
| Steel (known) | 4588.087547 kg |
| &nbsp;&nbsp;of which ProvidedBars | 4588.087547 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

**By concrete material group:** `C40` = 8.505 m³ net, `C30` = 51.9 m³ net

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
