# Snapshot format (schema 2)

A snapshot is the JSON input to `quentra calculate`. It describes the model: frames, slabs and walls, stories, reinforcement sources, and the measurement policy. `quentra template snapshot <file>` writes a working example ([fixtures/synthetic/takeoff.json](../fixtures/synthetic/takeoff.json)) to start from. This reference is built into the tool: `quentra help format` prints it.

Schema 2 is written by `quentra etabs extract` from a running ETABS 22.7 (see [ETABS_INTEGRATION.md](ETABS_INTEGRATION.md)), or by hand or script.

## Rules for every field

- Property names are camelCase. An unknown or misspelled property is an error, and so is a property given twice.
- Enum values are strings and case-sensitive, e.g. `"Beam"`, not `"beam"` or `0`.
- Properties marked **required** must be present. `null` is allowed only where the tables say so.
- Dates are ISO 8601 with an offset, e.g. `"2026-09-26T00:00:00+00:00"` or `"2026-09-26T05:30:00+05:30"`. A date without an offset is rejected, because it would be read in the local time zone of whichever machine runs Quentra. Dates are stored in UTC, so the same instant gives the same snapshot hash whichever offset it was written with.
- Values ending in `M`, `M2`, `M3` or `KgM3` are metres, m², m³ or kg/m³. Coordinates use `model.coordinateUnit`. Every section dimension and thickness carries its own unit.

A **length** is `{ "value": 300, "unit": "Millimetre" }`, with unit `Metre`, `Millimetre`, `Foot` or `Inch`. A **point** is `{ "x": 0, "y": 0, "z": 3.6 }` in the coordinate unit.

## Top level

| Field | | Meaning |
|---|---|---|
| `schemaVersion` | required | `2` |
| `model` | required | Model identity and frames (below) |
| `areas` | required | Slabs and walls; may be `[]` |
| `stories` | required | Story elevation bands; may be `[]` |
| `metadata` | required | One entry per frame and area |
| `reinforcement` | required | Steel sources; may be `[]` |
| `policy` | required | Measurement policy |
| `source` | optional | Where an extracted snapshot came from (below). Written by the ETABS adapter; not used in calculation |
| `sourceWarnings` | optional | Conditions the extractor found (below). Reported with their element, or with the run, and acknowledged at acceptance |

A snapshot without `source` and `sourceWarnings` has the same hash as before these fields existed.

## `source`

`program`, `programVersion`, `programBuild` (e.g. ETABS, 22.7.0, 22.7.0.4095), `apiAssembly` and `apiAssemblyVersion` (the ETABSv1.dll actually loaded), `modelPath`, `modelFileSha256` and `modelFileModifiedAt` (the last saved file; nullable), `modelLocked`, `presentUnits` (the ETABS units values were converted from), `extractor`, `extractorVersion`, `extractedAt`, and `rawCaptureSha256` (the SHA-256 of the raw capture file written beside the snapshot).

## `sourceWarnings[]`

`{ "objectId": "B12@L3", "code": "ETABS_SECTION_UNSUPPORTED", "message": "..." }`. `objectId` is a frame or area, or `null` for the whole run. `code` is upper case with underscores. `quentra codes` explains every ETABS code.

## `model`

| Field | | Meaning |
|---|---|---|
| `schemaVersion` | required | `1` |
| `modelId` | required | Model name |
| `origin` | required | `Synthetic` or `Etabs` (a declaration, not a compatibility claim) |
| `sourceDescription` | required | Where the data came from |
| `capturedAt` | required | When it was captured |
| `coordinateUnit` | required | Unit of all point coordinates |
| `frames` | required | Beams and columns; may be `[]` |

### `model.frames[]`

| Field | | Meaning |
|---|---|---|
| `objectId` | required | Unique across frames and areas |
| `sourceReference` | required | Where to find the object in the source, e.g. the ETABS label |
| `kind` | required | `Beam`, `Column` or `Other` (unsupported) |
| `material` | required | `Concrete`, `NonConcrete` (out of scope) or `Unknown` (unsupported) |
| `isStraight`, `isPrismatic` | required | Both must be `true` to be quantified |
| `start`, `end` | required | Axis end points |
| `section` | optional | `{ "shape": "Rectangle", "width": length, "depth": length }` or `{ "shape": "Circle", "diameter": length }`. Circles are columns only. Missing section: the frame is `Invalid`. |

Volume is the section area × 3D axis length.

## `areas[]`

