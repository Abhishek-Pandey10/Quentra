# RREQ-001 — Required area — max of stations

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** required · **Fixture set:** synthetic

## Purpose

Required design areas converted to steel with the **MaxOfStations** envelope. The envelope is named in the snapshot component; Quentra must use exactly that method.

**Expected Quentra behaviour:** approximate steel, info warning

## A. Structural truth

- B1: 0.3 × 0.6 × 6 m
- Simulated required areas at stations 0, 1.5, 3.0, 4.5, 6.0 m:
-   top (mm²): 1200, 450, 200, 450, 1200 — hogging at supports
-   bottom (mm²): 300, 800, 1100, 800, 300 — sagging mid-span
-   shear Av/s (mm²/m): 1200, 600, 400, 600, 1200
- Shear steel conversion: hoop length 1.76 m per 2 legs (see RSTIR-001)
- These are SIMULATED design results (origin = 'simulated'), not ETABS output.

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 1.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: RequiredDesignArea (exact = False); density rho = 7850 kg/m3
[0] RequiredArea: envelope = MaxOfStations; stations x = [0, 1.5, 3, 4.5, 6] m
    top required  (m2) = [0.0012, 0.00045, 0.0002, 0.00045, 0.0012] -> integral = 0.0072 m3
    bottom required (m2) = [0.0003, 0.0008, 0.0011, 0.0008, 0.0003] -> integral = 0.0066 m3
    shear Av/s (m2/m) = [0.0012, 0.0006, 0.0004, 0.0006, 0.0012] -> integral = 0.0072 m2 of leg area
    shear steel volume = 0.0072 x hoopLength / legs = 0.0072 x 1.76 / 2 = 0.006336 m3
    V = 0.0072 + 0.0066 + 0.006336 = 0.020136 m3; mass = 0.020136 x 7850 = 158.0676 kg
Element steel = 158.0676 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.steelKg` | (1200 + 1100) mm² × 6 m + 1200 mm²/m × 6 m × 1.76/2 = 0.0138 + 0.006336 = 0.020136 m³ → × 7850 | 158.0676 |
| `elements.B1.steelExact` | required ≠ provided | False |
| `modelTotals.steelKgBySource.RequiredDesignArea` |  | 158.0676 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 158.0676 kg |
| &nbsp;&nbsp;of which RequiredDesignArea | 158.0676 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `REQUIRED_AREA_USED` | Info | `B1` | no | Steel was derived from required design areas, not from provided bars. |

**Finalization:** allowed

## Notes and assumptions

- MaxOfStations ≥ SegmentStepMax ≥ TrapezoidalIntegration always holds for the same data; the three cases together pin the method.

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
