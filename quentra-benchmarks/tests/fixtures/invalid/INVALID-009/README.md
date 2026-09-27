# INVALID-009 — Duplicate element ID

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

A beam and a slab share ID 'B1' (conflicting content).

**Expected Quentra behaviour:** reject both, block finalization

## A. Structural truth

- Beam B1 and slab B1

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 3, materials: 2, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Status: Rejected - another element shares this ID with different content (DUPLICATE_ELEMENT_ID); the true object is unknown.
```

### Slab `B1` (copy 2)

```text
Status: Rejected - another element shares this ID with different content (DUPLICATE_ELEMENT_ID); the true object is unknown.
```

### Beam `B-OK`

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
| `elements.B-OK.netConcreteM3` | unaffected control beam | 1.08 |
| `elements.B1#0.status` |  | Rejected |
| `elements.B1#1.status` |  | Rejected |

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
