# Quentra — design review and development roadmap

**Date:** 26 September 2026  
**Status:** Proposed implementation plan; no software or ETABS capability certified yet.  
**Baseline:** [Quentra design document](quentradesigndoc.md), review draft 1.0.  
**Product:** A Windows desktop application for traceable concrete and reinforcing-steel quantity estimates from ETABS.

## 1. Review verdict

**The design is a sound basis for development, but it is not yet a validated implementation specification.** Its quantity formulas, separation of measurement bases, treatment of missing reinforcement, and isolated ETABS adapter are appropriate. Proceed with a small compatibility investigation and a concrete-first implementation. Do not promise complete construction steel, net building concrete, or broad ETABS compatibility in the first release.

This review compared the workspace design with the supplied technical specification in Downloads. Their content is equivalent after formatting differences. The workspace currently contains the design document, with no implementation, test suite, or supplied ETABS fixtures. The engineer meeting brief mentioned in §32 is absent from this workspace.

Review performed: document consistency, dimensional reasoning, independent calculation of the worked examples, and targeted checks against official CSI and Microsoft documentation. No running Windows ETABS session, installed API assembly/help, licensed integration test, or structural-design validation was available. Public historical API documentation confirms contracts, not behavior in a modern installed build.

### What is correct and should remain

- Keep gross modeled, opening-adjusted, net physical, BOQ, and procurement quantities separate.
- Use actual 3D member length and actual planar slab/wall area. Do not use stiffness modifiers to scale geometric volume.
- Count model objects once; their analysis mesh does not create additional concrete.
- Union and clip openings before deductions; do not subtract overlapping voids twice.
- Keep required/design-demand, model-assigned, estimated, and detailed steel as separate sources.
- Integrate longitudinal area over its verified length domain. Shear reinforcement area per length requires tie geometry before conversion to mass.
- Treat missing quantities as unknown, preserve exclusions, and expose component completeness.
- Keep raw evidence immutable; apply overrides and engineering rules separately, with review invalidation after changes.
- Isolate all CSI calls from the calculation core and keep extraction read-only.

