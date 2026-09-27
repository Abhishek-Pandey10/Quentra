# WALL-007 — Wall split across two stories

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** wall · **Fixture set:** synthetic

## Purpose

One wall line modelled as two story objects.

**Expected Quentra behaviour:** include

## A. Structural truth

- W1@L1: 6 × 3.5 m
- W1@L2: 6 × 3.0 m
- t = 0.25

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 1, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Wall `W1@L1`

```text
Host polygon (4 vertices) area in its own plane = 21 m2 (shoelace on local u-v coordinates)
Thickness t = 0.25 m (W250)
Gross volume = 21 x 0.25 = 5.25 m3
Net volume = (21 - 0) x 0.25 = 5.25 m3
Status: Included
```

### Wall `W1@L2`

```text
Host polygon (4 vertices) area in its own plane = 18 m2 (shoelace on local u-v coordinates)
Thickness t = 0.25 m (W250)
Gross volume = 18 x 0.25 = 4.5 m3
Net volume = (18 - 0) x 0.25 = 4.5 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byStory.L1.netConcreteM3` | 6 × 3.5 × 0.25 = 5.25 | 5.25 |
| `byStory.L2.netConcreteM3` | 6 × 3 × 0.25 = 4.5 | 4.5 |
| `modelTotals.netConcreteM3` | 5.25 + 4.5 | 9.75 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 9.75 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 9.75 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| L1 | 5.25 | 5.25 | unknown (null) |
| L2 | 4.5 | 4.5 | unknown (null) |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
