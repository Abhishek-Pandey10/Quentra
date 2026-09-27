# ETABS integration

Quentra reads the model open in a running **ETABS 22.7** and turns it into a Quentra snapshot, which then goes through the same validation, calculation, review, acceptance and export as any other snapshot. ETABS is only read: Quentra never changes, saves, analyses or designs a model it attaches to, and does not change its units.

```
ETABS 22.7 (running, model open)
  -> quentra etabs extract      raw capture (every API value as returned) + snapshot + log
  -> model health check         existing snapshot validation and warnings
  -> quentra calculate          engine takeoff/0.5.0, unchanged
  -> override / accept          existing review workflow
  -> quentra export             CSV + workbook named by status + manifest
```

The browser GUI does the same: **From ETABS…** connects, extracts, shows the extraction summary and model health, and calculates.

## Supported ETABS versions

| Version | Status |
|---|---|
| ETABS 22.7 (tested build 22.7.0.4095, API ETABSv1 2.10) | **Tested and supported** |
| ETABS 23.x | Untested. Installed on the development machine, not tested. Quentra refuses it unless `--pid` and `--allow-untested-version` are both given, and then marks the snapshot, the run warnings and the report as untested. |
| ETABS 21.x and earlier | Untested |

Quentra finds ETABS installations under `Program Files\Computers and Structures`, lists running ETABS processes with their versions, and chooses the running ETABS 22.7 automatically. It never switches to another version by itself; with several 22.7 copies running it asks for `--pid`.

## Architecture

| Project | Role |
|---|---|
| `Quentra.Etabs` | Adapter contracts (`IEtabsModelReader` and the story, material, section, frame, area and design-result readers), the raw capture format, unit normalisation, `EtabsSnapshotBuilder` (raw capture -> snapshot), discovery, and `SnapshotFingerprint`. No CSI assembly: tested on any platform. |
| `Quentra.Etabs.Api` | The live readers over the CSI API. Compiled against ETABS 22.7's `ETABSv1.dll` when installed, and to a stub otherwise, so the solution builds anywhere. |
| `tools/Quentra.EtabsFixtures` | Builds the controlled ETABS models and runs the ETABS integration suite. |

The calculation engine does not reference ETABS. It gained two optional snapshot fields, `source` (where an extracted snapshot came from) and `sourceWarnings` (conditions the extractor found, reported with their element and acknowledged like any warning); snapshots without them keep their hashes, and the committed reference runs replay unchanged.

**Why a standalone application, not a plugin.** Attaching to a running ETABS by process id works from .NET 10 and allows fully automated testing (the integration suite starts its own ETABS). A plugin runs inside ETABS's process and runtime and cannot be tested unattended. A thin plugin that launches Quentra can be added later without changing the adapter.

### Loading the right API

ETABSv1.dll is never shipped with or copied beside Quentra. At run time it is loaded from the folder of the ETABS being attached to, so Quentra uses ETABS 22.7's own API, and the loaded path and version are recorded in every snapshot (`source.apiAssembly`).

Cross-process calls are marshalled by Windows using the ETABSv1 type library registered on the machine, which is the **last ETABS registered** (on the development machine, ETABS 23). This is only safe if the registered version lays out every interface Quentra calls identically, or only appends methods. That was verified for ETABS 23.1.1 against 22.7 (all 135 interfaces identical or append-only), and Quentra repeats the check for the interfaces it uses each time it connects, refusing to connect if the layout differs.

## What is extracted

Everything comes from ETABS **model objects** (`FrameObj`, `AreaObj`), never analysis elements, so meshing cannot duplicate quantities.

| Source | API | Use |
|---|---|---|
| Model | `GetVersion`, `GetModelFilename`, `GetModelIsLocked`, `GetPresentUnits_2`, `GetDatabaseUnits_2`, `Tower.GetNameList` | identity, units, lock state |
| Stories | `Story.GetStories_2` | story bands (base to top) |
| Materials | `PropMaterial.GetTypeOAPI`, `GetOConcrete_1`, `GetORebar_1`, `GetWeightAndMass` | concrete / non-concrete classification, grades |
| Frame sections | `PropFrame.GetTypeOAPI`, `GetRectangle`, `GetCircle`, `GetNonPrismatic`, `GetSectProps`, `GetTypeRebar`, `GetRebarColumn`, `PropRebar.GetRebarProps` | dimensions, section type, modeled column bars |
| Area properties | `PropArea.GetSlab`, `GetWall`, `GetDeck` | slab/wall type, shell type, thickness, material |
| Frames | `FrameObj.GetAllFrames` (coordinates and joint offsets), `GetLabelFromName`, `GetGUID`, `GetDesignOrientation`, `GetDesignProcedure`, `GetMaterialOverwrite`, `GetCurved_2` | geometry and classification |
| Areas | `AreaObj.GetAllAreas`, `GetLabelFromName`, `GetGUID`, `GetProperty`, `GetOpening`, `GetMaterialOverwrite`, `GetCurvedEdges` | polygons, openings, classification |
| Design | `DesignConcrete.GetResultsAvailable`, `GetCode`, `GetDesignSection`, `GetSummaryResultsBeam`, `GetSummaryResultsColumn` | reinforcement evidence |

