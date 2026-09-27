<p align="center"><img src="assets/brand/quentra-logo-copper.png" alt="Quentra" width="480"></p>

# Quentra

Traceable concrete and reinforcing-steel quantity takeoff from ETABS. Development has started with a portable .NET 10 calculation core and a command-line snapshot workflow.

**Current milestone:** Offline takeoff foundation. Frames, slabs, walls, openings, story allocation, supplied reinforcement, overrides, review and export run from saved snapshots. This is a development tool, not a live ETABS integration or a production quantity report.

## Run the examples

Install the .NET 10 SDK, then from the repository root:

```sh
dotnet build Quentra.slnx --configuration Release
dotnet test Quentra.slnx --configuration Release --no-build
dotnet run --project src/Quentra.Cli --configuration Release --no-build -- calculate fixtures/synthetic/takeoff.json output/takeoff-run.json
dotnet run --project src/Quentra.Cli --configuration Release --no-build -- replay output/takeoff-run.json output/takeoff-replayed.json
dotnet run --project src/Quentra.Cli --configuration Release --no-build -- export output/takeoff-run.json output/takeoff-report
```

In this workspace a local SDK is installed at `.tools/dotnet/dotnet`. To use it without modifying your machine's configuration:

```sh
export DOTNET_CLI_HOME="$PWD/.tools/cli-home"
export NUGET_PACKAGES="$PWD/.tools/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
.tools/dotnet/dotnet build Quentra.slnx --configuration Release
.tools/dotnet/dotnet test Quentra.slnx --configuration Release --no-build
```

## Commands

```
quentra template  snapshot <new-snapshot.json>
quentra validate  <snapshot.json>
quentra calculate <snapshot.json> <new-run.json>
quentra replay    <saved-run.json> <new-run.json>
quentra override  <run.json> <overrides.json> <new-run.json>
quentra override  <run.json> <new-run.json> --reason <text> --author <name> [--set W1.thickness=300mm] [--exclude C2] [--include C2]
quentra accept    <run.json> <new-run.json> --reviewer <name> --note <text> --acknowledge <CODE,...> [--partial]
quentra export    <run.json> <new-directory>
quentra help      <command>
quentra help      format
quentra codes
quentra --version
```

- `template snapshot` writes a working example snapshot to start from. [docs/SNAPSHOT_FORMAT.md](docs/SNAPSHOT_FORMAT.md) describes every field; the tool prints the same reference with `quentra help format`.
- `validate` reads and calculates a snapshot without writing anything, and prints each problem with its element. Use it while editing a snapshot.
- `help <command>` prints that command's details, including the override file format. `codes` explains every warning code and the terms used in the output (known vs complete, gross vs opening-adjusted, `designEvidence`).
- A usage error prints only the relevant command's usage.

- `calculate` and `replay` detect the schema: **1** is the frame-concrete format ([frames.json](fixtures/synthetic/frames.json)); **2** is the takeoff format ([takeoff.json](fixtures/synthetic/takeoff.json)). `override`, `accept` and `export` require schema 2 runs.
- `replay` verifies the package hash, recomputes every quantity and checks that the stored results reproduce, and says so. If a run was altered, the error says which part changed: the snapshot, named elements' saved quantities, the override ledger, or the review history.
- `override` with `--set ID.width|depth|diameter|thickness=VALUE<unit>`, `--exclude ID` or `--include ID` records overrides directly; original values and the snapshot hash are read from the run. Options can be repeated. The unit (`mm`, `m`, `in` or `ft`) is required, because a bare `700` could mean millimetres or metres.
- Overrides that would change nothing (the same value within the linear tolerance, excluding an excluded element, including an included one) are rejected, as are dimensions more than ten times outside the plausibility review bands (sections 10 mm–30 m, thickness 5 mm–20 m). Changing a frame's section stops its `DemandEquivalent` steel being counted, since the demand belongs to the old section; the `STEEL_UNAVAILABLE` warning names the override that caused it.
- `override` with a file applies a JSON array of recorded changes (dimension or thickness replacement, or exclusion). Each override names its author, reason and date, the original value, and the run's snapshot SHA256, which every command prints. Source values are retained; overrides apply to the effective model only, and an accepted run returns to Draft. `field` is one of `FrameWidthM`, `FrameDepthM`, `FrameDiameterM`, `AreaThicknessM` (values in metres) or `Excluded` (0 = included, 1 = excluded):

  ```json
  [{ "id": "o1", "objectId": "W1", "field": "AreaThicknessM", "originalValue": 0.25, "replacementValue": 0.3,
     "reason": "Field-verified thickness", "author": "A. Reviewer", "recordedAt": "2026-09-26T12:00:00Z",
     "snapshotSha256": "<printed by calculate>" }]
  ```
