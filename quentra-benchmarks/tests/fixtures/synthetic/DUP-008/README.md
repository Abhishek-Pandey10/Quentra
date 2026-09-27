# DUP-008 — Beam-column intersection

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** double-counting · **Fixture set:** synthetic

## Purpose

Two beams frame into column C1. Per the centre-line convention nothing is deducted and nothing is warned.

**Expected Quentra behaviour:** include all, no warning

## A. Structural truth

- C1, C2 0.4 × 0.4 × 3
- B1 6 m, B2 5 m, both 0.3 × 0.6, meeting at C1's top

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 4, materials: 1, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3 = 0.48 m3 (no openings: net = gross)
Status: Included
```

### Column `C2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3 = 0.48 m3 (no openings: net = gross)
Status: Included
```

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 5^2 + 0^2) = 5 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 5 = 0.9 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 0.96 + 0.18 × 11 = 2.94 | 2.94 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 2.94 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 2.94 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

## Notes and assumptions

- Convention: centre-line quantities, no joint deduction (CONVENTIONS.md §3).

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
