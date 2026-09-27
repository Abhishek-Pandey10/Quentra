# SLAB-009 — Two slabs sharing an edge

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

Adjacent slabs touch along x = 5. Touching is not overlapping: no warning.

**Expected Quentra behaviour:** include both, no warning

## A. Structural truth

- S1: x 0–5, y 0–4
- S2: x 5–9, y 0–4
- t = 0.20 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 20 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 20 x 0.2 = 4 m3
Net volume = (20 - 0) x 0.2 = 4 m3
Status: Included
```

### Slab `S2`

```text
Host polygon (4 vertices) area in its own plane = 16 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 16 x 0.2 = 3.2 m3
Net volume = (16 - 0) x 0.2 = 3.2 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | (20 + 16) × 0.2 = 7.2 | 7.2 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 7.2 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 7.2 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
