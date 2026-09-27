# PLAUS-002 — Transfer slab with default threshold

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** plausibility · **Fixture set:** synthetic

## Purpose

Same snapshot as PLAUS-001 with default rules: 2.5 m > 2.0 m → UNREALISTIC_DIMENSION (non-blocking). 2.5/1000 = 0.0025 m is not a typical thickness, so no unit warning.

**Expected Quentra behaviour:** include, warn

## A. Structural truth

- TS1: 8 × 8 m, t = 2.5 m
- Default rules

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `TS1`

```text
Host polygon (4 vertices) area in its own plane = 64 m2 (shoelace on local u-v coordinates)
Thickness t = 2.5 m (TS2500)
Gross volume = 64 x 2.5 = 160 m3
Net volume = (64 - 0) x 2.5 = 160 m3
Plausibility: thickness 2.5 m > 2 m (SLAB_THICKNESS_MAX) -> UNREALISTIC_DIMENSION
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 64 × 2.5 — computed as given | 160 |
| `finalization.allowed` | warning does not block | True |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 160 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 160 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `UNREALISTIC_DIMENSION` | Warning | `TS1` | no | A dimension exceeds a configurable plausibility band. Quantities are still computed as given. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
