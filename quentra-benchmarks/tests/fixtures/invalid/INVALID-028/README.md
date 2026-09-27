# INVALID-028 — Slab mesh on irregular slab without region

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

A mesh over an L-shaped slab would be over-estimated if its bounding box were used. Quentra must refuse rather than guess.

**Expected Quentra behaviour:** error, steel unknown

## A. Structural truth

- S1 L-shaped, mesh T12@200 X, no region

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 3, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Slab `S1`

```text
Host polygon (6 vertices) area in its own plane = 44 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (S200)
Gross volume = 44 x 0.2 = 8.8 m3
Net volume = (44 - 0) x 0.2 = 8.8 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
Steel: component 0: slab is not an axis-aligned rectangle and no region is given -> steel unknown (UNSUPPORTED_REINFORCEMENT_LAYOUT)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.S1.steelKg` |  | None |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 8.8 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 8.8 m³ |
| Steel (known) | 0 kg |
| Concrete completeness | Complete |
| Steel completeness | Incomplete (unknown: S1) |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `UNSUPPORTED_REINFORCEMENT_LAYOUT` | Error | `S1` | yes | A mesh layout was given for a host that is not a rectangle and no explicit region was supplied. |

**Finalization:** BLOCKED by UNSUPPORTED_REINFORCEMENT_LAYOUT

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