- `accept` requires a named reviewer, a note, and every current warning code acknowledged (in any letter case), and no code the run does not raise. A partial run can only be accepted with `--partial` and is labelled `AcceptedPartial`. Acceptance is blocked until the measurement policy is approved. Every problem with an acceptance is reported at once.
- `--exclude` reduces the scope. Totals on such a run are labelled "complete in scope reduced by override (excludes W1 (o1))" in the console, the GUI and the report, and `Summary.csv` lists the exclusions, so a reduced total is never shown as simply "complete".
- `export` verifies the run, then writes a new directory containing `run.json`, one CSV per report table, `report.xlsx`, and `manifest.json` with SHA-256 hashes of every file. Report quantities are rounded (m³, m² and m to 0.001, kg to 0.1, override dimensions to 0.1 mm), and the console uses the same rounding; `run.json` keeps full precision. `Elements.csv` names the overrides that changed each element. Steel is also totalled by category, material, story and evidence type; required components with no reinforcement entry appear as a "Not supplied" evidence row. `report.xlsx` opens on a Contents sheet. Exporting the same run twice produces identical files. The "package seal" is a hash of the run's canonical content, not of the `run.json` file bytes.

The synthetic takeoff fixture produces **18.408 m³** gross and **17.808 m³** opening-adjusted modeled concrete, and **1,297.505 kg** of known steel. Steel is partial because beam B1 declares a required transverse component with no source. The frame fixture produces **1.656 m³**. The design document's §33 validation fixtures are in [fixtures/validation](fixtures/validation/README.md) and all reproduce their hand-calculated values.

Snapshots and override files are limited to 192 MiB and run files to 1000 MiB, enough for a 70-storey model of about 84,000 elements (see Scale below). Outputs must be new paths; existing runs and reports are never overwritten. Exit code 0 means the operation succeeded, including a valid draft with partial quantities. Inspect coverage and warnings; success does not mean engineering acceptance. Codes 1, 2 and 130 indicate input/IO/validation failure, usage error and cancellation respectively.

## Implemented

- Rectangular straight prismatic beams and rectangular/circular columns, using true 3D axis length.
- Planar slabs and walls in their own local plane, with planarity and self-intersection checks. Openings are unioned and clipped to the host before deduction (1 µm clipping precision).
- Gross and opening-adjusted modeled volumes, kept separate. Member intersections remain included; net, BOQ and procurement quantities are unavailable.
- Story allocation: sloped and vertical frames and walls are split by elevation band; a level beam goes to the story whose top it sits on (lower < z ≤ upper) unless metadata assigns a story; slabs use their assigned story; anything outside declared bands is reported as `Unallocated`, never dropped.
- Reinforcement only from supplied, approved inputs: verified longitudinal design demand (station integration over a declared domain), assigned straight bars, distributed area intensity, volume fraction, and kg/m³ estimates. Evidence types stay separate. No default ratios, laps, anchorage or waste are inferred.
- Required steel components declared per element; complete steel is null while any component is missing, and `PARTIAL_STEEL` names each element and its missing components. An element declared with `requiredSteelComponents: []` needs no steel (complete at 0 kg) and raises `NO_STEEL_REQUIRED` for the reviewer to acknowledge. Demand requires verified current design evidence and is marked stale by dimension overrides.
- Warnings for unverified areas, invalid geometry, coincident frames, overlapping coplanar areas, openings outside or crossing their host, unallocated volume and unapproved policy.
- Plausibility warnings (`IMPLAUSIBLE_*`) for section sizes, thicknesses, lengths, steel intensity and steel density outside provisional review bands, to catch unit slips. Values are still quantified, and console totals name the flagged elements; the reviewer must acknowledge each flagged element (`IMPLAUSIBLE_DIMENSION:B1`), not just the code. A kg/m³ rate above the steel density is rejected as impossible.
- Metre, millimetre, foot and inch inputs; SI calculations.
- Strict versioned JSON, unique identities, canonical ordering, SHA-256 integrity checks, replay, atomic writes, and formula-safe CSV/Excel text.

Hashes detect changes relative to stored contents; they are not digital signatures or engineer approval. The source snapshot hash includes its capture metadata. Reordering inventory does not change the hash.

## Local GUI

A simple browser-based GUI covers the same workflow: open or edit a snapshot, calculate, override, accept, download the run and export the report.

