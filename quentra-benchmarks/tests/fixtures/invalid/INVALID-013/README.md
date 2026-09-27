# INVALID-013 — Opening outside host (wall)

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

A wall opening that does not touch its wall.

**Expected Quentra behaviour:** warn, deduct nothing

## A. Structural truth

- W1 4 × 3 m; O1 at u 6–7

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 12 m2 (shoelace on local u-v coordinates)
Opening O1: area 1 m2, lies completely outside the host -> deducts 0 (OPENING_OUTSIDE_HOST)
Sum of opening areas = 1 m2; area of (union of openings) inside host = 0 m2
Thickness t = 0.2 m (W200)
Gross volume = 12 x 0.2 = 2.4 m3
Net volume = (12 - 0) x 0.2 = 2.4 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 12 × 0.2 | 2.4 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 2.4 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 2.4 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `OPENING_OUTSIDE_HOST` | Warning | `W1/O1` | no | An opening does not intersect its host. It deducts nothing. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
