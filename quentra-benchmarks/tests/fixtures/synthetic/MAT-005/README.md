# MAT-005 — Material aliases with explicit mapping

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** material · **Fixture set:** synthetic

## Purpose

Same snapshot as MAT-004, but the run supplies an explicit alias map. Now all four group under 'C30'.

**Expected Quentra behaviour:** one group via explicit mapping

## A. Structural truth

- Same as MAT-004
- Run option materialAliases = {'C30/37': 'C30', 'CONC30': 'C30', 'Concrete_30MPa': 'C30'}

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 4, materials: 4, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false,"materialAliases":{"C30/37":"C30","CONC30":"C30","Concrete_30MPa":"C30"}}`

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
Material 'C30/37' grouped as 'C30' by explicit alias mapping in the run options.
Status: Included
```

### Beam `B3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Material 'CONC30' grouped as 'C30' by explicit alias mapping in the run options.
Status: Included
```

### Beam `B4`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Material 'Concrete_30MPa' grouped as 'C30' by explicit alias mapping in the run options.
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byMaterial.C30.netConcreteM3` | 4 × 1.08 under one group | 4.32 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 4.32 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 4.32 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

## Notes and assumptions

- The mapping lives in the run options (a Quentra project setting), not in the snapshot, so the captured ETABS data is never rewritten.

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
