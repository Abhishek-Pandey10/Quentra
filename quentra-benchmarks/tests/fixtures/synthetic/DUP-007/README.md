# DUP-007 — Drop panel modelled as overlapping slab

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** double-counting · **Fixture set:** synthetic

## Purpose

A drop panel entered as a second slab inside the main slab (same plane). The overlap cannot be resolved automatically.

**Expected Quentra behaviour:** include both, warn, block

## A. Structural truth

- S1 8 × 8 × 0.20
- DP1 2 × 2 × 0.15 fully inside S1

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 1, sections: 5, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Slabs S1 and DP1 overlap by 4 m2 in plan -> SLAB_OVERLAP (both counted).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 64 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 64 x 0.2 = 12.8 m3
Net volume = (64 - 0) x 0.2 = 12.8 m3
Status: Included
```

### Slab `DP1`

```text
Host polygon (4 vertices) area in its own plane = 4 m2 (shoelace on local u-v coordinates)
Thickness t = 0.15 m (S150)
Gross volume = 4 x 0.15 = 0.6 m3
Net volume = (4 - 0) x 0.15 = 0.6 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 12.8 + 0.6 (both counted) | 13.4 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 13.4 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 13.4 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `SLAB_OVERLAP` | Warning | `S1+DP1` | yes | Two slabs on the same story and plane overlap in plan. Both are counted; the overlap is double-counted until resolved. |

**Finalization:** BLOCKED by SLAB_OVERLAP

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
