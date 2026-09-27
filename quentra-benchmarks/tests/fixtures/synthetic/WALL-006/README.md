# WALL-006 — Sloped/inclined wall

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** wall · **Fixture set:** synthetic

## Purpose

Inclined wall: area is measured in the wall's own plane (true area), not its vertical projection.

**Expected Quentra behaviour:** include

## A. Structural truth

- W1: base (0,0,0)–(4,0,0), top (0,1.2,3.5)–(4,1.2,3.5); t = 0.20 measured normal to the wall

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 14.8 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (W200)
Gross volume = 14.8 x 0.2 = 2.96 m3
Net volume = (14.8 - 0) x 0.2 = 2.96 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.W1.grossAreaM2` | 4 × √(1.2² + 3.5²) = 4 × 3.7 = 14.8 (vertical projection 14.0 would be wrong) | 14.8 |
| `modelTotals.netConcreteM3` | 14.8 × 0.2 = 2.96 | 2.96 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 2.96 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 2.96 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
