# RWALL-004 — Wall mesh with door opening

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** reinforcement · **Fixture set:** synthetic

## Purpose

Documents the opening convention for mesh steel: bars are counted over the full host rectangle (not cut at openings). Concrete IS deducted.

**Expected Quentra behaviour:** include; mesh not reduced by openings

## A. Structural truth

- W1: 5 × 3 m, t = 0.20, door 1.0 × 2.1 m
- Vertical and horizontal T12@200 both faces

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Wall `W1`

```text
Host polygon (4 vertices) area in its own plane = 15 m2 (shoelace on local u-v coordinates)
Opening D1: area 2.1 m2, fully inside the host
Sum of opening areas = 2.1 m2; area of (union of openings) inside host = 2.1 m2
Thickness t = 0.2 m (W)
Gross volume = 15 x 0.2 = 3 m3
Net volume = (15 - 2.1) x 0.2 = 2.58 m3
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] Mesh (bottom): bars along Vertical, d = 12 mm @ 0.2 m, host rectangle (wall-local): span along = 3 m, span across = 5 m, faces = 2
    bars N = floor((5 - 2 x 0.05) / 0.2) + 1 = floor(24.5) + 1 = 25; bar length = 3 - 2 x 0.025 = 2.95 m
    total length = 2 x 25 x 2.95 = 147.5 m; mass = 147.5 x 0.000113097 x 7850 = 130.952577 kg
[1] Mesh (bottom): bars along Horizontal, d = 12 mm @ 0.2 m, host rectangle (wall-local): span along = 5 m, span across = 3 m, faces = 2
    bars N = floor((3 - 2 x 0.05) / 0.2) + 1 = floor(14.5) + 1 = 15; bar length = 5 - 2 x 0.025 = 4.95 m
    total length = 2 x 15 x 4.95 = 148.5 m; mass = 148.5 x 0.000113097 x 7850 = 131.840391 kg
Element steel = 130.952577 + 131.840391 = 262.792969 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.steelKg` | same as an unpierced wall | 262.792969 |
| `modelTotals.netConcreteM3` | (15 − 2.1) × 0.2 = 2.58 | 2.58 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 3 m³ |
| Opening deduction | 0.42 m³ |
| Net concrete | 2.58 m³ |
| Steel (known) | 262.792969 kg |
| &nbsp;&nbsp;of which ProvidedBars | 262.792969 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

None. Quentra must emit **no** warnings for this case (an extra warning is a failure).

**Finalization:** allowed

## Notes and assumptions

- Trimming bars at openings is a legitimate alternative convention; if Quentra adopts it, this case must be versioned, not edited.

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
