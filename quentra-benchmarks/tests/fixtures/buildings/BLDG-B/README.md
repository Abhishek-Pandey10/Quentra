# BLDG-B — Building B — 5-story RC office

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** building · **Fixture set:** buildings

## Purpose

Five-story office with column size/grade step at L3, circular corner columns, a four-wall core with a door, slabs with core and stair openings, estimated slab steel and simulated required-area beams at L4–L5.

**Expected Quentra behaviour:** include all; estimated + required steel flagged

## A. Structural truth

- Grid 4 × 3 bays: x = 0…30 m @ 7.5, y = 0…18 m @ 6; L1 4.0 m, L2–L5 3.5 m
- Columns: L1–L2 0.6 × 0.6 C45 12T25; L3–L5 0.5 × 0.5 C40 8T20; corners circular D 0.6 8T20; all ties T10@150
- Primary X-beams 0.35 × 0.7 (C35): L1–L3 provided 4T20 + 3T20 + T10@150; L4–L5 simulated required areas (trapezoidal)
- Secondary Y-beams 0.3 × 0.6 (C35): 3T16 + 2T16 + T10@200
- Slabs 30 × 18 × 0.22 (C30), core 4.5 × 3 and stair 3 × 4 openings, estimated 90 kg/m³
- Core walls 0.25 (C40) on x 9–13.5, y 7.5–10.5 with a 1.0 × 2.2 m door in the south wall; T12/T10@200 both faces

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 280, materials: 5, sections: 7, design results: 32.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

_This model has 280 elements; the per-element working is shown for the first element of each type/section combination. Every element follows the same formulas; totals are cross-checked against the closed-form hand check below._

### Column `C-0-0-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 4^2) = 4 m
Section CC600: circle D = 0.6 m, A = pi x D^2 / 4 = 0.282743 m2
Gross volume = A x L = 0.282743 x 4 = 1.130973 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 4 m (member length)
    V = 8 x 0.000314159 x 4 = 0.010053096 m3; mass = V x rho = 78.916807 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 4 m, end offset 0.05 m
    count N = floor((4 - 2 x 0.05) / 0.15) + 1 = floor(26) + 1 = 27
    centreline Dc = D - 2c - d = 0.6 - 2 x 0.04 - 0.01 = 0.51 m; piece = pi x Dc + hook = 1.802212 m
    total length = 27 x 1 x 1.802212 = 48.659731 m; A_bar = 7.853982e-05 m2; mass = 48.659731 x 7.853982e-05 x 7850 = 30.000552 kg
Element steel = 78.916807 + 30.000552 = 108.917359 kg
Status: Included
```

### Column `C-0-6-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 4^2) = 4 m
Section C600: rectangle b x h = 0.6 m x 0.6 m, A = 0.36 m2
Gross volume = A x L = 0.36 x 4 = 1.44 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 12 bars, d = 25 mm, A_bar = pi x 0.025^2 / 4 = 0.000490874 m2; L = 4 m (member length)
    V = 12 x 0.000490874 x 4 = 0.023561945 m3; mass = V x rho = 184.961267 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 4 m, end offset 0.05 m
    count N = floor((4 - 2 x 0.05) / 0.15) + 1 = floor(26) + 1 = 27
    centreline w = b - 2c - d = 0.6 - 2 x 0.04 - 0.01 = 0.51 m; h = 0.6 - 2 x 0.04 - 0.01 = 0.51 m; piece = 2 x (w + h) + hook = 2 x (0.51 + 0.51) + 0.2 = 2.24 m
    total length = 27 x 1 x 2.24 = 60.48 m; A_bar = 7.853982e-05 m2; mass = 60.48 x 7.853982e-05 x 7850 = 37.288192 kg
Element steel = 184.961267 + 37.288192 = 222.249459 kg
Status: Included
```

### Beam `PB-0-0-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(7.5^2 + 0^2 + 0^2) = 7.5 m
Section PB350x700: rectangle b x h = 0.35 m x 0.7 m, A = 0.245 m2
Gross volume = A x L = 0.245 x 7.5 = 1.8375 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 7.5 m (member length)
    V = 4 x 0.000314159 x 7.5 = 0.009424778 m3; mass = V x rho = 73.984507 kg
[1] StraightBars (top): 3 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 7.5 m (member length)
    V = 3 x 0.000314159 x 7.5 = 0.007068583 m3; mass = V x rho = 55.48838 kg
