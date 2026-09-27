# SLAB-007 — Opening partly outside slab boundary

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** slab · **Fixture set:** synthetic

## Purpose

Opening straddles the slab edge. Only the part inside the slab may be deducted.

**Expected Quentra behaviour:** include, deduct clipped part, warn

## A. Structural truth

- S1: 6 × 5 m, t = 0.20
- O1: x 5–7, y 1–3 (4 m²); x 6–7 lies outside the slab

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Slab `S1`

```text
Host polygon (4 vertices) area in its own plane = 30 m2 (shoelace on local u-v coordinates)
Opening O1: area 4 m2, only 2 m2 inside the host -> clipped (OPENING_CLIPPED)
Sum of opening areas = 4 m2; area of (union of openings) inside host = 2 m2
Thickness t = 0.2 m (S200)
Gross volume = 30 x 0.2 = 6 m3
Net volume = (30 - 2) x 0.2 = 5.6 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.openingDeductedAreaM2` | clipped to x 5–6: 1 × 2 | 2 |
| `modelTotals.netConcreteM3` | (30 − 2) × 0.2 = 5.6 (unclipped would give 5.2) | 5.6 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 6 m³ |
| Opening deduction | 0.4 m³ |
| Net concrete | 5.6 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `OPENING_CLIPPED` | Warning | `S1/O1` | no | An opening lies partly outside its host. Only the part inside the host is deducted. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
