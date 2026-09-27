# DUP-002 — Slab plus mesh elements

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** double-counting · **Fixture set:** synthetic

## Purpose

Physical slab plus its four analysis mesh elements. Mesh elements are excluded; no SLAB_OVERLAP either, because excluded elements do not take part in overlap checks.

**Expected Quentra behaviour:** exclude mesh

## A. Structural truth

- S1 6 × 4 × 0.2
- 4 mesh elements 3 × 2 each, parent S1

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 5, materials: 1, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 24 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 24 x 0.2 = 4.8 m3
Net volume = (24 - 0) x 0.2 = 4.8 m3
Status: Included
```

### Slab `S1~M1`

```text
Status: Excluded - MeshElement of physical element S1; the parent carries the quantity.
```

### Slab `S1~M2`

```text
Status: Excluded - MeshElement of physical element S1; the parent carries the quantity.
```

### Slab `S1~M3`

```text
Status: Excluded - MeshElement of physical element S1; the parent carries the quantity.
```

### Slab `S1~M4`

```text
Status: Excluded - MeshElement of physical element S1; the parent carries the quantity.
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 24 × 0.2 (9.6 would be double counting) | 4.8 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 4.8 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 4.8 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `S1~M1` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `S1~M2` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `S1~M3` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `S1~M4` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
