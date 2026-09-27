# SLAB-008 — Opening completely outside slab

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

Opening associated with a slab it does not touch (typical extractor association bug). Deduct nothing, warn.

**Expected Quentra behaviour:** include, deduct nothing, warn

## A. Structural truth

- S1: 6 × 5 m, t = 0.20
- O1: x 8–9, y 1–2 — entirely outside

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 30 m2 (shoelace on local u-v coordinates)
Opening O1: area 1 m2, lies completely outside the host -> deducts 0 (OPENING_OUTSIDE_HOST)
Sum of opening areas = 1 m2; area of (union of openings) inside host = 0 m2
Thickness t = 0.2 m (S200)
Gross volume = 30 x 0.2 = 6 m3
Net volume = (30 - 0) x 0.2 = 6 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.openingDeductedAreaM2` | no intersection | 0 |
| `modelTotals.netConcreteM3` | 30 × 0.2 | 6 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 6 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 6 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `OPENING_OUTSIDE_HOST` | Warning | `S1/O1` | no | An opening does not intersect its host. It deducts nothing. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
