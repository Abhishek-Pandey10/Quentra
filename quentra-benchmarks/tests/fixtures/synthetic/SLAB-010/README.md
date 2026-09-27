# SLAB-010 — Two slabs overlapping incorrectly

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

Modelling error: two slabs overlap by 2 m × 4 m. Quentra must not silently merge or trim them; it counts both and blocks finalization.

**Expected Quentra behaviour:** include both, warn, block finalization

## A. Structural truth

- S1: x 0–6, S2: x 4–10, both y 0–4, t = 0.20
- Overlap x 4–6: 8 m² (1.6 m³ double counted)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Slabs S1 and S2 overlap by 8 m2 in plan -> SLAB_OVERLAP (both counted).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 24 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 24 x 0.2 = 4.8 m3
Net volume = (24 - 0) x 0.2 = 4.8 m3
Status: Included
```

### Slab `S2`

```text
Host polygon (4 vertices) area in its own plane = 24 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 24 x 0.2 = 4.8 m3
Net volume = (24 - 0) x 0.2 = 4.8 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | (24 + 24) × 0.2 = 9.6 (both counted) | 9.6 |
| `finalization.allowed` | SLAB_OVERLAP blocks | False |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 9.6 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 9.6 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `SLAB_OVERLAP` | Warning | `S1+S2` | yes | Two slabs on the same story and plane overlap in plan. Both are counted; the overlap is double-counted until resolved. |

**Finalization:** BLOCKED by SLAB_OVERLAP

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
