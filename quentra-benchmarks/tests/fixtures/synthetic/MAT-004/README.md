# MAT-004 — Material aliases without mapping

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** material · **Fixture set:** synthetic

## Purpose

Four names that a human would read as 'C30'. With no explicit mapping Quentra must keep four separate groups — no silent normalisation.

**Expected Quentra behaviour:** four separate groups, no warning

## A. Structural truth

- B1: 0.3 × 0.6 × 6 in material 'C30'
- B2: 0.3 × 0.6 × 6 in material 'C30/37'
- B3: 0.3 × 0.6 × 6 in material 'CONC30'
- B4: 0.3 × 0.6 × 6 in material 'Concrete_30MPa'

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 4, materials: 4, sections: 1, design results: 0.
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

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

### Beam `B3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

### Beam `B4`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byMaterial.C30.netConcreteM3` | 0.3 × 0.6 × 6 | 1.08 |
| `byMaterial.CONC30.netConcreteM3` | 0.3 × 0.6 × 6 | 1.08 |
| `byMaterial.Concrete_30MPa.netConcreteM3` | 0.3 × 0.6 × 6 | 1.08 |
| `modelTotals.netConcreteM3` | 4 × 1.08 | 4.32 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 4.32 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 4.32 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

**By concrete material group:** `C30` = 1.08 m³ net, `C30/37` = 1.08 m³ net, `CONC30` = 1.08 m³ net, `Concrete_30MPa` = 1.08 m³ net

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
