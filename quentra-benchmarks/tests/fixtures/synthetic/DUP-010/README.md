# DUP-010 — Orphan analytical element

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** double-counting · **Fixture set:** synthetic

## Purpose

An analytical segment whose parent 'B9' is missing from the snapshot. It must not be silently dropped (quantity loss) nor silently counted.

**Expected Quentra behaviour:** reject, block

## A. Structural truth

- B9~A1: analytical, parent B9 absent
- B2: physical

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 1, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Beam `B9~A1`

```text
Status: Rejected - AnalyticalSegment whose parent 'B9' does not exist.
```

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | B2 only | 1.08 |
| `elements.B9~A1.status` |  | Rejected |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Incomplete (unknown: B9~A1) |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `ORPHAN_ANALYTICAL_ELEMENT` | Error | `B9~A1` | yes | An analytical/mesh child references a parent that does not exist. |

**Finalization:** BLOCKED by ORPHAN_ANALYTICAL_ELEMENT

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
