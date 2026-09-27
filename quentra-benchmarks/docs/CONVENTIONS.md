# Quentra benchmark conventions

These rules define every expected value in `tests/fixtures`. If Quentra adopts a different convention, version the affected cases; never edit their expected values in place.

## 1. Units
- `model.units.length` ∈ {m, mm, cm, ft, in}. Every coordinate, section dimension, bar diameter, spacing, cover, offset and design station is in that unit. Required areas are in unit²; shear Av/s in unit²/unit.
- Conversions are exact: 1 ft = 0.3048 m, 1 in = 0.0254 m, 1 lb = 0.45359237 kg (1 lb/ft³ = 16.018463373960138 kg/m³).
- Values are never auto-rescaled. A suspect value (e.g. slab thickness 300 m) is computed as given and flagged `UNIT_PLAUSIBILITY`.

## 2. Provenance
- Every fixture here is `synthetic_engineering_benchmark`, `etabs_verified: false`. Design results carry `origin: "simulated"`.
- Captured ETABS data must use `captured_etabs_snapshot`, `etabs_verified: true` and a non-empty `etabs_version`. Mixing the two categories is fatal (`PROVENANCE_INCONSISTENT`).
- Every design result must also declare `origin`: `simulated` in synthetic snapshots, `captured` in captured snapshots. Missing or inconsistent result origins reject the snapshot, even if steel quantification is disabled.

## 3. Concrete
- Frames (Beam/Column): V = A × L, with L the true 3-D length between the end nodes. Rectangle A = b × h; Circle A = πD²/4. Centre-line to centre-line: **no deduction at beam–column joints**.
- Area objects (Slab/Wall): area measured in the object's own plane (true area for inclined walls). V = area × thickness.
- Openings: deducted area = area of (union of openings) ∩ host. Overlapping openings count once (`OPENING_OVERLAP`, Info). Openings partly outside are clipped (`OPENING_CLIPPED`); fully outside deduct 0 (`OPENING_OUTSIDE_HOST`). Circular openings are the polygons given, not πr².
- Walls: the first boundary edge defines the local u axis; v points upward.

## 4. Element handling
| Situation | Status | Counted in concrete totals |
|---|---|---|
| Normal concrete element | Included | yes |
| Analytical segment / mesh element with existing parent | Excluded (Info) | no |
| Identical duplicate (same ID, same content) | first Included, copies Excluded | once |
| Conflicting duplicate ID | all copies Rejected | no (unknown) |
| Invalid section / geometry / coordinates | Rejected | no (unknown) |
| Missing or unsupported section | Unquantified | no (unknown) |
| Unknown or non-Concrete/Steel material | Unclassified (volume reported separately) | no |
| Structural steel material | Skipped (Info) | no |
| Unsupported type (Tendon, Link, …) | Skipped (Warning) | no |
| Missing / unknown story | Included under `<unassigned>` + Error | yes |

Unknown is `null`, never 0. `completeness` lists every element whose concrete or steel is unknown.

## 5. Reinforcement (steel density 7850 kg/m³ unless the material says otherwise)
- Bar area = πd²/4. Mass = total bar length × bar area × density.
- Bar/stirrup counts along a length: N = floor((length − 2 × offset)/spacing + 1e-9) + 1. The 1e-9 guard keeps exact divisions exact (14.000 → 14, not 13).
- StraightBars: length given, else the member length (frames only).
- Hoops: centre-line width = b − 2·cover − d (cover to outside face of the hoop); piece = 2(w + h) + hookAllowance; circular piece = π(D − 2·cover − d) + hookAllowance. Explicit centre-line dimensions override. `setsPerLocation` multiplies.
- Crossties: explicit piece length × perLocation.
- Mesh (slabs X/Y, walls Horizontal/Vertical): bars run along one span and are distributed across the other; bar length = span − 2·endCover; count uses edgeOffset. Host must be a rectangle or an explicit region must be given. Mesh bars are **not** cut at openings.
- RequiredArea (simulated design results): envelope named per component — `MaxOfStations` (max × L), `TrapezoidalIntegration`, `SegmentStepMax` (max of segment ends × segment length). Shear steel volume = ∫Av/s dx × hoopLength / legs.
- RequiredArea must refer to exactly one result belonging to the same member. `NotDesigned`/unknown statuses, negative/nonfinite/missing station values, duplicate station positions and conversion overflow leave steel unknown and block finalization. Current and stale results require complete station coverage; stale results retain the benchmark's historical known-subtotal behavior but block finalization. This differs from production Quentra's unquantified stale-demand policy.
- Ratio: rate × net concrete volume. FixedMass: as entered.
- Source categories are never mixed: exact = ProvidedBars, ManualOverride; approximate = RequiredDesignArea, EstimatedRatio; Unavailable = unknown. Exactness is derived from the source, not from the input `exact` flag.

## 6. Plausibility (configurable warning bands, see `tests/config/plausibility-rules.json`)
Beam/column dimension > 5 m, slab > 2 m, wall > 3 m, frame length > 100 m → `UNREALISTIC_DIMENSION`. If value/1000 would be typical → also `UNIT_PLAUSIBILITY` (Error). Steel density outside 7000–9000 kg/m³ → Error. Steel rate > 600 kg/m³ → Warning; > steel density → impossible, steel discarded.

## 7. Warnings and finalization
Warnings are a set of (code, subject). Codes, severities and whether they block finalization are in `tests/config/warning-codes.json`. Fatal rejects the whole snapshot.

## 8. Tolerances
Volume 1e-6 m³, area 1e-6 m², length 1e-6 m, steel 1e-3 kg, plus relative 1e-9.

Generator checks allow these tolerances only in expected quantity fields. Labels, counts, booleans, nulls, warnings and tolerance definitions compare exactly. Inputs are never rescaled to make a comparison pass.
