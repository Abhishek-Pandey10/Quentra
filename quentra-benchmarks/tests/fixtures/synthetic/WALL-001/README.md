# WALL-001 — Simple rectangular wall

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** wall · **Fixture set:** synthetic

## Purpose

Vertical rectangular wall.

**Expected Quentra behaviour:** include

## A. Structural truth

- W1: 5 m long × 3 m high, t = 0.25 m, in plane y = 0

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 15 m2 (shoelace on local u-v coordinates)
Thickness t = 0.25 m (W250)
Gross volume = 15 x 0.25 = 3.75 m3
Net volume = (15 - 0) x 0.25 = 3.75 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.W1.grossAreaM2` | 5 × 3 | 15 |
| `modelTotals.netConcreteM3` | 15 × 0.25 | 3.75 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3.75 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 3.75 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
