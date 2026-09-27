# INVALID-011 — Missing story

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

Element has no story. It is still counted (no quantity loss) but under '<unassigned>', and finalization is blocked.

**Expected Quentra behaviour:** include under <unassigned>, error, block finalization

## A. Structural truth

- B1 story = null

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 2, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Story: missing -> reported under '<unassigned>' (STORY_REFERENCE_MISSING).
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B300x600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
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
| `byStory.<unassigned>.netConcreteM3` |  | 1.08 |
| `modelTotals.netConcreteM3` | Σ stories incl. <unassigned> | 2.16 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 2.16 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 2.16 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| L1 | 1.08 | 1.08 | unknown (null) |
| <unassigned> | 1.08 | 1.08 | unknown (null) |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `STORY_REFERENCE_MISSING` | Error | `B1` | yes | The element has no story. It is reported under '<unassigned>'. |

**Finalization:** BLOCKED by STORY_REFERENCE_MISSING

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
