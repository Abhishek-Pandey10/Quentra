# DUP-005 — Duplicate object ID with conflicting content

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** double-counting · **Fixture set:** synthetic

## Purpose

Two different beams claim ID 'B1'. Quentra cannot know which is real: reject both, keep B2, block finalization.

**Expected Quentra behaviour:** reject both copies, block finalization

## A. Structural truth

- B1 (6 m) and B1 (8 m) — same ID, different geometry
- B2 6 m, unaffected

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 3, materials: 1, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Beam `B1`

```text
Status: Rejected - another element shares this ID with different content (DUPLICATE_ELEMENT_ID); the true object is unknown.
```

### Beam `B1` (copy 2)

```text
Status: Rejected - another element shares this ID with different content (DUPLICATE_ELEMENT_ID); the true object is unknown.
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
| `completeness.concrete` | B1 unknown | Incomplete |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Incomplete (unknown: B1) |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `DUPLICATE_ELEMENT_ID` | Error | `B1` | yes | Two or more elements share an ID but differ in content. All copies are rejected because the true object cannot be determined. |

**Finalization:** BLOCKED by DUPLICATE_ELEMENT_ID

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
