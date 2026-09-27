# DUP-009 — Wall-column overlap

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** double-counting · **Fixture set:** synthetic

## Purpose

A column modelled inside a wall. Both are counted in full; Quentra warns so the modeller can decide.

**Expected Quentra behaviour:** include both, warn

## A. Structural truth

- W1 6 × 3 × 0.2 in plane y = 0
- C1 0.4 × 0.4 at (3, 0) — its axis lies inside W1

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 1, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Column C1 axis mid-point lies inside wall W1 -> WALL_COLUMN_OVERLAP (no deduction).

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 18 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (W200)
Gross volume = 18 x 0.2 = 3.6 m3
Net volume = (18 - 0) x 0.2 = 3.6 m3
Status: Included
```

### Column `C1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3^2) = 3 m
Section C400: rectangle b x h = 0.4 m x 0.4 m, A = 0.16 m2
Gross volume = A x L = 0.16 x 3 = 0.48 m3 (no openings: net = gross)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | 3.6 + 0.48 (overlap 0.2 × 0.4 × 3 = 0.24 m³ not deducted) | 4.08 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 4.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 4.08 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `WALL_COLUMN_OVERLAP` | Warning | `C1+W1` | no | A column axis lies inside a wall. Both are counted in full (no deduction). |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