```sh
dotnet run --project src/Quentra.Gui --configuration Release   # opens http://127.0.0.1:5178/
dotnet run --project src/Quentra.Gui --configuration Release -- --port 5200 --no-browser
```

It listens on this computer only and uses the same engine, replay checks and export as the CLI. The GUI keeps the 4 most recent runs in memory, and accepted runs until they are downloaded; a "Runs in this session" list shows what is held, and the page says when a run that was never downloaded is released. Download `run.json` to keep a run. Editing the snapshot after calculating marks the results out of date and blocks Override and Accept until you calculate again. Warnings are acknowledged one by one; there is no "tick all". The page links to the snapshot field reference. Snapshots over 2 MB are sent straight from the file rather than shown in the editor, and tables show 500 rows at a time with a filter. It works with schema 2 snapshots; schema 1 stays CLI-only. It is a stopgap for use on any platform, not the planned WPF review application.

## Scale

Measured on synthetic towers (columns, two-way beams, bay slabs with openings, a walled core with doors, and steel for every element), pretty-printed as an exporter would write them, through the CLI on an Apple-silicon Mac:

| Model | Elements | Snapshot | Calculate | Accept | Export | Peak memory | Run file |
|---|---|---|---|---|---|---|---|
| 60 storeys, 10×8 bays | 21,900 | 37 MB | 3 s | 4 s | 9 s | 1.9 GB | 89 MB |
| 70 storeys, 14×10 bays | 43,190 | 74 MB | 5 s | 7 s | 15 s | 3.4 GB | 177 MB |
| 70 storeys, 20×14 bays | 83,790 | 144 MB | 11 s | 15 s | 32 s | 4.8 GB | 345 MB |

Each command replays the run it reads, so accept and export include a full recalculation. The 60-storey totals were checked by hand. Plan on 8 GB of memory for models of about 40,000 elements and 16 GB above that. Through the GUI, the 70-storey, 43,190-element model took 3–5 s to open and calculate, about 2 s per override and under 1 s to accept, with the GUI process using about 4 GB. No real ETABS model of this size has been run yet.

## Not implemented yet

Live ETABS extraction, WPF desktop UI, net concrete, curved or non-prismatic frames, shear/transverse demand conversion, reinforcement extraction from ETABS, and Windows packaging. An `Etabs` source label in an imported snapshot is a source declaration, not compatibility certification. All current fixtures are synthetic; no engineering policy or quantity has been accepted by a structural engineer.

The core and CLI run independently of ETABS on platforms supported by .NET 10. Live CSI integration and WPF development require a licensed Windows ETABS test environment and verification of the exact build/runtime/code combination.

## Structure

| Project | Responsibility |
|---|---|
| `Quentra.Core` | Source records, SI quantity types, frame and planar geometry, steel calculation |
| `Quentra.Application` | Snapshot validation, takeoff engine, story allocation, coverage, overrides |
| `Quentra.Infrastructure` | JSON persistence, canonical ordering, hashes, replay, review, CSV/Excel export |
| `Quentra.Cli` | Offline calculate/replay/override/accept/export workflow |
| `Quentra.Gui` | Local browser GUI over the same workflow (ASP.NET Core, loopback only) |
| `Quentra.Tests` | Calculation, validation, persistence, review and export tests |

Runtime packages: [Clipper2](https://github.com/AngusJohnson/Clipper2) (polygon clipping, in Core) and [ClosedXML](https://github.com/ClosedXML/ClosedXML) (Excel, in Infrastructure). No CSI assemblies are bundled. The ETABS adapter and desktop projects will be added when their implementation starts.

Schema 2 snapshots contain the frame model plus areas, story bands, per-element metadata (material and section names, optional story, required steel components), reinforcement inputs and a measurement policy. See [takeoff.json](fixtures/synthetic/takeoff.json). Malformed JSON, unknown enum values, duplicate IDs, broken references, overlapping stories and inconsistent steel scopes reject the snapshot. Missing or unverifiable geometry produces unknown quantities instead. Inputs are limited to 192 MiB.

## Project documents

- [Design specification](quentradesigndoc.md)
- [Roadmap and acceptance gates](QUENTRA_ROADMAP.md)
- [Development status and next tasks](docs/DEVELOPMENT_STATUS.md)
- [Structural engineer meeting brief](docs/ETABS_Structural_Engineer_Meeting_Brief.md)

The next step is Windows API exploration alongside further offline development. Synthetic test success does not complete the live ETABS compatibility gate.
