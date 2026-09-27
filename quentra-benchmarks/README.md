# Quentra Golden Benchmark Data

132 synthetic benchmark cases (suite 1.1.0) for testing the Quentra quantity engine without ETABS.

**None of this is ETABS data.** Every snapshot is labelled `synthetic_engineering_benchmark`, `etabs_verified: false`.

## Contents

```
docs/CONVENTIONS.md              every rule used to compute expected values
tests/
  fixtures/
    manifest.json                index of all cases (id, category, path, run options, equivalence group)
    synthetic/<CASE-ID>/         91 cases
    invalid/<CASE-ID>/           36 bad/dangerous inputs
    buildings/BLDG-A..E/         5 larger buildings (up to 710 elements)
    captured-etabs/              empty — place real ETABS captures here later, never over the synthetic ones
      each case folder:  snapshot.json  (ETABS-like input)
                         expected.json  (expected Quentra output)
                         README.md      (hand calculation, step by step)
  schemas/                       snapshot, expected, warnings, manifest (JSON Schema 2020-12)
  config/                        warning-codes.json, plausibility-rules.json
  generators/                    Python that produced the data (optional — regenerate / audit)
```

## Case groups

| Group | IDs |
|---|---|
| Frames | FRAME-001…008 |
| Slabs (openings, overlap, clipping) | SLAB-001…010 |
| Walls | WALL-001…008 |
| Materials (grades, aliases, ambiguity) | MAT-001…007 |
| Units (same model in m, mm, cm, ft, in) | UNITS-M/MM/CM/FT/IN |
| Beam bars / stirrups | RBEAM-001…006, RSTIR-001…004 |
| Column / slab / wall reinforcement | RCOL-001…005, RSLAB-001…005, RWALL-001…004 |
| Reinforcement source classification | RSRC-001…007 |
| Required-area envelopes (simulated) | RREQ-001…006 |
| Plausibility | PLAUS-001…004 |
| Story aggregation | AGG-001 |
| Double counting | DUP-001…010 |
| Invalid inputs | INVALID-001…028 |
| Design-evidence regression guards | DGUARD-001…008 |
| Messy naming | NAME-001 |
| Buildings | BLDG-A (1-story), B (5-story office), C (10-story wall-frame), D (irregular), E (intentional defects) |

## How the expected values were made

Each case carries hand-worked assertions (361 in total) and each building a closed-form tally. The generator refuses to write a fixture unless an independent calculator reproduces all of them and the output invariants pass; the five UNITS cases must match each other. Expected values never come from Quentra.

Regenerate or check (Python 3.11+; tested locally on 3.14, CI configured for 3.11):

```bash
python3 -m pip install -r requirements.txt
python3 -m unittest discover -s tests/generators -p 'test_*.py'
python3 tests/generators/generate.py --check   # data on disk is up to date
python3 tests/generators/validate.py           # schemas + invariants
```

## Using it from Quentra

This corpus uses a different schema and some different policies from the production application. The geometry-only bridge is `../scripts/audit_benchmark_geometry.py`; it checks compatible concrete quantities against the real CLI and reports exclusions explicitly. It does not assert steel, warning, story-policy or finalization parity. See `../docs/BENCHMARK_REVIEW.md` for those mappings and remaining differences. There is no full-contract C# harness yet.

For a future full-contract harness, load each manifest entry's snapshot and options, apply an explicitly reviewed adapter, then compare to `expected.json` using its tolerance block. `null` means unknown, never zero. Benchmark-specific warning comparisons use a set of (code, subject), after deliberate mapping to production diagnostics.

## Changes in 1.1.0

- Reject undesigned demand, negative/nonfinite demand values, duplicate stations and ambiguous result records.
- Verify result ownership and result-level provenance against snapshot provenance.
- Add eight DGUARD fixtures and adversarial Python regression tests; reject invalid generated outputs before writing files.
- Regeneration compares expected quantities within declared dimensional tolerances. Counts, provenance, warnings, structure and tolerance settings remain exact; snapshot/config/README files still use exact text comparisons.
- Retain the benchmark's documented stale-result policy for compatibility: stale demand remains in its known total but blocks finalization. Production Quentra instead leaves stale demand unknown; this remains an explicit policy difference.
