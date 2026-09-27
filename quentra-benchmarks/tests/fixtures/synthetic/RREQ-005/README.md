# RREQ-005 — Required area — missing/incomplete design result

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** required · **Fixture set:** synthetic

## Purpose

B1 references a design result that does not exist; B2's stations stop at 4.5 m of a 6 m member. Neither may be extrapolated.

**Expected Quentra behaviour:** error, steel unknown

## A. Structural truth

- B1: no design result
- B2: stations 0–4.5 m only

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 2, sections: 1, design results: 1.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: RequiredDesignArea (exact = False); density rho = 7850 kg/m3
Steel: component 0: no design result for 'B1' -> steel unknown (DESIGN_RESULT_MISSING)
Status: Included
```

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel source: RequiredDesignArea (exact = False); density rho = 7850 kg/m3
Steel: component 0: design stations do not cover 0..L -> steel unknown (DESIGN_RESULT_MISSING)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelKg` |  | None |
| `elements.B2.steelKg` |  | None |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 2.16 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 2.16 m³ |
| Steel (known) | 0 kg |
| Concrete completeness | Complete |
| Steel completeness | Incomplete (unknown: B1, B2) |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `DESIGN_RESULT_MISSING` | Error | `B1` | yes | A RequiredArea component references a design result that is absent or does not cover the member. |
| `DESIGN_RESULT_MISSING` | Error | `B2` | yes | A RequiredArea component references a design result that is absent or does not cover the member. |

**Finalization:** BLOCKED by DESIGN_RESULT_MISSING

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
