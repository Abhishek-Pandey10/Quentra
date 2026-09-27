# FRAME-006 — Two-story column

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** frame · **Fixture set:** synthetic

## Purpose

A column stack split per story, as ETABS does (same label, different object per story). Verifies story aggregation and that the shared label 'C1' does not merge the objects.

**Expected Quentra behaviour:** include

## A. Structural truth

- C1 at L1: 0.5 × 0.5, height 3.5 m
- C1 at L2: 0.5 × 0.5, height 3.0 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Column `C1@L1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C500x500: rectangle b x h = 0.5 m x 0.5 m, A = 0.25 m2
Gross volume = A x L = 0.25 x 3.5 = 0.875 m3 (no openings: net = gross)
Status: Included
```

### Column `C1@L2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C500x500: rectangle b x h = 0.5 m x 0.5 m, A = 0.25 m2
Gross volume = A x L = 0.25 x 3 = 0.75 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byStory.L1.netConcreteM3` | 0.25 × 3.5 = 0.875 | 0.875 |
| `byStory.L2.netConcreteM3` | 0.25 × 3.0 = 0.75 | 0.75 |
| `modelTotals.netConcreteM3` | 0.875 + 0.75 | 1.625 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.625 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.625 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| L1 | 0.875 | 0.875 | unknown (null) |
| L2 | 0.75 | 0.75 | unknown (null) |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
