# INVALID-008 — Steel density = 15000 kg/m3

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

Implausible density (> 9000 kg/m³ is not steel). Error; steel unknown.

**Expected Quentra behaviour:** error, steel unknown, block finalization

## A. Structural truth

- Rebar density 15 000 kg/m³

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material HEAVY: density 15000 kg/m3 is outside the plausible band -> steel using it is unknown.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel: rebar material 'HEAVY' has an unusable density -> steel unknown
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelKg` |  | None |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Steel (known) | 0 kg |
| Concrete completeness | Complete |
| Steel completeness | Incomplete (unknown: B1) |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `UNREALISTIC_MATERIAL_DENSITY` | Error | `HEAVY` | yes | A rebar material density is outside the configured plausible band. Steel using it is unknown. |

**Finalization:** BLOCKED by UNREALISTIC_MATERIAL_DENSITY

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
