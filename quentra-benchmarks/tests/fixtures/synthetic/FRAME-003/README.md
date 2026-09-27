# FRAME-003 — Single circular column

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** frame · **Fixture set:** synthetic

## Purpose

Circular section: A = πD²/4. Checks that Quentra does not use a square D × D.

**Expected Quentra behaviour:** include

## A. Structural truth

- Column C1: diameter 0.50 m, length 3.50 m

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 1, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section C500D: circle D = 0.5 m, A = pi x D^2 / 4 = 0.19635 m2
Gross volume = A x L = 0.19635 x 3.5 = 0.687223 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | π × 0.5² / 4 × 3.5 = 0.21875π = 0.687223 | 0.687223 |
| `elements.C1.netConcreteM3` | square would give 0.875 — must NOT | 0.687223 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 0.687223 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 0.687223 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
