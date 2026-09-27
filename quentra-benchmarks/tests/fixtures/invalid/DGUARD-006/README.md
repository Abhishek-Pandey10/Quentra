# DGUARD-006 — Negative station

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

Regression for design-result validation; no live ETABS data.

**Expected Quentra behaviour:** steel unknown; block finalization

## A. Structural truth

- Negative station

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
Steel: component 0: station positions and demands must be finite and nonnegative -> steel unknown (INVALID_DESIGN_RESULT)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `finalization.allowed` | Invalid design evidence cannot be finalized | False |
| `elements.B1.steelKg` | Untrusted demand is unknown, never zero or a negative mass | None |
| `modelTotals.netConcreteM3` | 0.3 × 0.6 × 6; invalid design evidence does not invalidate geometry | 1.08 |

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
| `INVALID_DESIGN_RESULT` | Error | `B1` | yes | Design results have invalid/nonfinite/negative demands, duplicate stations, ambiguous result records or conversion overflow. |

**Finalization:** BLOCKED by INVALID_DESIGN_RESULT

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
