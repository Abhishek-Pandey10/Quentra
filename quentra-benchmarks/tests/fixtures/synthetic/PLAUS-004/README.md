# PLAUS-004 — Impossible steel rate

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** plausibility · **Fixture set:** synthetic

## Purpose

Manual override of 20 000 kg in 1.08 m³ of concrete = 18 519 kg/m³ > 7850 kg/m³. Physically impossible: the value is discarded (steel unknown), finalization blocked.

**Expected Quentra behaviour:** error, block finalization

## A. Structural truth

- B1: 1.08 m³, manual steel 20 000 kg

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
Steel source: ManualOverride (exact = True)
[0] FixedMass: manual override mass = 20000 kg
Steel rate = 20000 / 1.08 = 18518.518519 kg/m3 > steel density 7850 kg/m3 -> physically impossible, steel discarded (UNREALISTIC_STEEL_RATE)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelKg` | discarded | None |
| `modelTotals.steelKg` | known steel only | 0 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 0 kg |
| Concrete completeness | Complete |
| Steel completeness | Incomplete (unknown: B1) |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `UNREALISTIC_STEEL_RATE` | Error | `B1` | yes | Element steel mass per m3 of concrete exceeds the density of steel - physically impossible. The steel value is discarded (unknown). |

**Finalization:** BLOCKED by UNREALISTIC_STEEL_RATE

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
