# Development status

Updated: 26 September 2026.

## Delivered: offline takeoff foundation

The solution contains Core, Application, Infrastructure, CLI and an xUnit test project. Two synthetic snapshot formats run end to end through the CLI:

- **Schema 1 (frame concrete):** fixtures A/B, gross modeled beam/column concrete, saved run packages and deterministic replay.
- **Schema 2 (takeoff):** frames plus planar slabs/walls with unioned openings, story allocation, supplied reinforcement with component coverage, recorded overrides, review/acceptance history, and CSV/Excel/JSON export with a hashed manifest.

Production dependencies are Clipper2 (polygon clipping) and ClosedXML (Excel). No CSI assemblies are referenced.

This covers the offline part of roadmap Phases 1 and 3–5. G0 is pending. None of G1–G5 has passed. Gates G2–G4 require live Windows ETABS extraction and reviewed evidence, and G1/G5 still depend on engineering decisions and engineer review. Schema 2 is a development contract, not the final raw/normalized snapshot schema.

| Roadmap area | State |
|---|---|
| Phase 0: build/code selection and Windows API experiments | Pending external environment and engineering decisions |
| Phase 1: SI types, schemas, frame calculations, replay, CI | Implemented offline; policy decisions unapproved |
| Phase 2: ETABS adapter, frame extraction, WPF | Not started (story allocation implemented offline) |
| Phase 3: slab/wall geometry, openings, overlaps, summaries | Implemented offline; A–E engineer fixtures not yet supplied |
| Phase 4: reinforcement integration, ratios, coverage | Calculation implemented for supplied inputs; ETABS result extraction not started |
| Phase 5: overrides, review, reports | Implemented in core/CLI; WPF review tables not started |
| Phase 6: qualification and packaging | Not started |

## Evidence and limits

Local verification on macOS arm64 with SDK 10.0.401: Release solution build succeeded with zero warnings/errors; all 90 xUnit tests passed.

- **Schema 1:** fixture calculates 1.656 m³.
- **Schema 2:** fixture calculates 18.408 m³ gross and 17.808 m³ opening-adjusted concrete, and 1,297.505 kg of known steel, all matching hand calculations. Replay is byte-identical.
- **CLI:** override, accept (including missing-acknowledgment and partial-scope refusals), export, tamper detection, schema mismatch and usage exit codes were exercised manually.
- **CI:** GitHub Actions is configured for Linux and Windows core/CLI checks, including the schema 2 calculate/replay/export flow. It has not been run remotely and does not test ETABS.

The tests cover:

- **Frames:** manual examples, circular columns, inclined lengths, mixed/imperial units, subdivision, missing/invalid geometry and unsupported scope.
- **Areas:** overlapping and out-of-host openings, rotated and warped slabs, and self-intersecting boundaries.
- **Stories and overlaps:** story splits and volume conservation, and overlap/coincidence warnings.
- **Steel:** each steel method, and stale, check-mode, gapped or partial-domain demand.
- **Validation:** rejection rules for invalid snapshots and invalid overrides.
- **Runs:** review states, export integrity and cancellation, CSV formula safety, strict JSON parsing, replay integrity, and atomic writes.

Reports remain Draft until accepted. Complete quantities mean every in-scope object and required component in this input was quantified, not a complete building. Planar areas are rounded to 1 µm in their local plane. Axis-aligned polygons are exact; rotated polygons carry errors of order 10⁻⁶ m².

## Open decisions

- **Beam story convention:** a horizontal beam lying exactly on a story's lower elevation is assigned to the story below it, or to `Unallocated` at the lowest level, unless metadata assigns a story. The engineer should confirm this convention.
- **Measurement policy:** density, tolerances, required steel components per element type and intended report use are fixture values, not approved policy.

## Next tasks

1. Record first Windows/ETABS build, code/edition and reviewer in Phase 0 decisions.
2. Obtain installed API help and approved fixture models; prove read-only attachment and per-field units.
3. Add adapter contracts and raw-source capture from those verified signatures, producing schema 2 snapshots.
4. Obtain the engineer's A–E worksheets and replace synthetic expectations with reviewed fixtures.
5. Begin the WPF review flow on Windows over the existing run/review/export contracts.

Continue pure-core development with synthetic inputs while Windows verification is pending. Do not claim ETABS compatibility based on offline fixtures.
