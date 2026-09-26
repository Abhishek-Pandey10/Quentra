# Validation fixtures A–E

Schema 2 snapshots of the hand-calculated examples in [design document §33](../../quentradesigndoc.md#33-engineering-validation-before-coding). Values are illustrative inputs, not ETABS data. Automated checks are in [ValidationFixtureTests.cs](../../tests/Quentra.Tests/ValidationFixtureTests.cs).

| Fixture | §33 gross | §33 opening-adjusted | §33 steel | Current result |
|---|---|---|---|---|
| [A — beam](A-beam.json) | 1.080 m³ | 1.080 m³ | 117.75 kg | Matches |
| [B — column](B-column.json) | 0.576 m³ | 0.576 m³ | 71.02512671 kg | Matches |
| [C — slab](C-slab.json) | 6.000 m³ | 5.800 m³ | 455.30 kg | Matches |
| [D — wall](D-wall.json) | 2.400 m³ | 1.980 m³ | 155.43 kg | Matches |
| [E — bay](E-bay.json) | 15.192 m³ | 14.772 m³ | Not defined | Matches; steel unknown |

The tests also repeat every fixture with millimetre coordinates and inch dimensions. They split C and D into pieces across their openings and check that the totals don't change. Analysis-mesh invariance needs ETABS and is not covered here. E's net union total (12.972 m³) is V1 scope and is not calculated.

## Modelling choices for engineer review

§33 does not state these, so they were chosen to reproduce its arithmetic:

- **Frame axes:** Frames are modelled on their centroidal axes. In E, the beams lie at z = 2.7 (centre of 2.4–3.0) along the column centre lines, so beam lengths are 6 m and 5 m centre to centre, as in §33. Joint overlap is retained in gross volume.
- **Area planes:** Areas are modelled on their mid-planes. In E, the slab sits at z = 2.9 and the wall centre line at y = 2.5.
- **Required steel components match §33's stated exclusions.** A requires top and bottom longitudinal steel only (ties excluded), and B requires longitudinal steel only (ties and laps excluded). C requires four layers. D requires web steel only (no boundary, edge or lap steel). A real policy would require more, and so would report steel as partial.
- **A's demand is a constant area at three stations.** Its evidence is declared `VerifiedCurrent` only so the integration path can be exercised. This is not an ETABS design result.
- **B's bars are straight 8-D20 bars** (As = π·0.02²/4 m² each) over the 3.6 m column.
- **C and D steel** use a distributed intensity over the opening-adjusted area, with no trimmer bars.
- **E has no reinforcement inputs.** All 25 required components are missing until an engineer-reviewed component schedule exists.
- **Policy:** density 7850 kg/m³, linear tolerance 0.1 mm and planarity tolerance 1 mm. The policy is unapproved, so every run warns `POLICY_PENDING` and cannot be accepted.
