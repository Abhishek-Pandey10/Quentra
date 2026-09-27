# Controlled ETABS models

Small ETABS 22.7 models with hand-calculated quantities, used to test ETABS extraction. They are built through the ETABS API by `tools/Quentra.EtabsFixtures` (`Models.cs` defines every dimension), in kN-m, in a fresh ETABS started by the tool.

`v22.7/` holds, for ETABS 22.7.0.4095:

- `models/*.edb`: the models (ETABS side files such as `.$et` are not needed).
- `*.etabs-raw.json`: raw captures, every API value exactly as ETABS returned it. The unit tests replay these without ETABS.
- `golden.json`: expected quantities, hand-calculated from the dimensions (not from Quentra output), with the working in each `hand` field.
- `integration-results.md`: the last run of the live integration suite.
- `mesh-counts.txt`: object and analysis-element counts before and after meshing model 10.

| Model | Content | Checks |
|---|---|---|
| 01-single-beam | Beam 300×600, 6.000 m | 1.080 m³, story |
| 02-single-column | Column 400×600, 3.5 m | 0.840 m³ |
| 03-circular-column | Column D500, 3.5 m | 0.687 m³, circular section mapping |
| 04-slab | Slab 6×8 m, 200 mm | 9.600 m³ |
| 05-slab-openings | Slab with two overlapping openings and one crossing the edge | 8.550 m³ opening-adjusted; union and clipping |
| 06-wall | Wall 5.0×3.5 m, 250 mm | 4.375 m³ |
| 07-wall-opening | Wall 6.0×3.5 m with a door | 5.250 gross, 4.725 opening-adjusted |
| 08-one-story-frame | Columns, beams, slab and wall in one bay | 17.240 m³ by category and story |
| 09-multi-story | Three stories of different heights | 39.056 m³ and per-story totals |
| 10-meshed | Slab with opening, wall with door, beam as two objects crossed by a third, slab as four panels | 25.125 m³ opening-adjusted before and after `CreateAnalysisModel` |
| 11-units | Model 08 extracted in kN-m, N-mm, kgf-m, kip-ft, lb-in | identical per element |
| 12-unsupported | Non-prismatic, tee, circular-as-beam and inclined frames, steel beam, ribbed slab, deck, warped slab, null area, opening over nothing | only the rectangular beam is measured; each case is listed with its reason |
| 13-designed-frame | Model 08 without the wall, analysed and designed (ACI 318-19); one column checks modeled bars | beam/column design demand integration, 69.061 kg modeled column bars, evidence labels |

## Regenerate and verify

Both need ETABS 22.7 installed. They start their own hidden ETABS and never touch a model you have open.

```
dotnet run --project tools/Quentra.EtabsFixtures -c Release -- generate fixtures/etabs/v22.7
dotnet run --project tools/Quentra.EtabsFixtures -c Release -- verify   fixtures/etabs/v22.7
```

`generate` rebuilds the models and captures. Regenerate only for an intentional change, then run `dotnet test`: every capture must still match `golden.json`. `verify` reopens each model in ETABS, re-extracts it, and checks read-only behaviour, the golden values, unit and mesh invariance, refusal of an unsaved model, and ETABS being ended during extraction.

The tool also has the minimal experiments behind the adapter's rules (`experiment-wall`, `experiment-design`, `experiment-blank`), a read-only table dump of a running ETABS (`tables <pid> [table keys]`), and a synthetic-tower timing run (`perf <stories> <bays> <folder>`).
