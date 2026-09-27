# AGG-001 — Four-story aggregation model

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** aggregation · **Fixture set:** synthetic

## Purpose

Stories L1, L2, L3, Roof with beams, columns, slabs and walls. Verifies per-story, per-type and whole-model totals and that Σ(story) = model.

**Expected Quentra behaviour:** Σ(story totals) = model total; Σ(type totals) = model total

## A. Structural truth

- Each story: 2 columns 0.4 × 0.4 × 3.2 m (C40), 1 beam 0.3 × 0.6 × 6 m (C30), 1 slab 6 × 4 m (t 0.20; Roof t 0.15), 1 wall 4 × 3.2 × 0.2 m (not at Roof)
- Columns: 4T20 + T8@200 ties; beams: 5T16 + T8@200 stirrups; slabs: estimated 80 kg/m³; walls: no reinforcement (steel unknown, warned)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 19, materials: 3, sections: 5, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Column `C1-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.2 = 0.512 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.2 m (member length)
    V = 4 x 0.000314159 x 3.2 = 0.004021239 m3; mass = V x rho = 31.566723 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 16 x 1 x 1.408 = 22.528 m; A_bar = 5.026548e-05 m2; mass = 22.528 x 5.026548e-05 x 7850 = 8.889189 kg
Element steel = 31.566723 + 8.889189 = 40.455912 kg
Status: Included
```

### Column `C2-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.2 = 0.512 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.2 m (member length)
    V = 4 x 0.000314159 x 3.2 = 0.004021239 m3; mass = V x rho = 31.566723 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 16 x 1 x 1.408 = 22.528 m; A_bar = 5.026548e-05 m2; mass = 22.528 x 5.026548e-05 x 7850 = 8.889189 kg
Element steel = 31.566723 + 8.889189 = 40.455912 kg
Status: Included
```

### Beam `B1-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 3 x 0.000201062 x 6 = 0.003619115 m3; mass = V x rho = 28.410051 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 2 x 0.000201062 x 6 = 0.002412743 m3; mass = V x rho = 18.940034 kg
[2] Hoops (ties): d = 8 mm @ 0.2 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.008 = 0.242 m; h = 0.6 - 2 x 0.025 - 0.008 = 0.542 m; piece = 2 x (w + h) + hook = 2 x (0.242 + 0.542) + 0.16 = 1.728 m
    total length = 30 x 1 x 1.728 = 51.84 m; A_bar = 5.026548e-05 m2; mass = 51.84 x 5.026548e-05 x 7850 = 20.455236 kg
Element steel = 28.410051 + 18.940034 + 20.455236 = 67.805321 kg
Status: Included
```

### Slab `S1-L1`

```text
Host polygon (4 vertices) area in its own plane = 24 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 24 x 0.2 = 4.8 m3
Net volume = (24 - 0) x 0.2 = 4.8 m3
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 80 kg/m3 x net concrete 4.8 m3 = 384 kg (ESTIMATE)
Element steel = 384 kg
Status: Included
```

### Wall `W1-L1`

```text
Host polygon (4 vertices) area in its own plane = 12.8 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (W200)
Gross volume = 12.8 x 0.2 = 2.56 m3
Net volume = (12.8 - 0) x 0.2 = 2.56 m3
Steel: no reinforcement data -> steel UNKNOWN (not zero) (MISSING_REINFORCEMENT)
Status: Included
```

### Column `C1-L2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.2 = 0.512 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.2 m (member length)
    V = 4 x 0.000314159 x 3.2 = 0.004021239 m3; mass = V x rho = 31.566723 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 16 x 1 x 1.408 = 22.528 m; A_bar = 5.026548e-05 m2; mass = 22.528 x 5.026548e-05 x 7850 = 8.889189 kg
Element steel = 31.566723 + 8.889189 = 40.455912 kg
Status: Included
```

### Column `C2-L2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.2 = 0.512 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.2 m (member length)
    V = 4 x 0.000314159 x 3.2 = 0.004021239 m3; mass = V x rho = 31.566723 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 16 x 1 x 1.408 = 22.528 m; A_bar = 5.026548e-05 m2; mass = 22.528 x 5.026548e-05 x 7850 = 8.889189 kg