[2] Hoops (ties): d = 10 mm @ 0.15 m over zone 7.5 m, end offset 0.05 m
    count N = floor((7.5 - 2 x 0.05) / 0.15) + 1 = floor(49.333333) + 1 = 50
    centreline w = b - 2c - d = 0.35 - 2 x 0.025 - 0.01 = 0.29 m; h = 0.7 - 2 x 0.025 - 0.01 = 0.64 m; piece = 2 x (w + h) + hook = 2 x (0.29 + 0.64) + 0.2 = 2.06 m
    total length = 50 x 1 x 2.06 = 103 m; A_bar = 7.853982e-05 m2; mass = 103 x 7.853982e-05 x 7850 = 63.503369 kg
Element steel = 73.984507 + 55.48838 + 63.503369 = 192.976256 kg
Status: Included
```

### Beam `SB-0-0-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 6^2 + 0^2) = 6 m
Section SB300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (bottom): 3 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 3 x 0.000201062 x 6 = 0.003619115 m3; mass = V x rho = 28.410051 kg
[1] StraightBars (top): 2 bars, d = 16 mm, A_bar = pi x 0.016^2 / 4 = 0.000201062 m2; L = 6 m (member length)
    V = 2 x 0.000201062 x 6 = 0.002412743 m3; mass = V x rho = 18.940034 kg
[2] Hoops (ties): d = 10 mm @ 0.2 m over zone 6 m, end offset 0.05 m
    count N = floor((6 - 2 x 0.05) / 0.2) + 1 = floor(29.5) + 1 = 30
    centreline w = b - 2c - d = 0.3 - 2 x 0.025 - 0.01 = 0.24 m; h = 0.6 - 2 x 0.025 - 0.01 = 0.54 m; piece = 2 x (w + h) + hook = 2 x (0.24 + 0.54) + 0.2 = 1.76 m
    total length = 30 x 1 x 1.76 = 52.8 m; A_bar = 7.853982e-05 m2; mass = 52.8 x 7.853982e-05 x 7850 = 32.553183 kg
Element steel = 28.410051 + 18.940034 + 32.553183 = 79.903268 kg
Status: Included
```

### Slab `SLAB-L1`

```text
Host polygon (4 vertices) area in its own plane = 540 m2 (shoelace on local u-v coordinates)
Opening CORE: area 13.5 m2, fully inside the host
Opening STAIR: area 12 m2, fully inside the host
Sum of opening areas = 25.5 m2; area of (union of openings) inside host = 25.5 m2
Thickness t = 0.22 m (S220)
Gross volume = 540 x 0.22 = 118.8 m3
Net volume = (540 - 25.5) x 0.22 = 113.19 m3
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 90 kg/m3 x net concrete 113.19 m3 = 10187.1 kg (ESTIMATE)
Element steel = 10187.1 kg
Status: Included
```

### Wall `CW-S-L1`

```text
Host polygon (4 vertices) area in its own plane = 18 m2 (shoelace on local u-v coordinates)
Opening DOOR: area 2.2 m2, fully inside the host
Sum of opening areas = 2.2 m2; area of (union of openings) inside host = 2.2 m2
Thickness t = 0.25 m (CW250)
Gross volume = 18 x 0.25 = 4.5 m3
Net volume = (18 - 2.2) x 0.25 = 3.95 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along Vertical, d = 12 mm @ 0.2 m, host rectangle (wall-local): span along = 4 m, span across = 4.5 m, faces = 2
    bars N = floor((4.5 - 2 x 0.05) / 0.2) + 1 = floor(22) + 1 = 23; bar length = 4 - 2 x 0.025 = 3.95 m
    total length = 2 x 23 x 3.95 = 181.7 m; mass = 181.7 x 0.000113097 x 7850 = 161.315819 kg
[1] Mesh (bottom): bars along Horizontal, d = 10 mm @ 0.2 m, host rectangle (wall-local): span along = 4.5 m, span across = 4 m, faces = 2
    bars N = floor((4 - 2 x 0.05) / 0.2) + 1 = floor(19.5) + 1 = 20; bar length = 4.5 - 2 x 0.025 = 4.45 m
    total length = 2 x 20 x 4.45 = 178 m; mass = 178 x 7.853982e-05 x 7850 = 109.743685 kg
Element steel = 161.315819 + 109.743685 = 271.059504 kg
Status: Included
```

### Column `C-0-6-L3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C500: rectangle b x h = 0.5 m x 0.5 m, A = 0.25 m2
Gross volume = A x L = 0.25 x 3.5 = 0.875 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 8 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 8 x 0.000314159 x 3.5 = 0.008796459 m3; mass = V x rho = 69.052207 kg
[1] Hoops (ties): d = 10 mm @ 0.15 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.15) + 1 = floor(22.666667) + 1 = 23
    centreline w = b - 2c - d = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; h = 0.5 - 2 x 0.04 - 0.01 = 0.41 m; piece = 2 x (w + h) + hook = 2 x (0.41 + 0.41) + 0.2 = 1.84 m
    total length = 23 x 1 x 1.84 = 42.32 m; A_bar = 7.853982e-05 m2; mass = 42.32 x 7.853982e-05 x 7850 = 26.091869 kg
