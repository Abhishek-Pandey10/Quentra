# INVALID-024 — Beam width = 6 m (plausibility only)

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

Unusual but not impossible (6/1000 = 6 mm is not a typical width, so no unit error). Warning only; finalization allowed.

**Expected Quentra behaviour:** include, warn

## A. Structural truth

- B1 6.0 × 0.6 × 6 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B6000x600: rectangle b x h = 6 m x 0.6 m, A = 3.6 m2
Gross volume = A x L = 3.6 x 6 = 21.6 m3 (no openings: net = gross)
Plausibility: section dimension 6 m > 5 m (BEAM_SECTION_DIM_MAX) -> UNREALISTIC_DIMENSION
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 6 × 0.6 × 6 | 21.6 |
| `finalization.allowed` |  | True |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 21.6 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 21.6 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `UNREALISTIC_DIMENSION` | Warning | `B1` | no | A dimension exceeds a configurable plausibility band. Quantities are still computed as given. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
