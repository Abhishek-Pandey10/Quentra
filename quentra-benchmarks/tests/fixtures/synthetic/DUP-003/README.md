# DUP-003 — Wall split into mesh objects

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** double-counting · **Fixture set:** synthetic

## Purpose

Wall plus two mesh children.

**Expected Quentra behaviour:** exclude mesh

## A. Structural truth

- W1 6 × 3 × 0.2; two mesh halves

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 3, materials: 1, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 18 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (W200)
Gross volume = 18 x 0.2 = 3.6 m3
Net volume = (18 - 0) x 0.2 = 3.6 m3
Status: Included
```

### Wall `W1~M1`

```text
Status: Excluded - MeshElement of physical element W1; the parent carries the quantity.
```

### Wall `W1~M2`

```text
Status: Excluded - MeshElement of physical element W1; the parent carries the quantity.
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 18 × 0.2 | 3.6 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3.6 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 3.6 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `W1~M1` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `W1~M2` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
