# RSRC-005 — Mixed reinforcement sources

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** source · **Fixture set:** synthetic

## Purpose

Five beams, one per source category. Totals by source must be kept apart; exact and approximate subtotals must add up to the known steel.

**Expected Quentra behaviour:** separate by source; B5 unknown

## A. Structural truth

- B1 ProvidedBars 4T20
- B2 EstimatedRatio 120 kg/m³
- B3 ManualOverride 150 kg
- B4 RequiredDesignArea (max-of-stations, flexure only)
- B5 no reinforcement

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 5, materials: 2, sections: 1, design results: 1.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 4 x 0.000314159 x 6 = 0.007539822 m3; mass = V x rho = 59.187606 kg
Element steel = 59.187606 kg
Status: Included
```

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 120 kg/m3 x net concrete 1.08 m3 = 129.6 kg (ESTIMATE)
Element steel = 129.6 kg
Status: Included
```

### Beam `B3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: ManualOverride (exact = True)
[0] FixedMass: manual override mass = 150 kg
Element steel = 150 kg
Status: Included
```

### Beam `B4`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: RequiredDesignArea (exact = False); density rho = 7850 kg/m3
[0] RequiredArea: envelope = MaxOfStations; stations x = [0, 3, 6] m
    top required  (m2) = [0.001, 0.0005, 0.001] -> integral = 0.006 m3
    bottom required (m2) = [0.0005, 0.001, 0.0005] -> integral = 0.006 m3
    V = 0.006 + 0.006 + 0 = 0.012 m3; mass = 0.012 x 7850 = 94.2 kg
Element steel = 94.2 kg
Status: Included
```

### Beam `B5`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel: no reinforcement data -> steel UNKNOWN (not zero) (MISSING_REINFORCEMENT)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.steelKgBySource.ProvidedBars` | 4 × A(20) × 6 × 7850 | 59.187606 |
| `modelTotals.steelKgBySource.EstimatedRatio` | 120 × 1.08 | 129.6 |
| `modelTotals.steelKgBySource.ManualOverride` |  | 150 |
| `modelTotals.steelKgBySource.RequiredDesignArea` | (max top 1000 mm² + max bottom 1000 mm²) × 6 m × 7850 | 94.2 |
| `modelTotals.steelExactKg` | ProvidedBars + ManualOverride | 209.187606 |
| `modelTotals.steelApproximateKg` | EstimatedRatio + RequiredDesignArea | 223.8 |
| `completeness.steelUnknownElementIds` | B5 unknown | ['B5'] |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 5.4 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 5.4 m³ |
| Steel (known) | 432.987606 kg |
| &nbsp;&nbsp;of which ProvidedBars | 59.187606 kg |
| &nbsp;&nbsp;of which RequiredDesignArea | 94.2 kg |
| &nbsp;&nbsp;of which EstimatedRatio | 129.6 kg |
| &nbsp;&nbsp;of which ManualOverride | 150 kg |
| Concrete completeness | Complete |
| Steel completeness | Incomplete (unknown: B5) |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `ESTIMATED_REINFORCEMENT` | Warning | `B2` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `REQUIRED_AREA_USED` | Info | `B4` | no | Steel was derived from required design areas, not from provided bars. |
| `MISSING_REINFORCEMENT` | Warning | `B5` | yes | Steel quantification was requested but the element has no reinforcement data. Steel is unknown, not zero. |

**Finalization:** BLOCKED by MISSING_REINFORCEMENT

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