Element steel = 31.566723 + 8.889189 = 40.455912 kg
Status: Included
```

### Beam `B1-L2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 3 x 0.000201062 x 6 = 0.003619115 m3; mass = V x rho = 28.410051 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 2 x 0.000201062 x 6 = 0.002412743 m3; mass = V x rho = 18.940034 kg
[2] Hoops (ties): d = 8 mm @ 0.2 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.008 = 0.242 m; h = 0.6 - 2 x 0.025 - 0.008 = 0.542 m; piece = 2 x (w + h) + hook = 2 x (0.242 + 0.542) + 0.16 = 1.728 m
    total length = 30 x 1 x 1.728 = 51.84 m; A_bar = 5.026548e-05 m2; mass = 51.84 x 5.026548e-05 x 7850 = 20.455236 kg
Element steel = 28.410051 + 18.940034 + 20.455236 = 67.805321 kg
Status: Included
```

### Slab `S1-L2`

```text
Host polygon (4 vertices) area in its own plane = 24 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 24 x 0.2 = 4.8 m3
Net volume = (24 - 0) x 0.2 = 4.8 m3
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 80 kg/m3 x net concrete 4.8 m3 = 384 kg (ESTIMATE)
Element steel = 384 kg
Status: Included
```

### Wall `W1-L2`

```text
Host polygon (4 vertices) area in its own plane = 12.8 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (W200)
Gross volume = 12.8 x 0.2 = 2.56 m3
Net volume = (12.8 - 0) x 0.2 = 2.56 m3
Steel: no reinforcement data -> steel UNKNOWN (not zero) (MISSING_REINFORCEMENT)
Status: Included
```

### Column `C1-L3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.2 = 0.512 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.2 m (member length)
    V = 4 x 0.000314159 x 3.2 = 0.004021239 m3; mass = V x rho = 31.566723 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 16 x 1 x 1.408 = 22.528 m; A_bar = 5.026548e-05 m2; mass = 22.528 x 5.026548e-05 x 7850 = 8.889189 kg
Element steel = 31.566723 + 8.889189 = 40.455912 kg
Status: Included
```

### Column `C2-L3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.2 = 0.512 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.2 m (member length)
    V = 4 x 0.000314159 x 3.2 = 0.004021239 m3; mass = V x rho = 31.566723 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 16 x 1 x 1.408 = 22.528 m; A_bar = 5.026548e-05 m2; mass = 22.528 x 5.026548e-05 x 7850 = 8.889189 kg
Element steel = 31.566723 + 8.889189 = 40.455912 kg
Status: Included
```

### Beam `B1-L3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 3 x 0.000201062 x 6 = 0.003619115 m3; mass = V x rho = 28.410051 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 2 x 0.000201062 x 6 = 0.002412743 m3; mass = V x rho = 18.940034 kg
[2] Hoops (ties): d = 8 mm @ 0.2 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.008 = 0.242 m; h = 0.6 - 2 x 0.025 - 0.008 = 0.542 m; piece = 2 x (w + h) + hook = 2 x (0.242 + 0.542) + 0.16 = 1.728 m
    total length = 30 x 1 x 1.728 = 51.84 m; A_bar = 5.026548e-05 m2; mass = 51.84 x 5.026548e-05 x 7850 = 20.455236 kg
Element steel = 28.410051 + 18.940034 + 20.455236 = 67.805321 kg
Status: Included
```

### Slab `S1-L3`

```text
Host polygon (4 vertices) area in its own plane = 24 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 24 x 0.2 = 4.8 m3
Net volume = (24 - 0) x 0.2 = 4.8 m3
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 80 kg/m3 x net concrete 4.8 m3 = 384 kg (ESTIMATE)
Element steel = 384 kg
Status: Included
```

### Wall `W1-L3`

```text
Host polygon (4 vertices) area in its own plane = 12.8 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (W200)
Gross volume = 12.8 x 0.2 = 2.56 m3
Net volume = (12.8 - 0) x 0.2 = 2.56 m3
Steel: no reinforcement data -> steel UNKNOWN (not zero) (MISSING_REINFORCEMENT)
Status: Included
```

### Column `C1-Roof`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.2 = 0.512 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.2 m (member length)
    V = 4 x 0.000314159 x 3.2 = 0.004021239 m3; mass = V x rho = 31.566723 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 16 x 1 x 1.408 = 22.528 m; A_bar = 5.026548e-05 m2; mass = 22.528 x 5.026548e-05 x 7850 = 8.889189 kg
Element steel = 31.566723 + 8.889189 = 40.455912 kg
Status: Included
```