CSI's beam summary exposes longitudinal areas and transverse area-per-length demands; it does not provide a complete bar schedule. Its column summary explicitly distinguishes design-required area from check-mode utilization. These support the document's approach. See [CSI beam summary](https://docs.csiamerica.com/help-files/etabs-api-2016/html/c3556046-fd3f-4559-7659-966eb731ea4c.htm) and [CSI column summary](https://docs.csiamerica.com/help-files/etabs-api-2016/html/d85059d5-a38c-d7d7-69dc-4bdc7fe21b56.htm).

### Required clarifications before feature acceptance

| ID | Finding | Resolution in this roadmap |
|---|---|---|
| R01 | §1 makes beam/column demand conditional, while §29 makes it a release requirement. | Distinguish a concrete-only internal alpha from the full MVP. Full MVP requires verified beam/column demand for the chosen build/code. If unavailable, record a deliberate scope revision; do not quietly replace it with estimates. |
| R02 | §26 puts all E01–E15 investigations in Phase 0, including future slab/wall design and 100k-object work. | Investigate only MVP-critical contracts first. Defer E10/E11 result adapters and the 100k production target. Detect unsupported objects from the start. |
| R03 | Runtime compatibility remains a hypothesis. | Prefer .NET 10 for UI/core; validate exact ETABS interop before committing. Use a separate compatible adapter process only if evidence requires it. |
| R04 | Development is currently in a macOS workspace; WPF and live ETABS work need Windows. | Develop pure calculations and snapshot replay independently; provide a Windows test machine before the connection milestone. A Mac-only implementation cannot validate this product. |
| R05 | “Known concrete volume coverage” has no reliable denominator when unsupported volume is unknown. | Report count coverage and known volume separately. Show volume coverage as unavailable unless an independent denominator and its basis are established. |
| R06 | Complete steel depends on a defined required-component set. | Version that set by element type and report purpose. Missing ties, boundary bars, or detailing cannot disappear from completeness because extraction omitted them. An all-in estimate remains estimated even when its declared scope is covered. |
| R07 | Draft, partial, accepted, and final report behavior needs an explicit contract. | Track review state independently from completeness. Permit accepted partial reports only for an explicitly declared scope, with persistent exclusions; they cannot claim complete building quantities. |
| R08 | Geometry tolerances and throughput figures are proposals, not demonstrated guarantees. | Adopt fixture-specific tolerances before acceptance; benchmark on named hardware and model scope. Never market the proposed timings or numerical tolerances as estimating accuracy. |
| R09 | §32 refers to an accompanying engineer meeting brief that is not present. | Create the brief and decision register in Phase 0, drawing from §§28/31/33. |
| R10 | The proposed 11+ project split is larger than needed for the first working flow. | Begin with five production projects and clear internal modules. Split further when dependencies or packaging require it. This changes organization, not engineering boundaries. |

WPF is Windows-only according to [Microsoft's WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/). CSI's ETABS 22 release notes describe .NET Standard 2.0 API assemblies and client compatibility through .NET 8; they do not certify .NET 10. See [CSI ticket 10489](https://www.csiamerica.com/software/ETABS/22/ReleaseNotesETABSv2210plus2200.pdf). Microsoft's support schedule ends .NET 8 support on 10 November 2026 and .NET 10 support on 14 November 2028, so the design's conditional .NET 10 preference is reasonable. See [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

### Arithmetic checks

The following values were independently recalculated. They validate illustrative arithmetic only; the steel inputs are not verified ETABS design output.

| Fixture | Concrete | Steel basis and checked mass |
|---|---:|---|
| A: 300 × 600 mm beam, 6 m long | 1.080 m³ | Constant 2,500 mm² total longitudinal area: 117.75 kg |
| B: 400 × 400 mm column, 3.6 m high | 0.576 m³ | Eight idealized D20 straight bars: 71.02512671 kg |
| C: 6 × 5 m slab, 1 m² opening, 200 mm thick | 5.800 m³ | Four intensities of 500 mm²/m: 455.30 kg |
| D: 4 × 3 m wall, 1 × 2.1 m door, 200 mm thick | 1.980 m³ | Combined web intensity of 2,000 mm²/m: 155.43 kg |
| E: Explicit bay in §33 | Gross 15.192; opening-adjusted 14.772; net 12.972 m³ | No complete steel reference supplied; component schedule still needed |

The bay's net volume was also checked through independent axis-aligned cell partitioning of the specified solids and door void. The §31.23 override example also agrees: 1.188 m³ concrete, 72.32676 kg longitudinal equivalent, and 86.12676 kg after the illustrative tie allowance. No arithmetic correction was identified in these examples.

## 2. Product and release boundaries

**Primary user:** Structural engineer or estimator performing design-stage quantity reconciliation.

**Core workflow:** Select ETABS instance → inspect scope → extract snapshot → calculate → review assumptions and omissions → export a traceable report.

| Release | Included | Explicit boundary |
|---|---|---|
| Internal alpha | One exact ETABS build; rectangular beams, rectangular/circular columns; frame concrete; JSON replay and basic review | Internal development milestone, not full MVP |
| Concrete beta | Supported planar uniform slabs/walls, openings, story allocation, summaries, trace and draft workbook | Steel may remain unknown; member intersections remain in modeled totals |
| Full MVP pilot | Concrete beta plus verified beam/column longitudinal demand, approved estimates, component coverage, overrides, review, Excel/CSV/JSON, installer | One tested build/runtime/code combination; no net concrete or full construction steel claim |
| V1 | Separately qualified net concrete, assigned column cages, selected slab/wall design adapters, modeled mats, additional builds | Each feature needs its own source contracts and engineering fixtures |
| Later | BBS/detailing imports, SAFE, complex geometry, code-specific detailing, carbon and additional reporting | Prioritize from pilot evidence |

MVP supports straight prismatic frames, valid concave planar polygons, verified uniform physical thickness, and correctly associated coplanar openings. Unsupported curves, tapers, layered/equivalent sections, ambiguous overrides, drops, and duplicate/overlap candidates remain visible with reasons. Foundations are excluded from MVP even if their analytical representations are readable.

No default steel ratio is automatically applied. Ratios must declare whether they represent all steel or a component, their concrete basis, evidence, applicability, and approval. An all-in estimate replaces corresponding contributions; it cannot be added to longitudinal demand for the same scope.

## 3. Initial architecture

```text
src/
  Quentra.Core/          SI types, geometry, quantities, units, validation
  Quentra.Application/   extraction orchestration, policies, runs, review
  Quentra.Etabs/         CSI adapter, raw DTOs, capability and unit contracts
  Quentra.Infrastructure/ snapshot storage, JSON/CSV/Excel, logging
  Quentra.Desktop/       Windows WPF views and view models
tests/
  Quentra.Core.Tests/
  Quentra.Application.Tests/
  Quentra.Etabs.IntegrationTests/
  Quentra.Reporting.Tests/
fixtures/
  synthetic/
  etabs/                controlled models and provenance, subject to sharing rights
docs/
  decisions/
  engineering/
  compatibility/
```

Core has no CSI or WPF dependency. Application owns interfaces and workflows. The ETABS adapter and infrastructure implement those interfaces; Desktop composes them. CSI calls run serially on an appropriately initialized interop thread. Parallel calculation is allowed only after extraction. Add `Quentra.EtabsHost` only if runtime isolation is necessary.

Persist versioned raw and normalized snapshots, quantities, policies, overrides, and review records in a run package. Define canonical content hashing separately from timestamps and run IDs so identical inputs can produce comparable calculation hashes. Preserve source units and conversion provenance. Reopening a snapshot must reproduce quantities without ETABS; an incompatible schema must fail clearly rather than silently change results.

## 4. Delivery phases and acceptance gates

**Development update, 26 September 2026:** The offline core now covers Phase 1 and the non-ETABS parts of Phases 3–5: frame and planar slab/wall concrete with openings, story allocation, supplied reinforcement with component coverage, overrides, review and CSV/Excel/JSON export, all through the CLI with tests. Phase 0 live ETABS verification and all acceptance gates remain pending; synthetic fixtures do not pass any gate. See [development status](docs/DEVELOPMENT_STATUS.md) and [run instructions](README.md).

Effort ranges are planning estimates for one experienced full-time C# developer with a reviewing structural engineer available regularly. They are not calendar commitments. Windows access, fixtures, reviews, and unexpected API behavior can extend elapsed time. Re-estimate after Phase 0.

### Phase 0 — Resolve engineering scope and integration feasibility

**Estimate:** 1–2 developer-weeks. **Owners:** Developer, structural engineer, product owner.

1. Select the first exact ETABS build, Windows environment, runtime/architecture, license configuration, and design code/edition.
2. Record intended report use, component requirements, story ownership, volume basis, exclusions, density, integration method, and approval responsibilities.
3. Prepare the missing engineer meeting brief and manual A–E geometry worksheets. Keep unsupplied steel expectations pending.
4. Inspect installed API help/assemblies. Run a minimal read-only attachment and capability harness on Windows.
5. Verify process selection, units, inventory, property overrides, frame/area geometry, beam/column result semantics, and nonmutation. Map results to E01–E09 and MVP-relevant E12–E14.
6. Capture sample payloads in two unit systems. Measure a small representative extraction before adopting throughput targets.

**Deliverables:** Decision register, compatibility/runtime decision, field-unit registry, capability matrix, fixture manifest, and captured evidence.

**Gate G0:** Required geometry contracts demonstrated; runtime route selected; steel capability known or explicitly marked blocked; no unidentified units in enabled quantities. Engineering policy remains visibly draft until approved. If Windows access is unavailable, pure-core work may proceed with synthetic fixtures, but G0 cannot pass.

### Phase 1 — Build the domain and offline calculation foundation

**Estimate:** 1–2 weeks. **Dependency:** Draft decisions from Phase 0; can overlap while live checks proceed.

- Scaffold projects and CI; separate portable core tests from licensed Windows integration tests.
- Implement typed SI inputs, source references, classification, nullable quantities, warning codes, and independent review/completeness states.
- Define schemas for raw snapshots, normalized snapshots, settings, and calculation results.
- Implement frame area/length math, dimensional conversions, and synthetic fixture replay.
- Define canonical serialization and reject invalid/nonfinite values, broken references, or incompatible schema versions.

**Gate G1:** A synthetic beam/column snapshot reproduces checked quantities and source traces without ETABS; unit changes preserve SI results; unknown steel remains unknown through serialization and aggregation.

### Phase 2 — Deliver live frame concrete: internal alpha

**Estimate:** 2–3 weeks. **Dependencies:** G0 geometry/runtime acceptance and G1.

- Implement explicit instance selection, serialized getters, return-contract validation, and safe disconnect.
- Extract frames, endpoints, materials and effective overrides, sections, local axes, offsets, stories, and stable source identities.
- Capture start/end model context; reject detected model changes and incomplete extraction.
- Calculate supported frame gross volumes, story allocations, exclusions, and reconciled summaries.
- Deliver a minimal WPF connection/results/trace flow and JSON run export.

**Gate G2:** Live beam/column fixtures match manual quantities in two unit systems; split-member/story conservation passes; wrong-instance, host-close, missing-property, no-design, and unsupported-section cases are handled. ETABS stays open and its tested state remains unchanged.

### Phase 3 — Deliver slabs, walls, and openings: concrete beta

**Estimate:** 2–3 weeks. **Dependency:** G2.

- Extract canonical area objects with thickness, material, opening, and placement evidence.
- Validate planarity and polygons; calculate in local planes; union and clip eligible openings.
- Handle concave geometry, edge-touching doors, rotated/inclined planes, and multistory wall allocation.
- Detect ambiguous hosts, physical-thickness ambiguity, coincident objects, and intersecting areas.
- Add category/story/material/section summaries and an early draft workbook for engineer feedback.

**Gate G3:** A–E gross/opening-adjusted concrete passes; C/D remain invariant under equivalent object subdivision and analysis meshing. Every unsupported record is accounted for. Overlaps are disclosed; net fields remain unavailable.

### Phase 4 — Add verified reinforcement and estimates

**Estimate:** 2–3 weeks. **Dependencies:** Verified result contracts from G0, snapshot pipeline, and G3 summaries.

- Extract beam/column stations, components, combinations, errors, design/check mode, and section/freshness evidence.
- Integrate supported longitudinal demands over their verified domains. Keep unreported ends, gaps, torsion, and detailing omissions explicit.
- Implement approved all-in and component-only ratios with distinct dimensional types and basis checks.
- Add source selection, component coverage, known mass, and nullable complete mass; prevent overlapping source contributions.
- Preserve check-mode utilization as utilization. Defer provided cage mass unless separately qualified for this release.

**Gate G4:** Demand inputs match reviewed ETABS UI/table evidence for the chosen code/build. Synthetic integration examples pass independently. Missing/stale/failed/check-mode cases cannot produce misleading complete steel. A ratio plus an overlapping demand result is rejected or explicitly replaced.

### Phase 5 — Complete review and reports

**Estimate:** 2–3 weeks. **Dependencies:** G3 and G4; UI/report groundwork begins earlier.

- Complete virtualized element tables, filters, warnings, trace details, assumptions, and scope selection.
- Implement reasoned overrides bound to snapshots; recompute and invalidate review when relevant inputs change.
- Freeze accepted run revisions. Preserve review history without rewriting source evidence.
- Finish Excel, normalized CSV tables with manifest, and full-fidelity JSON exports.
- Include basis, units, exact compatibility context, component coverage, exclusions, assumptions, and review status in reports.
- Protect spreadsheet text fields; use atomic file writes; keep a run retryable after export failure.

**Gate G5:** An engineer can trace any reported quantity to inputs and rules. Workbook/CSV/JSON totals reconcile with stored quantities. Filters do not silently change export scope. Draft or partial states remain visible after export and reopening.

### Phase 6 — Qualify and package the MVP pilot

**Estimate:** 2–3 weeks. **Dependencies:** G0–G5.

- Run the complete fixture matrix and compare pilot projects with independent quantities on the same measurement basis.
- Test multiple instances, privilege mismatch, unit/model changes, busy/closed host, cancellation, large payloads, and export failure.
- Benchmark agreed 1k/10k model scopes; establish a measured supported ceiling. Treat 100k as a later scale gate.
- Produce a Windows installer, dependency/license inventory, prerequisite diagnostics, release notes, and recovery instructions.
- Verify clean installation and upgrade preservation of run packages; arrange signing for distributed releases.

**Gate G6:** Engineer accepts applicable rules and fixtures; no unresolved critical quantity defects; exact build/runtime/code matrix published; complete pilot evidence and clean-machine checks pass. Do not imply certification for untested ETABS patches.

**Planning envelope:** Approximately 12–19 developer-weeks if phases are predominantly sequential, plus external waiting time. This includes integration and packaging, not merely the formulas. Phase 0 findings may materially change the estimate.

## 5. Validation contract

| Area | Required evidence |
|---|---|
| Arithmetic | A–D checks above, circular columns, nonconstant demand integrals, and dimensional error cases |
| Geometry | Translation/rotation invariance, reversed winding, concavity, overlapping openings, edge-touching voids, invalid/warped polygons |
| Conservation | Story fragments sum to element quantities; summaries sum to included detail; mesh does not add concrete |
| Units | At least two source systems, including area versus area-per-length and correct density dimensions |
| Completeness | Missing inputs stay null; out-of-scope, unsupported, estimated, and unknown records remain distinguishable |
| Integration | Exact installed API/build/code evidence; status and array validation; source-to-object joins; read-only behavior |
| Persistence/review | Deterministic replay; hash/schema validation; changes invalidate acceptance; prior run revisions remain reproducible |
| Reporting | Units and basis visible; detail/summary parity; spreadsheet-safe text; rounding only at presentation |
| Deployment | Clean Windows machine, prerequisite failures, upgrade preservation, safe host disconnect |

Adopt the baseline's proposed numerical tolerances only after confirming absolute floors, model scale, and source precision. Numerical agreement does not establish the accuracy of a steel estimate. Final acceptance records actual errors and tolerances per fixture, with reviewer and date.

## 6. V1 sequence after the pilot

1. **Net concrete for a limited supported solid set.** Verify insertion/offset placement; implement union or deterministic ownership with a deduction ledger. Pass the 12.972 m³ bay reference, triple intersections, ownership-invariant totals, and material-interface rules.
2. **Model-assigned column longitudinal cages.** Verify counts/catalogue areas, design/check meaning, length extent, and declared detailing exclusions.
3. **Wall and slab design adapters, independently.** Complete E10/E11; verify region mapping, face/direction units, boundary components, strip overlap, and table schemas before conversion to mass.
4. **Modeled mats and thickness zones.** Add only explicit supported physical geometry with reviewed replacement/addition rules.
5. **Additional builds and larger models.** Extend compatibility through regression evidence; add streaming or alternate persistence only when benchmarks justify it.

Do not make all V1 capabilities one release dependency. Ship individually qualified capabilities with their limitations.

## 7. Immediate backlog and outstanding decisions

| Priority | Task | Owner | Completion evidence |
|---|---|---|---|
| P0 | Select Windows test environment and exact ETABS build | Product owner + developer | Accessible licensed host and recorded version |
| P0 | Choose initial report purpose and code/edition | Engineer + product owner | Decision register entry |
| P0 | Prepare engineer meeting brief and assumption register | Developer + engineer | Reviewable documents with pending decisions explicit |
| P0 | Establish A–E fixture files and manual baselines | Engineer | Geometry worksheets; actual design output captured separately |
| P0 | Verify runtime attachment and getter/unit contracts | Developer | Phase 0 evidence and capability matrix |
| P1 | Scaffold Quentra projects and snapshot schemas | Developer | Buildable solution and portable fixture replay |
| P1 | Deliver beam/column model-to-JSON flow | Developer | G2 demonstration |
| P1 | Obtain an example report/workbook from intended users | Product owner + estimator | Agreed minimum columns and grouping |

Decisions still needed: exact host/build and code, engineering reviewer, scope and completeness requirements, measurement conventions, approved ratio sources, numerical acceptance tolerances, and pilot models. These are implementation/release dependencies; they do not prevent creation of this roadmap or development of provisional pure-core components.

**First implementation target:** Select a running ETABS model, read a rectangular beam and a column without changing the host, calculate their gross concrete, and export a replayable JSON snapshot with source units and trace. Establish this path before expanding geometry, steel, and UI scope.
