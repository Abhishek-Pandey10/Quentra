# DUP-001 — Beam plus analytical subdivision

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** double-counting · **Fixture set:** synthetic

## Purpose

The extractor delivered the physical beam and its two analytical segments. Only the physical object may be counted.

**Expected Quentra behaviour:** include parent, exclude children (info)

## A. Structural truth

- B1 physical 0.3 × 0.6 × 6
- B1~A1, B1~A2: analytical halves (role AnalyticalSegment, parentId B1)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 3, materials: 1, sections: 4, design results: 0.
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

### Beam `B1~A1`

```text
Status: Excluded - AnalyticalSegment of physical element B1; the parent carries the quantity.
```

### Beam `B1~A2`

```text
Status: Excluded - AnalyticalSegment of physical element B1; the parent carries the quantity.
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | B1 only (2.16 would be double counting) | 1.08 |
| `elements.B1~A1.status` |  | Excluded |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `B1~A1` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |
| `ANALYTICAL_CHILD_EXCLUDED` | Info | `B1~A2` | no | An analytical segment or mesh element whose physical parent exists was excluded to avoid double counting. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
