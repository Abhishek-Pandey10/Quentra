# RSRC-002 — Source EstimatedRatio

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** source · **Fixture set:** synthetic

## Purpose

Steel estimated as 120 kg per m³ of net concrete. Must be flagged approximate.

**Expected Quentra behaviour:** approximate, warn

## A. Structural truth

- B1 0.3 × 0.6 × 6 (1.08 m³)
- rate 120 kg/m³

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 120 kg/m3 x net concrete 1.08 m3 = 129.6 kg (ESTIMATE)
Element steel = 129.6 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.steelKg` | 120 × 1.08 = 129.6 | 129.6 |
| `elements.B1.steelExact` |  | False |
| `modelTotals.steelKgBySource.EstimatedRatio` |  | 129.6 |
| `modelTotals.steelExactKg` |  | 0 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 129.6 kg |
| &nbsp;&nbsp;of which EstimatedRatio | 129.6 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `ESTIMATED_REINFORCEMENT` | Warning | `B1` | no | Steel comes from an estimated ratio, not from bars or design output. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
