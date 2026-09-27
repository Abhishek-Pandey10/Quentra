# INVALID-005 — Wall thickness = 250 m

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

Same unit mistake for a wall.

**Expected Quentra behaviour:** include as given, warn + unit error, block finalization

## A. Structural truth

- W1: 4 × 3 m, t = 250 m (sic)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 2, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 12 m2 (shoelace on local u-v coordinates)
Thickness t = 250 m (W250M)
Gross volume = 12 x 250 = 3000 m3
Net volume = (12 - 0) x 250 = 3000 m3
Plausibility: thickness 250 m > 3 m (WALL_THICKNESS_MAX) -> UNREALISTIC_DIMENSION
Plausibility: 250/1000 = 0.25 m would be a typical thickness -> probable unit error (UNIT_PLAUSIBILITY). Value is NOT rescaled.
Status: Included
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
| `elements.W1.netConcreteM3` | 12 × 250 — not rescaled | 3000 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3001.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 3001.08 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `UNREALISTIC_DIMENSION` | Warning | `W1` | no | A dimension exceeds a configurable plausibility band. Quantities are still computed as given. |
| `UNIT_PLAUSIBILITY` | Error | `W1` | yes | A dimension is implausibly large and would be typical if divided by 1000 - probably a unit error. The value is NOT auto-corrected. |

**Finalization:** BLOCKED by UNIT_PLAUSIBILITY

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
