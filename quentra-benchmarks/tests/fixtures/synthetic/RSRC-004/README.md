# RSRC-004 — Source Unavailable

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** source · **Fixture set:** synthetic

## Purpose

No reinforcement information. Steel is UNKNOWN: element steel is null, model steel completeness is Incomplete. It must not become 0 kg of steel.

**Expected Quentra behaviour:** warn, steel unknown, block finalization

## A. Structural truth

- B1 with source Unavailable

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel: no reinforcement data -> steel UNKNOWN (not zero) (MISSING_REINFORCEMENT)
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelKg` | null, not 0 | None |
| `completeness.steel` |  | Incomplete |
| `modelTotals.steelKg` | sum of KNOWN steel only | 0 |

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
| `MISSING_REINFORCEMENT` | Warning | `B1` | yes | Steel quantification was requested but the element has no reinforcement data. Steel is unknown, not zero. |

**Finalization:** BLOCKED by MISSING_REINFORCEMENT

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
