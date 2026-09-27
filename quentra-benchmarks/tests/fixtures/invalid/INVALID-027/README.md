# INVALID-027 — Missing provenance

> **Provenance:** `None` · `etabs_verified: none` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** invalid · **Fixture set:** invalid

## Purpose

No provenance: the origin of the data is unknown. Reject.

**Expected Quentra behaviour:** reject snapshot

## A. Structural truth

- no provenance block

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 1, materials: 2, sections: 3, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Snapshot rejected by fatal checks - no quantities are produced.

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `snapshotStatus` | fatal | Rejected |
| `modelTotals` | no quantities at all | None |

## Expected totals

Snapshot status: **Rejected** — no quantities are produced (all totals `null`).
## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `PROVENANCE_MISSING` | Fatal | `None` | yes | No provenance block, or provenance.type is not a known category. |

**Finalization:** BLOCKED by PROVENANCE_MISSING

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
