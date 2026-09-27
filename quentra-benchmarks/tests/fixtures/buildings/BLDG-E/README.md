# BLDG-E — Building E — model with intentional data problems

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** building · **Fixture set:** buildings

## Purpose

A clean 2-story frame with fourteen injected defects. Each defect has a documented, deterministic outcome; finalization is blocked. Totals = clean frame + the defects that are counted by rule.

**Expected Quentra behaviour:** per-defect rules; block finalization

## A. Structural truth

- Clean frame: 2 stories × (9 columns 0.4 × 0.4 C40 + 12 beams 0.3 × 0.6 × 6 C30), all provided bars
- Defects: slab opening half outside (clipped); L2 slab 300 m thick (counted as given + UNIT_PLAUSIBILITY); wall without reinforcement; two analytical children (excluded); orphan analytical (rejected); identical duplicate column (excluded); conflicting duplicate ID ×2 (rejected); duplicate geometry B-GHOST (counted + warning); unknown material (unclassified); ambiguous material (unclassified); missing story (<unassigned>); stale design result (counted + blocked); unsupported Link (skipped)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 57, materials: 4, sections: 5, design results: 1.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.
- Elements BY012-L2 and B-GHOST have identical geometry -> DUPLICATE_GEOMETRY (both counted).

_This model has 57 elements; the per-element working is shown for the first element of each type/section combination. Every element follows the same formulas; totals are cross-checked against the closed-form hand check below._

### Column `C00-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3.5 = 0.56 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 3.5 m (member length)
    V = 4 x 0.000314159 x 3.5 = 0.00439823 m3; mass = V x rho = 34.526103 kg
[1] Hoops (ties): d = 8 mm @ 0.2 m over zone 3.5 m, end offset 0.05 m
    count N = floor((3.5 - 2 x 0.05) / 0.2) + 1 = floor(17) + 1 = 18
    centreline w = b - 2c - d = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; h = 0.4 - 2 x 0.04 - 0.008 = 0.312 m; piece = 2 x (w + h) + hook = 2 x (0.312 + 0.312) + 0.16 = 1.408 m
    total length = 18 x 1 x 1.408 = 25.344 m; A_bar = 5.026548e-05 m2; mass = 25.344 x 5.026548e-05 x 7850 = 10.000338 kg
Element steel = 34.526103 + 10.000338 = 44.526441 kg
Status: Included
```

### Beam `BX00-L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
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

### Slab `S-L1`

```text
Host polygon (4 vertices) area in its own plane = 144 m2 (shoelace on local u-v coordinates)
Opening OP-EDGE: area 4 m2, only 2 m2 inside the host -> clipped (OPENING_CLIPPED)
Sum of opening areas = 4 m2; area of (union of openings) inside host = 2 m2
Thickness t = 0.2 m (S200)
Gross volume = 144 x 0.2 = 28.8 m3
Net volume = (144 - 2) x 0.2 = 28.4 m3
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 85 kg/m3 x net concrete 28.4 m3 = 2414 kg (ESTIMATE)
Element steel = 2414 kg
Status: Included
```

### Slab `S-L2`

```text
Host polygon (4 vertices) area in its own plane = 144 m2 (shoelace on local u-v coordinates)
Thickness t = 300 m (S300M)
Gross volume = 144 x 300 = 43200 m3
Net volume = (144 - 0) x 300 = 43200 m3
Plausibility: thickness 300 m > 2 m (SLAB_THICKNESS_MAX) -> UNREALISTIC_DIMENSION
Plausibility: 300/1000 = 0.3 m would be a typical thickness -> probable unit error (UNIT_PLAUSIBILITY). Value is NOT rescaled.
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 85 kg/m3 x net concrete 43200 m3 = 3.672000e+06 kg (ESTIMATE)
Element steel = 3.672000e+06 kg
Status: Included
```

### Wall `W-L1`

```text
Host polygon (4 vertices) area in its own plane = 28 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (W200)
Gross volume = 28 x 0.2 = 5.6 m3
Net volume = (28 - 0) x 0.2 = 5.6 m3
Steel: no reinforcement data -> steel UNKNOWN (not zero) (MISSING_REINFORCEMENT)
Status: Included
```

### Beam `BX00-L1~seg1`

```text
Status: Excluded - AnalyticalSegment of physical element BX00-L1; the parent carries the quantity.
```

### Beam `BX99-L2~seg1`

```text
Status: Rejected - AnalyticalSegment whose parent 'BX99-L2' does not exist.
```

### Column `C00-L1` (copy 2)

```text
Status: Excluded - identical copy of an earlier element with the same ID (DUPLICATE_ELEMENT); only the first copy is counted.
```

### Beam `B-UNK`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Unclassified - material 'C35-UNDEFINED' is not defined; the volume is reported but not counted as concrete.
```

### Beam `B-STALE`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: RequiredDesignArea (exact = False); density rho = 7850 kg/m3
Design result 'B-STALE' is flagged Stale (STALE_DESIGN_RESULT) - used, but finalization is blocked.
[0] RequiredArea: envelope = MaxOfStations; stations x = [0, 3, 6] m
    top required  (m2) = [0.0009, 0.0003, 0.0009] -> integral = 0.0054 m3
    bottom required (m2) = [0.0003, 0.0008, 0.0003] -> integral = 0.0048 m3
    shear Av/s (m2/m) = [0.0008, 0.0004, 0.0008] -> integral = 0.0048 m2 of leg area
    shear steel volume = 0.0048 x hoopLength / legs = 0.0048 x 1.76 / 2 = 0.004224 m3
    V = 0.0054 + 0.0048 + 0.004224 = 0.014424 m3; mass = 0.014424 x 7850 = 113.2284 kg
Element steel = 113.2284 kg
Status: Included
```

