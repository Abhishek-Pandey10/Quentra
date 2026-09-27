# RSRC-006 — Conflicting exactness flags

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** source · **Fixture set:** synthetic

## Purpose

Input claims contradict the source: an estimate marked exact, and drawn bars marked inexact. Quentra must derive exactness from the source category, not trust the flag.

**Expected Quentra behaviour:** derive exactness from source, warn

## A. Structural truth

- B1: EstimatedRatio with exact = true (wrong)
- B2: ProvidedBars with exact = false (wrong)

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 2, materials: 2, sections: 1, design results: 0.

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).
- Rebar material B500: density = 7850 kg/m3.

### Beam `B1`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel: input says exact=True but source EstimatedRatio is not exact -> reported exact=False (EXACTNESS_FLAG_CONFLICT)
Steel source: EstimatedRatio (exact = False); density rho = 7850 kg/m3
[0] Ratio: rate 120 kg/m3 x net concrete 1.08 m3 = 129.6 kg (ESTIMATE)
Element steel = 129.6 kg
Status: Included
```

### Beam `B2`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section B: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Steel: input says exact=False but source ProvidedBars is exact -> reported exact=True (EXACTNESS_FLAG_CONFLICT)
Steel source: ProvidedBars (exact = True); density rho = 7850 kg/m3
[0] StraightBars (main): 4 bars, d = 20 mm, A_bar = pi x 0.02^2 / 4 = 0.000314159 m2; L = 6 m (member length)
    V = 4 x 0.000314159 x 6 = 0.007539822 m3; mass = V x rho = 59.187606 kg
Element steel = 59.187606 kg
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `elements.B1.steelExact` | EstimatedRatio is never exact | False |
| `elements.B2.steelExact` | ProvidedBars is exact | True |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 2.16 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 2.16 m³ |
| Steel (known) | 188.787606 kg |
| &nbsp;&nbsp;of which ProvidedBars | 59.187606 kg |
| &nbsp;&nbsp;of which EstimatedRatio | 129.6 kg |
| Concrete completeness | Complete |
| Steel completeness | Complete |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `EXACTNESS_FLAG_CONFLICT` | Warning | `B1` | no | The input 'exact' flag contradicts the reinforcement source. The engine reports exactness derived from the source. |
| `ESTIMATED_REINFORCEMENT` | Warning | `B1` | no | Steel comes from an estimated ratio, not from bars or design output. |
| `EXACTNESS_FLAG_CONFLICT` | Warning | `B2` | no | The input 'exact' flag contradicts the reinforcement source. The engine reports exactness derived from the source. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