### Column `C2-Roof`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.2^2) = 3.2 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.2 = 0.512 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.2 m (member length)
    V = 4 x 0.000314159 x 3.2 = 0.004021239 m3; mass = V x rho = 31.566723 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.2 m, end offset 0.05 m
    count N = floor((3.2 - 2 x 0.05) / 0.2) + 1 = floor(15.5) + 1 = 16
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 16 x 1 x 1.408 = 22.528 m; A_bar = 5.026548e-05 m2; mass = 22.528 x 5.026548e-05 x 7850 = 8.889189 kg
Element steel = 31.566723 + 8.889189 = 40.455912 kg
Status: Included
```

### Beam `B1-Roof`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 3 x 0.000201062 x 6 = 0.003619115 m3; mass = V x rho = 28.410051 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 2 x 0.000201062 x 6 = 0.002412743 m3; mass = V x rho = 18.940034 kg
[2] Hoops (ties): d = 8 mm @ 0.2 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.008 = 0.242 m; h = 0.6 - 2 x 0.025 - 0.008 = 0.542 m; piece = 2 x (w + h) + hook = 2 x (0.242 + 0.542) + 0.16 = 1.728 m
    total length = 30 x 1 x 1.728 = 51.84 m; A_bar = 5.026548e-05 m2; mass = 51.84 x 5.026548e-05 x 7850 = 20.455236 kg
Element steel = 28.410051 + 18.940034 + 20.455236 = 67.805321 kg
Status: Included
```

### Slab `S1-Roof`

```text
Host polygon (4 vertices) area in its own plane = 24 m2 (shoelace on local u-v coordinates)
Thickness t = 0.15 m (S150)
Gross volume = 24 x 0.15 = 3.6 m3
Net volume = (24 - 0) x 0.15 = 3.6 m3
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 80 kg/m3 x net concrete 3.6 m3 = 288 kg (ESTIMATE)
Element steel = 288 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byStory.L1.netConcreteM3` | 2 × 0.512 + 1.08 + 4.8 + 2.56 = 9.464 | 9.464 |
| `byStory.Roof.netConcreteM3` | 2 × 0.512 + 1.08 + 3.6 = 5.704 | 5.704 |
| `modelTotals.netConcreteM3` | 3 × 9.464 + 5.704 = 34.096 | 34.096 |
| `byElementType.Wall.netConcreteM3` | 3 × 2.56 | 7.68 |
| `byElementType.Column.steelKg` | 8 columns × (4 × A(20) × 3.2 × 7850 + 16 ties × 1.408 m × A(8) × 7850) | 323.647297 |
| `byStory.L2.steelKg` | 2 columns + beam + 80 × 4.8 | 532.717145 |
| `modelTotals.steelKg` | Σ stories | 2034.868581 |
| `byMaterial.C40.netConcreteM3` | all columns | 4.096 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 34.096 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 34.096 m³ |
| Steel (known) | 2034.868581 kg |
| &nbsp;&nbsp;of which ProvidedBars | 594.868581 kg |
| &nbsp;&nbsp;of which EstimatedRatio | 1440 kg |
| Concrete completeness | Complete |
| Steel completeness | Incomplete (unknown: W1-L1, W1-L2, W1-L3) |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| L1 | 9.464 | 9.464 | 532.717145 |
| L2 | 9.464 | 9.464 | 532.717145 |
| L3 | 9.464 | 9.464 | 532.717145 |
| Roof | 5.704 | 5.704 | 436.717145 |

**By concrete material group:** `C40` = 4.096 m³ net, `C30` = 30 m³ net

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `ESTIMATED_REINFORCEMENT` | Warning | `S1-L1` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `MISSING_REINFORCEMENT` | Warning | `W1-L1` | yes | Steel quantification was requested but the element has no reinforcement data. Steel is unknown, not zero. |
| `ESTIMATED_REINFORCEMENT` | Warning | `S1-L2` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `MISSING_REINFORCEMENT` | Warning | `W1-L2` | yes | Steel quantification was requested but the element has no reinforcement data. Steel is unknown, not zero. |
| `ESTIMATED_REINFORCEMENT` | Warning | `S1-L3` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `MISSING_REINFORCEMENT` | Warning | `W1-L3` | yes | Steel quantification was requested but the element has no reinforcement data. Steel is unknown, not zero. |
| `ESTIMATED_REINFORCEMENT` | Warning | `S1-Roof` | no | Steel comes from an estimated ratio, not from bars or design output. |

**Finalization:** BLOCKED by MISSING_REINFORCEMENT

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
