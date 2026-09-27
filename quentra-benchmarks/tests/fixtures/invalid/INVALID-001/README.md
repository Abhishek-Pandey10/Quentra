# INVALID-001 — Beam width = 0

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

Zero section width. Rejected, never quantified as 0 m³.

**Expected Quentra behaviour:** reject element, block finalization

## A. Structural truth

- B1 section width 0

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 2, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Status: Rejected - section 'B0x600' has a zero/negative/missing dimension [0.0, 0.6].
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
| `elements.B1.netConcreteM3` | unknown, not 0 | None |
| `completeness.concrete` |  | Incomplete |

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
| `INVALID_SECTION` | Error | `B1` | yes | A section dimension (width, depth, diameter, thickness) is zero or negative. |

**Finalization:** BLOCKED by INVALID_SECTION

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
