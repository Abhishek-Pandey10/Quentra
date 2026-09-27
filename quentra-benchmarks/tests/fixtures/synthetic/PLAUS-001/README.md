# PLAUS-001 — Transfer slab with overridden threshold

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** plausibility · **Fixture set:** synthetic

## Purpose

A genuine 2.5 m transfer slab. The project raises SLAB_THICKNESS_MAX to 3.0 m, so no warning is expected. Demonstrates that plausibility bands are configuration, not law.

**Expected Quentra behaviour:** include, no warning

## A. Structural truth

- TS1: 8 × 8 m, t = 2.5 m
- Run option: SLAB_THICKNESS_MAX threshold = 3.0 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false,"plausibility":{"SLAB_THICKNESS_MAX":{"threshold":3.0}}}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `TS1`

```text
Host polygon (4 vertices) area in its own plane = 64 m2 (shoelace on local u-v coordinates)
Thickness t = 2.5 m (TS2500)
Gross volume = 64 x 2.5 = 160 m3
Net volume = (64 - 0) x 2.5 = 160 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 64 × 2.5 | 160 |

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

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
