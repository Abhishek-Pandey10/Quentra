# MAT-006 — Material classification ambiguity

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** material · **Fixture set:** synthetic

## Purpose

B2's material type is 'Other' (not classifiable); B3 is structural steel. Only B1 is concrete. B2's volume is reported separately as unclassified, not guessed as concrete.

**Expected Quentra behaviour:** B2 unclassified + error; B3 skipped + info; block finalization

## A. Structural truth

- B1 C30 (Concrete)
- B2 MAT-X (type Other)
- B3 S355 (type Steel)
- all 0.3 × 0.6 × 6

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 3, materials: 3, sections: 1, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Unclassified - material 'MAT-X' has type 'Other', which is neither Concrete nor Steel.
```

### Beam `B3`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Skipped - material 'S355' is structural steel - not a concrete quantity.
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `modelTotals.netConcreteM3` | B1 only | 1.08 |
| `modelTotals.unclassifiedVolumeM3` | B2 volume, not concrete | 1.08 |
| `elements.B2.status` |  | Unclassified |
| `elements.B3.status` |  | Skipped |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 1.08 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 1.08 m³ |
| Unclassified volume (not concrete) | 1.08 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Incomplete (unknown: B2) |
| Steel completeness | NotEvaluated |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `MATERIAL_TYPE_AMBIGUOUS` | Error | `B2` | yes | The material exists but its type is not Concrete, Steel or Rebar, so the element cannot be classified. |
| `NON_CONCRETE_ELEMENT` | Info | `B3` | no | The element's material is structural steel; it is not part of concrete quantities. |

**Finalization:** BLOCKED by MATERIAL_TYPE_AMBIGUOUS

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
