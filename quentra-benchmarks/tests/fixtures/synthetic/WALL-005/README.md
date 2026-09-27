# WALL-005 — Wall with opening partly outside boundary

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** wall · **Fixture set:** synthetic

## Purpose

Opening extends past the wall end.

**Expected Quentra behaviour:** include, deduct clipped part, warn

## A. Structural truth

- W1: 5 × 3 m, t = 0.25
- O1: u 4–6, v 1–2 (2 m²), half outside

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 15 m2 (shoelace on local u-v coordinates)
Opening O1: area 2 m2, only 1 m2 inside the host -> clipped (OPENING_CLIPPED)
Sum of opening areas = 2 m2; area of (union of openings) inside host = 1 m2
Thickness t = 0.25 m (W250)
Gross volume = 15 x 0.25 = 3.75 m3
Net volume = (15 - 1) x 0.25 = 3.5 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.W1.openingDeductedAreaM2` | clipped to u 4–5 | 1 |
| `modelTotals.netConcreteM3` | (15 − 1) × 0.25 = 3.5 | 3.5 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3.75 m³ |
| Opening deduction | 0.25 m³ |
| Net concrete | 3.5 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `OPENING_CLIPPED` | Warning | `W1/O1` | no | An opening lies partly outside its host. Only the part inside the host is deducted. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
