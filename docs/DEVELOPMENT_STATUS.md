# Development status

Updated: 27 September 2026.

## Delivered: release candidate 0.2, live ETABS 22.7 extraction

Quentra reads the model open in a running ETABS 22.7 (read-only), builds a schema 2 snapshot, runs a model health check using the existing validation, and takes it through calculation, override, review, acceptance and export, from the CLI or the local GUI. See [ETABS_INTEGRATION.md](ETABS_INTEGRATION.md).

The calculation engine is unchanged (`takeoff/0.5.0`): it gained two optional snapshot fields (`source`, `sourceWarnings`) and the `LongitudinalTorsion` component. Snapshots without them keep their hashes, and the committed reference runs (made on macOS arm64) replay byte-identically on Windows x64.

| Roadmap area | State |
|---|---|
| Phase 0: build selection and Windows API experiments | ETABS 22.7.0.4095 selected; API experiments done and recorded (ETABS_INTEGRATION.md, "Verified ETABS 22.7 behaviour") |
| Phase 1: SI types, schemas, frame calculations, replay, CI | Implemented; policy decisions unapproved |
| Phase 2: ETABS adapter, frame extraction | Implemented (standalone attach by process id; GUI in the browser, not WPF) |
| Phase 3: slab/wall geometry, openings, summaries | Implemented and verified against ETABS models |
| Phase 4: reinforcement integration | Beam/column longitudinal design demand and modeled column bars from ETABS; shear, wall and slab steel not extracted |
| Phase 5: overrides, review, reports | Implemented; review findings resolved (below) |
| Phase 6: qualification and packaging | Windows package script; qualification by a structural engineer pending |

## Evidence

Windows 11 x64, .NET SDK 10.0.401, ETABS 22.7.0.4095 (ETABS 23.1.1 also installed and COM-registered).

- **Build and tests:** Release build with zero warnings; 224 unit tests pass, with or without ETABS installed (a copy built with the ETABS API absent also passes and ships no ETABSv1.dll).
- **Controlled ETABS models:** 13 models built through the API, with hand-calculated expectations in `fixtures/etabs/v22.7/golden.json`. Their 18 raw captures match in `dotnet test`; the live integration suite (`tools/Quentra.EtabsFixtures verify`) passes 217/217: read-only behaviour for every model, golden values, unit invariance over kN-m, N-mm, kgf-m, kip-ft and lb-in (worst relative difference 3.7e-16), mesh invariance, design-evidence classification, refusal of an unsaved model, and ETABS ended mid-extraction.
- **Real projects:** two 25-story ETABS 22.7 projects (about 8,400 objects each: beams, slabs, walls, a 1 m raft), extracted read-only. Totals, and every story, match ETABS's own "Material List by Story" and "by Object Type" tables to 0.0005 m³ (the rounding of ETABS's reported weights): 6,267.137 m³ and 6,538.279 m³. Twelve elements of the first (five beam sections, a six-vertex slab, the raft, a stair, M30 and M40 walls) were recomputed independently from the raw coordinates and match to 1e-13 m³. Neither project has frame columns or opening objects, and neither has concrete design results, so columns, openings and ETABS steel are verified on the controlled models only.
- **Performance:** extraction of a 50,320-object tower takes under a minute; normalise, validate and calculate together about 5 s; export 11 s; 2.4 GB peak (README, Scale).
- **Windows:** paths with spaces, commas and Unicode, a 403-character path, a read-only destination (clear refusal), existing outputs (never overwritten), several ETABS 22.7 instances (asks for --pid), no ETABS running, an untested version, a model opened from a `.$et` text file.

## Review findings resolved

- **Implausible quantities:** until the run is accepted, a total that includes implausible values is labelled "including unreviewed implausible values in ..."; acceptance still needs each flagged element acknowledged.
- **Draft exports:** workbook named `report-DRAFT.xlsx` / `report-ACCEPTED.xlsx` / `report-ACCEPTED-PARTIAL.xlsx`, status banner at the top of the Contents and Summary sheets and the console, GUI zip named by status.
- **GUI persistence:** accepted runs are written to disk as soon as they are accepted and can be reopened from a Saved runs list.
- **Reviewer identity:** acceptance records the typed name, Windows account, computer, time and Quentra version; reports and the GUI say it is recorded, not verified. Older runs without these fields replay unchanged.
- **Stale results:** editing the snapshot blocks Override, Accept and Export in the GUI; `etabs check` / Check against ETABS compares the run with the live model element by element, and a changed model blocks Accept and Export (enforced by the server).

## Open decisions (engineer)

- Measurement policy approval, plausibility bands, and the required steel components per element type used by the ETABS adapter (beam: top, bottom, transverse; column: longitudinal, transverse; slab: four layers; wall: web, boundary).
- The default maximum design-station gap (1.0 m). ETABS's default column output has 1.5 m gaps, so column design demand is unknown unless a larger gap is approved.
- Whether weightless non-concrete materials may be treated as dummy members (current rule; acknowledged per run).
- Beam story convention and the 1 mm Base band.

## Next tasks

1. Engineer review of the ETABS rules, the controlled models and the measurement policy.
2. A real project with frame columns, openings and concrete design results, reconciled the same way.
3. Wall (pier/spandrel) and slab reinforcement extraction; shear steel conversion.
4. Test ETABS 23 and record it as tested or not.
5. Bulk (table) reads for per-object attributes on very large models, if extraction time matters.
