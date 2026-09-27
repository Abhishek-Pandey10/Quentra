# MAT-001 — Multiple concrete grades

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** material · **Fixture set:** synthetic

## Purpose

Beam, column and slab in three grades; totals must be grouped by material.

**Expected Quentra behaviour:** group by material

## A. Structural truth

- B1 C30: 0.3 × 0.6 × 6
- C1 C40: 0.4 × 0.4 × 3
- S1 C35: 6 × 5 × 0.2

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 3, materials: 3, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C400x400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3 = 0.48 m3 (no openings: net = gross)
Status: Included
```

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 30 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 30 x 0.2 = 6 m3
Net volume = (30 - 0) x 0.2 = 6 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byMaterial.C30.netConcreteM3` | 0.3 × 0.6 × 6 | 1.08 |
| `byMaterial.C40.netConcreteM3` | 0.4 × 0.4 × 3 | 0.48 |
| `byMaterial.C35.netConcreteM3` | 6 × 5 × 0.2 | 6 |
| `modelTotals.netConcreteM3` | 1.08 + 0.48 + 6.0 | 7.56 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 7.56 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 7.56 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

**By concrete material group:** `C30` = 1.08 m³ net, `C40` = 0.48 m³ net, `C35` = 6 m³ net

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