Element steel = 69.052207 + 26.091869 = 95.144076 kg
Status: Included
```

### Beam `PB-0-0-L4`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(7.5^2 + 0^2 + 0^2) = 7.5 m
Section PB350x700: rectangle b x h = 0.35 m x 0.7 m, A = 0.245 m2
Gross volume = A x L = 0.245 x 7.5 = 1.8375 m3 (no openings: net = gross)
Steel source: RequiredDesignArea (exact = False); density rho = 7850 kg/m3
[0] RequiredArea: envelope = TrapezoidalIntegration; stations x = [0, 1.875, 3.75, 5.625, 7.5] m
    top required  (m2) = [0.0016, 0.0006, 0.0003, 0.0006, 0.0016] -> integral = 0.0058125 m3
    bottom required (m2) = [0.0005, 0.0011, 0.0014, 0.0011, 0.0005] -> integral = 0.0076875 m3
    shear Av/s (m2/m) = [0.001, 0.0006, 0.0004, 0.0006, 0.001] -> integral = 0.004875 m2 of leg area
    shear steel volume = 0.004875 x hoopLength / legs = 0.004875 x 2.14 / 2 = 0.00521625 m3
    V = 0.0058125 + 0.0076875 + 0.00521625 = 0.01871625 m3; mass = 0.01871625 x 7850 = 146.922562 kg
Element steel = 146.922562 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | closed-form Σ over element groups | 964.25752 |
| `byStory.L1.netConcreteM3` | closed form | 200.803893 |
| `byStory.L2.netConcreteM3` | closed form | 195.483407 |
| `byStory.L3.netConcreteM3` | closed form | 189.323407 |
| `byStory.L4.netConcreteM3` | closed form | 189.323407 |
| `byStory.L5.netConcreteM3` | closed form | 189.323407 |
| `byElementType.Beam.netConcreteM3` | closed form | 228 |
| `byElementType.Column.netConcreteM3` | closed form | 105.55752 |
| `byElementType.Slab.netConcreteM3` | closed form | 565.95 |
| `byElementType.Wall.netConcreteM3` | closed form | 64.75 |
| `modelTotals.steelKg` | closed-form Σ of bar formulas | 88136.894944 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 995.05752 m³ |
| Opening deduction | 30.8 m³ |
| Net concrete | 964.25752 m³ |
| Steel (known) | 88136.894944 kg |
| &nbsp;&nbsp;of which ProvidedBars | 32499.872944 kg |
| &nbsp;&nbsp;of which RequiredDesignArea | 4701.522 kg |
| &nbsp;&nbsp;of which EstimatedRatio | 50935.5 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| L1 | 206.963893 | 200.803893 | 19362.963696 |
| L2 | 201.643407 | 195.483407 | 18743.444934 |
| L3 | 195.483407 | 189.323407 | 17168.068166 |
| L4 | 195.483407 | 189.323407 | 16431.209074 |
| L5 | 195.483407 | 189.323407 | 16431.209074 |

**By concrete material group:** `C45` = 51.6823 m³ net, `C35` = 228 m³ net, `C30` = 565.95 m³ net, `C40` = 118.62522 m³ net

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `ESTIMATED_REINFORCEMENT` | Warning | `SLAB-L1` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `ESTIMATED_REINFORCEMENT` | Warning | `SLAB-L2` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `ESTIMATED_REINFORCEMENT` | Warning | `SLAB-L3` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `REQUIRED_AREA_USED` | Info | `PB-0-0-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-0-1-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-0-2-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-0-3-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-6-0-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-6-1-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-6-2-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-6-3-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-12-0-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-12-1-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-12-2-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-12-3-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-18-0-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-18-1-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-18-2-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-18-3-L4` | no | Steel was derived from required design areas, not from provided bars. |
| `ESTIMATED_REINFORCEMENT` | Warning | `SLAB-L4` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `REQUIRED_AREA_USED` | Info | `PB-0-0-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-0-1-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-0-2-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-0-3-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-6-0-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-6-1-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-6-2-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-6-3-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-12-0-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-12-1-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-12-2-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-12-3-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-18-0-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-18-1-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-18-2-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `REQUIRED_AREA_USED` | Info | `PB-18-3-L5` | no | Steel was derived from required design areas, not from provided bars. |
| `ESTIMATED_REINFORCEMENT` | Warning | `SLAB-L5` | no | Steel comes from an estimated ratio, not from bars or design output. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