| Field | | Meaning |
|---|---|---|
| `objectId`, `sourceReference` | required | As for frames |
| `kind` | required | `Slab`, `Wall` or `Unsupported` |
| `material` | required | `Concrete`, `NonConcrete` or `Unknown` |
| `boundary` | required | Planar polygon, 3–1000 points, not self-intersecting |
| `openings` | required | Array of polygons in the same plane; may be `[]`. Overlapping openings are unioned. An opening outside the boundary deducts nothing, and one that crosses it deducts only the part inside; both raise a warning. |
| `openingsVerified` | required | `true` once the openings' host assignment is checked. `false` makes the area unsupported. |
| `physicalThicknessVerified` | required | `true` once the thickness is checked as the physical thickness. `false` makes the area unsupported. |
| `thickness` | required | Length, measured normal to the plane |

## `stories[]`

`{ "id": "L1", "lowerElevationM": 0, "upperElevationM": 3.6 }`. Names must be unique, bands must not overlap, and `Unallocated` is reserved. Elevations are always metres. Columns, sloped beams and walls are split between stories in proportion to how much of their length (or area) lies within each band; a sloped beam that lies wholly within one band goes entirely to that story. A level beam (both ends within `linearToleranceM` of the same elevation) belongs to the story whose top it sits on (lower < z ≤ upper). Slabs use `metadata.assignedStoryId`. Anything unassigned is reported as `Unallocated`.

## `metadata[]`

Exactly one entry for every frame and area.

| Field | | Meaning |
|---|---|---|
| `objectId` | required | The frame or area |
| `materialName`, `sectionName` | required | Names used for grouping in reports |
| `assignedStoryId` | nullable | Story for slabs and horizontal members; overrides elevation-based allocation |
| `requiredSteelComponents` | required | The steel components that must be supplied for complete steel. `[]` declares that the element needs no steel: its steel is complete at 0 kg, it takes no `reinforcement` entries, and the run raises `NO_STEEL_REQUIRED` naming it, which the reviewer acknowledges. Otherwise any of: `Longitudinal`, `LongitudinalTop`, `LongitudinalBottom`, `LongitudinalTorsion`, `Transverse`, `Web`, `Boundary`, `XTop`, `XBottom`, `YTop`, `YBottom`, `Detailing`, `Accessories`. `Longitudinal` cannot be combined with `LongitudinalTop` or `LongitudinalBottom`. |

## `reinforcement[]`

Each entry supplies one component of one element (or `AllIn`, which covers all of its required components). Every entry needs `objectId`, `component`, `method`, `sourceReference`, `approvedBy`, `approvedAt` and `assumption`. If the approval or the assumption is blank, the mass is unknown.

| `method` | Extra fields | Mass |
|---|---|---|
| `KgPerCubicMetre` | `ratio` (kg/m³, at most the steel density), `concreteBasis` | ratio × concrete volume |
| `VolumeFraction` | `ratio` (0–1), `concreteBasis` | ratio × concrete volume × density |
| `AssignedBars` | `bars: [{ "count", "areaM2", "lengthM" }]` | Σ count × area × length × density |
| `DistributedIntensity` | `intensityM2PerM` (areas only) | intensity × opening-adjusted surface area × density |
| `DemandEquivalent` | `designEvidence: "VerifiedCurrent"`, `designCode`, `domainStartM`, `domainEndM`, `maximumStationGapM`, `stations: [{ "positionM", "areaM2" }]` (longitudinal components only: `Longitudinal`, `LongitudinalTop`, `LongitudinalBottom`, `LongitudinalTorsion`) | Σ max(endpoint areas) × interval × density |

`concreteBasis` is `GrossModeled` or `OpeningAdjusted` (the default). An `AllIn` entry must use `KgPerCubicMetre` or `VolumeFraction` and list exactly the element's required components in `coversComponents`. Other entries leave `coversComponents` empty.

## `policy`

| Field | | Meaning |
|---|---|---|
| `id`, `intendedUse` | required | Policy identity and what reports are for |
| `approvedBy`, `approvedAt` | nullable | Both or neither. Acceptance is blocked until they are set. |
| `steelDensityKgM3` | required | Usually 7850 |
| `linearToleranceM` | required | 1e-6 to 0.001 |
| `planarityToleranceM` | required | From `linearToleranceM` up to 0.01 |

## Plausibility warnings

Values outside these provisional bands are still calculated, but raise an `IMPLAUSIBLE_*` warning that must be acknowledged at acceptance; `IMPLAUSIBLE_DIMENSION` and `IMPLAUSIBLE_STEEL_INTENSITY` are acknowledged per element (`IMPLAUSIBLE_DIMENSION:B1`). The bands are meant to catch unit slips: section 0.1–3 m, area thickness 0.05–2 m, frame length up to 50 m, area extent up to 300 m, steel up to 600 kg per m³ of concrete, and steel density 7000–8500 kg/m³.