Materials, sections and properties are read once each, not per element. Every call goes through one wrapper that records a non-zero return code with the operation, object, method and its consequence for quantities (shown as `ETABS_API_CALL_FAILED` on the element). If ETABS closes or stops responding, extraction stops and nothing is written.

### Classification (never by name)

- **Frames:** ETABS design orientation Column -> column, Beam -> beam, Brace/Null/Other -> listed but not quantified (`ETABS_FRAME_ROLE`).
- **Material:** ETABS material type Concrete -> concrete; Steel, Rebar, Tendon, ColdFormed, Aluminum, Masonry -> out of scope. A material of any other type (e.g. NoDesign) with **zero weight and zero mass** marks a dummy or null member (load-transfer or null beams): out of scope with `ETABS_WEIGHTLESS_MATERIAL`, which the reviewer acknowledges. Any other unclassified material leaves the element unknown.
- **Sections:** rectangular (beams and columns) and circular (columns) are measured. Non-prismatic (Variable), Section Designer, tee, box, general, auto-select and all other types are listed with `ETABS_SECTION_UNSUPPORTED` and not estimated.
- **Areas:** by property: slab property -> slab, wall property -> wall, deck or other -> unsupported. Slab types Slab, Drop, Mat and Footing and wall type Specified with shell type ShellThin, ShellThick or Membrane have a uniform physical thickness. Ribbed, waffle, layered, auto-select-list and deck properties are listed but not measured. A property of None is a null area (out of scope). Curved-edge areas are not measured.

### Geometry

- Frame volume = section area × 3D length between the (joint-offset) end points. Insertion-point joint offsets are applied and disclosed (`ETABS_JOINT_OFFSET`); rigid end offsets and cardinal points do not change volume and are not deducted.
- Slab and wall volume = true planar polygon area × uniform thickness, less the union of openings clipped to the host. Walls use their actual polygon, never plan length × story height.
- **Openings.** An ETABS opening is a separate area object that voids whatever it overlaps in its plane. Quentra deducts it from every slab or wall it overlaps in that plane (the engine clips it to each host and warns when it crosses an edge) and lists the assignment per host (`ETABS_OPENINGS_ASSIGNED`). An opening that overlaps nothing is reported (`ETABS_OPENING_NO_HOST`).
- **Stories.** Bands from the ETABS base elevation up. Objects ETABS places on the base level (e.g. a raft) are reported under the base story name using a 1 mm band just below the base elevation (`ETABS_BASE_STORY`). Each object's ETABS story is recorded as its assigned story.

## Units

ETABS returns every value in its present (display) units. Quentra reads the present units, converts every length, area and coordinate to metres once in the builder, and refuses the capture if the present units change during extraction. It never calls `SetPresentUnits`. The raw capture keeps the original values and units.

Verified: the same model extracted in kN-m, N-mm, kgf-m, kip-ft and lb-in gives the same quantity for every element to 3.7 × 10⁻¹⁶ relative (live and from captures).

## Reinforcement

ETABS does not hold detailed construction reinforcement. Quentra separates the evidence and never presents design reinforcement as installed steel.

| Element | Evidence Quentra uses | Label |
|---|---|---|
| Beams | Concrete frame design: top and bottom flexural area, and torsional longitudinal area, at the design stations, integrated along the member | `DesignDemandEquivalent`: ETABS design reinforcement equivalent |
| Columns, design mode | PMM required longitudinal area at the design stations | `DesignDemandEquivalent` |
| Columns, check mode | The section's modeled bars (reinforcement to be checked) as straight bars over the column length | `ModelAssignedEquivalent` |
| Shear / ties (all frames) | Not converted (area per length) | missing: steel unknown |
| Walls, slabs | Not extracted in this version | missing: steel unknown (`ETABS_AREA_STEEL_NOT_EXTRACTED`) |

Rules:
- Design evidence counts only when the model is **locked** (ETABS deletes design results when a model is unlocked and edited), the ETABS design section equals the analysis section, and ETABS reports no design errors; otherwise it is recorded as stale or failed and not counted (`ETABS_DESIGN_NOT_CURRENT`).
- No design results at all: `ETABS_DESIGN_UNAVAILABLE`; beam and column steel is unknown.
- ETABS evidence is counted only when a person is named as approving its use for the takeoff (`--steel-approved-by`, or the GUI field). Without that it is recorded but not counted (`ETABS_STEEL_UNAPPROVED`).
- ETABS designs beams between column faces, so the design stations do not reach the member ends (verified: 0.2 m to 5.8 m on a 6 m beam between 400 mm columns). The sampled length is counted and the component stays incomplete (`PARTIAL_DEMAND_DOMAIN`); nothing is extrapolated.
- Stations further apart than the allowed maximum (default 1.0 m, `--max-station-gap`) leave that component unknown with `ETABS_STATION_GAP`. ETABS's default column output has stations 1.5 m apart on a 3.5 m column.
- Repeated stations at one location (ETABS reports segment ends twice) keep the larger area.

