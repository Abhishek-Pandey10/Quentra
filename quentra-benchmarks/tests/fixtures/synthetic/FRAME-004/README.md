# FRAME-004 — Two connected beams

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** frame · **Fixture set:** synthetic

## Purpose

Two collinear beams sharing a node, different sections. Nothing is deducted at the shared node.

**Expected Quentra behaviour:** include

## A. Structural truth

- B1: 0.30 × 0.60, L = 6 m
- B2: 0.30 × 0.50, L = 4 m (shares node (6,0,3) with B1)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 1, sections: 2, design results: 0.
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
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(4^2 + 0^2 + 0^2) = 4 m
Section B300x500: rectangle b x h = 0.3 m x 0.5 m, A = 0.15 m2
Gross volume = A x L = 0.15 x 4 = 0.6 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.netConcreteM3` | 0.3 × 0.6 × 6 = 1.08 | 1.08 |
| `elements.B2.netConcreteM3` | 0.3 × 0.5 × 4 = 0.60 | 0.6 |
| `modelTotals.netConcreteM3` | 1.08 + 0.60 = 1.68 | 1.68 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.68 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.68 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