### Link `LINK-1`

```text
Status: Skipped - element type 'Link' is outside the quantity scope.
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | clean frame + S-L1 (142 × 0.2) + S-L2 (144 × 300) + wall (8 × 3.5 × 0.2) + B-GHOST + B-NOSTORY + B-STALE | 43273.24 |
| `modelTotals.unclassifiedVolumeM3` | B-UNK + B-AMB | 2.16 |
| `byStory.<unassigned>.netConcreteM3` | B-NOSTORY | 1.08 |
| `modelTotals.steelKg` | clean bars + slab estimates + B-GHOST + B-NOSTORY + B-STALE required | 3.677406e+06 |
| `finalization.allowed` | multiple blocking codes | False |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 43273.64 m³ |
| Opening deduction | 0.4 m³ |
| Net concrete | 43273.24 m³ |
| Unclassified volume (not concrete) | 2.16 m³ |
| Steel (known) | 3.677406e+06 kg |
| &nbsp;&nbsp;of which ProvidedBars | 2878.960896 kg |
| &nbsp;&nbsp;of which RequiredDesignArea | 113.2284 kg |
| &nbsp;&nbsp;of which EstimatedRatio | 3.674414e+06 kg |
| Concrete completeness | Incomplete (unknown: BX99-L2~seg1, DUPE-1, B-UNK, B-AMB) |
| Steel completeness | Incomplete (unknown: W-L1, BX99-L2~seg1, DUPE-1, B-UNK, B-AMB) |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| L1 | 52.4 | 52 | 3773.577181 |
| L2 | 43220.16 | 43220.16 | 3.673553e+06 |
| <unassigned> | 1.08 | 1.08 | 79.903268 |

**By concrete material group:** `C40` = 10.08 m³ net, `C30` = 43263.16 m³ net

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `DUPLICATE_ELEMENT` | Warning | `C00-L1` | no | An element appears more than once with identical content. The first copy is kept, later copies are excluded. |
| `DUPLICATE_ELEMENT_ID` | Error | `DUPE-1` | yes | Two or more elements share an ID but differ in content. All copies are rejected because the true object cannot be determined. |
| `OPENING_CLIPPED` | Warning | `S-L1/OP-EDGE` | no | An opening lies partly outside its host. Only the part inside the host is deducted. |
| `ESTIMATED_REINFORCEMENT` | Warning | `S-L1` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `UNREALISTIC_DIMENSION` | Warning | `S-L2` | no | A dimension exceeds a configurable plausibility band. Quantities are still computed as given. |
| `UNIT_PLAUSIBILITY` | Error | `S-L2` | yes | A dimension is implausibly large and would be typical if divided by 1000 - probably a unit error. The value is NOT auto-corrected. |
| `ESTIMATED_REINFORCEMENT` | Warning | `S-L2` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `MISSING_REINFORCEMENT` | Warning | `W-L1` | yes | Steel quantification was requested but the element has no reinforcement data. Steel is unknown, not zero. |
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `BX00-L1~seg1` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `BX00-L1~seg2` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |
| `ORPHAN_ANALYTICAL_ELEMENT` | Error | `BX99-L2~seg1` | yes | An analytical/mesh child references a parent that does not exist. |
| `UNKNOWN_MATERIAL` | Error | `B-UNK` | yes | The element or its reinforcement references a material not defined in the snapshot. |
| `MATERIAL_TYPE_AMBIGUOUS` | Error | `B-AMB` | yes | The material exists but its type is not Concrete, Steel or Rebar, so the element cannot be classified. |
| `STORY_REFERENCE_MISSING` | Error | `B-NOSTORY` | yes | The element has no story. It is reported under '<unassigned>'. |
| `STALE_DESIGN_RESULT` | Warning | `B-STALE` | yes | The design result used for required reinforcement is flagged stale (model changed after design). |
| `REQUIRED_AREA_USED` | Info | `B-STALE` | no | Steel was derived from required design areas, not from provided bars. |
| `UNSUPPORTED_ELEMENT_TYPE` | Warning | `LINK-1` | no | The element type is outside the quantity scope (e.g. Tendon, Link). It is skipped. |
| `DUPLICATE_GEOMETRY` | Warning | `BY012-L2+B-GHOST` | yes | Two elements with different IDs have identical type, section and geometry. Both are counted until resolved. |

**Finalization:** BLOCKED by DUPLICATE_ELEMENT_ID, DUPLICATE_GEOMETRY, MATERIAL_TYPE_AMBIGUOUS, MISSING_REINFORCEMENT, ORPHAN_ANALYTICAL_ELEMENT, STALE_DESIGN_RESULT, STORY_REFERENCE_MISSING, UNIT_PLAUSIBILITY, UNKNOWN_MATERIAL

## Notes and assumptions

- The 300 m slab produces an absurd but *as-given* volume; the whole point is that Quentra flags it instead of silently rescaling or silently accepting it.

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