So a beam or column never shows complete steel from ETABS alone: shear steel is always missing and end zones are unsampled. That is deliberate.

## Verified ETABS 22.7 behaviour

Each was established by a minimal experiment (`tools/Quentra.EtabsFixtures experiment-*`) before designing around it.

| Behaviour | Consequence in Quentra |
|---|---|
| `FrameObj.GetCurved_2` returns 1 for every straight frame, 0 with the curve type for a curved one | non-zero return means straight |
| `GetModelFilename` is `""` in a freshly started ETABS and `(Untitled)` for any unsaved model | extraction refuses both |
| `CreateAnalysisModel` permanently replaces a wall containing an opening with wall objects around the opening and deletes the opening object | such walls' gross equals opening-adjusted; disclosed as `ETABS_WALL_OPENING_GAPS` |
| Creating the analysis model adds line and area **elements**, not objects, for frames and slabs | object-level extraction is mesh-invariant |
| `File.Save(path)` discards analysis and design results even for the same path; `File.Save()` keeps them | fixture tool uses `Save()` after design |
| The blank-model template's default design code (Chinese 2010) produced no concrete design results | fixtures set ACI 318-19 |
| ETABS keeps default column rebar data on every concrete section | column bars read only when `GetTypeRebar` = column |
| `PropFrame.GetMaterial` fails for a non-prismatic section | material read from its start section |
| When ETABS ends during a call, its managed wrappers throw `NullReferenceException` | treated, like `COMException`, as ETABS being gone |
| ETABS gives the dummy `--` and null `NB` sections zero weight in its own material list | consistent with the weightless-material rule |

## Read-only guarantee

Quentra calls only `Get*` methods on an attached model. The integration suite checks, for every controlled model, that the model file hash, lock state and present units are identical before and after extraction. The tool that builds test models only ever changes models in an ETABS it started itself.

## Staleness

`quentra etabs check <run>` (GUI: **Check against ETABS**) re-reads the open model and compares it with the run's snapshot element by element: geometry, sections, materials, openings, stories, steel evidence and source warnings, to 10 significant digits, ignoring timestamps, approvals and ETABS display units. A changed model names the changed, added and removed elements. In the GUI a run whose model changed can no longer be accepted or exported.

## Commands

```
quentra etabs status  [--pid <id>]
quentra etabs extract <new-snapshot.json> [--pid <id>] [--steel-approved-by <name>] [--policy-approved-by <name>] [--max-station-gap <m>] [--allow-untested-version]
quentra etabs inspect <object> [--pid <id>] [--raw <capture.etabs-raw.json>]
quentra etabs build   <capture.etabs-raw.json> <new-snapshot.json>
quentra etabs check   <run.json | snapshot.json> [--pid <id>]
```

`extract` writes `<name>.etabs-raw.json` (raw capture, hashed into the snapshot) and `<name>.etabs-log.txt` (versions, model, timings, counts, failed calls, warnings) beside the snapshot. `inspect B42@L3` prints the raw API values, section, property and material for one object, its classification, its snapshot record and its calculated quantity. It accepts the Quentra id, the ETABS unique name, or `frame:`/`area:` plus the unique name.

## Testing

- **Unit and capture tests** (`dotnet test`, no ETABS needed): 18 raw captures of the controlled models, taken from ETABS 22.7, replayed through the builder and engine and compared with hand calculations in `fixtures/etabs/v22.7/golden.json`; unit invariance over five unit systems; mesh invariance; design-evidence classification; mapping rules; GUI ETABS workflow over captures.
- **ETABS integration suite** (needs ETABS 22.7): `dotnet run --project tools/Quentra.EtabsFixtures -c Release -- verify fixtures/etabs/v22.7` starts its own ETABS, reopens every controlled model, re-extracts it, and checks read-only behaviour, the golden values, live unit and mesh invariance, refusal of an unsaved model, and ETABS being ended mid-extraction. Results are written to `fixtures/etabs/v22.7/integration-results.md`.

See [fixtures/etabs/README.md](../fixtures/etabs/README.md) for the controlled models.

## Known limitations

- Only ETABS 22.7 is tested.
- Concrete: rectangular beams, rectangular and circular columns, uniform-thickness planar slabs and walls. Curved, non-prismatic, Section Designer and other section shapes, ribbed/waffle/deck/layered areas and curved-edge areas are listed but not quantified. Braces are not quantified.
- Volumes are modeled gross and opening-adjusted: member intersections (beam-slab, beam-column, wall-slab) are counted in each member. Net, BOQ and procurement quantities are not produced.
- Steel: beam and column longitudinal design demand equivalents and modeled column bars only. Shear/tie steel, wall (pier/spandrel) and slab reinforcement are not extracted; their steel is unknown.
- Multi-tower models: story bands come from the active tower.
- A snapshot reflects the model in ETABS memory, which may include unsaved changes; the recorded file hash is of the last saved file.
