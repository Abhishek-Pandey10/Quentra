# Review of the Quentra benchmark corpus

**Reviewed:** 27 September 2026. **Environment:** macOS arm64, .NET SDK 10.0.401, Python 3.14.7, Shapely 2.1.2, jsonschema 4.26.0.  
**Verdict:** Keep and integrate it as an independent synthetic regression corpus. It is useful now for numerical geometry comparisons, but is not yet a drop-in acceptance suite or an authoritative reinforcement reference.

## Fix status — suite 1.1.0

The four reference-calculator defects identified below are now fixed: undesigned demand, negative/nonfinite demands, wrong-member result references, and inconsistent result provenance are rejected. Duplicate stations/results, missing fields and overflow are also guarded. Eight new DGUARD fixtures and 13 Python regression tests cover these changes. Generation now checks output invariants before writing any files, and expected quantity comparisons tolerate declared numerical rounding while keeping nonnumeric data and counts exact.

Verification after the fixes:

- **132 benchmark cases** pass schema/invariant validation; **361 hand assertions** and both equivalence groups pass.
- `generate.py --check` passes for all **399 generated files**.
- All **four original adversarial probes** now reject the unsafe steel/snapshot and block finalization.
- The geometry-only CLI audit matches **111 cases**, with **21 explicit exclusions**. The extra exclusions are the two new fatal-provenance cases, which have no expected geometry totals.
- All **143 Quentra tests** and **13 Python regression tests** pass locally.
- The original 124 cases retain their quantities and behavior within their original tolerances; generator/version metadata is updated.

CI now runs the geometry audit on Linux and Windows and the Python reference checks in a separate job. These workflow changes have been verified locally, not run remotely. Production quantity algorithms were not changed. The policy/schema differences documented below remain deliberate integration work, not bugs hidden by changing expected outputs.

**The remainder records the original review findings and evidence, before these fixes.**

## What the current codebase already provides

The existing solution has Core, Application, Infrastructure, CLI and xUnit projects. Current code supports frames, planar slab/wall geometry with unioned openings, story allocation, supplied steel methods, coverage, overrides, review and JSON/CSV/Excel reports. It uses Clipper2 for polygon operations. There is no live ETABS adapter, WPF application or Windows packaging yet.

The current Release build passes with zero warnings/errors. **143 existing xUnit tests pass**. The status document's earlier count of 134 predates the current source. This review used the current working tree, including existing uncommitted work; that work was preserved.

The benchmark adds a materially different test source: **124 cases**, **339 hand-check assertions**, **two equivalence groups**, 28 invalid-input fixtures and five building fixtures. Its expected-value generator uses Python/Shapely rather than Quentra/Clipper2. That separation is valuable for catching shared assumptions in our existing fixture calculations. Its comment describing a C# “exact slab sweep” does not describe this repository's actual geometry implementation.

## Checks performed

| Check | Observed result | What it establishes |
|---|---|---|
| Current solution build and tests | Build clean; 143/143 tests passed | Current application baseline |
| Benchmark `validate.py` | All 124 cases passed their declared schema expectations and output invariants; zero ETABS captures | Corpus structure and internal consistency, not agreement with Quentra |
| Generator hand checks | 339 checks and two equivalence groups passed in memory | Generated numbers agree with the hand-check expressions supplied with the cases |
| Generator `--check` | Two `expected.json` files differ under this Python environment | Byte-for-byte regeneration is not portable as presently checked; differences are tiny floating-point changes |
| Geometry-only bridge to the real Quentra CLI | **105 cases matched; 19 explicitly not compared; zero remaining geometry mismatches/rejections in the selected scope** | Per-element gross/opening-adjusted volume, available area/length fields, and model concrete subtotals match within benchmark tolerances |
| Four adversarial reference-calculator probes | All four expose missing validation in the benchmark evaluator | The evaluator needs hardening before it becomes a reference for new steel/rejection behavior |

The geometry comparison covered **1,251 element records**, including null quantities where expected. It includes all eight frame, ten slab, eight wall and five unit cases, plus the concrete geometry in four building cases (31, 280, 710 and 52 elements). Comparisons of reinforcement-category fixtures here cover their **concrete geometry only**, not their reinforcement results.

The 19 exclusions are listed individually in the comparison JSON. They involve duplicate identity policy, analytical/mesh children, ambiguous/non-concrete materials, missing/unsupported sections, missing stories, unsupported types/units, or invalid schema/provenance. BLDG-E, which deliberately includes ingestion defects, is among these exclusions. They are not passes and should become separately mapped ingestion tests.

The initial translation rejected AGG-001 and BLDG-C because binary subtraction of story top minus height created microscopic apparent overlaps. The review bridge now performs that source convention in decimal arithmetic before converting to doubles. Production story validation was not changed. This is a useful future adapter regression case.

## Findings requiring correction or an explicit mapping

### 1. The reference calculator can trust invalid design results

In [`evaluator.py`](../quentra-benchmarks/tests/generators/quentra_bench/evaluator.py), the `RequiredArea` branch around lines 693–737 checks station endpoints and handles `Stale`, but does not adequately validate design status, ownership, station values or result provenance.

Starting with RREQ-003, each of these mutations was made **only in memory**. Each remains valid under the benchmark JSON schema:

| Mutation | Reference-calculator output | Required correction |
|---|---|---|
| Change result status to `NotDesigned` | 120.9057 kg; finalization allowed | Reject/unquantify demand that has not been designed |
| Make top and bottom required areas negative | **−94.2 kg**; finalization allowed | Reject nonfinite/negative station demands before integration; never emit negative mass |
| Change design-result origin to `captured` while snapshot remains synthetic | 120.9057 kg; finalization allowed | Check result-level provenance against snapshot provenance |
| Point B1 to a design result owned by `ANOTHER_MEMBER` | 120.9057 kg; finalization allowed | Require result-to-member identity reconciliation |

These are newly exercised gaps, not claims that the supplied 124 committed fixtures contain negative steel. Their existing invariant validator catches negative expected totals if asked to validate such an output; it does not itself add the missing cases or stop the evaluator producing that output.

**Recommendation:** Add these as explicit negative fixtures, correct the evaluator, and validate generated outputs before writing them. Do not weaken Quentra's checks to match an unsafe oracle.

### 2. The benchmark schema is not the application schema

Benchmark `schemaVersion` is the string `"1.0"`, with `materials`, `sections`, `elements`, source warnings and provenance. Quentra uses integer schema 1 for frame snapshots or integer schema 2 with explicit frames/areas, metadata, policy, component requirements and approvals.

There is no supplied C# benchmark harness despite comments referring to one. The files cannot be passed directly to `quentra calculate`. Add a test-only adapter with documented mappings. Keep the benchmark corpus version separate from production schema versions.

The review bridge converts input geometry only, preserves native metre/mm/ft/in source units, and converts centimetres explicitly because Quentra has no native centimetre enum. That case validates the converted physical result, not native centimetre ingestion. It adds an **unapproved synthetic policy**, supplies no reinforcement, and never labels input as ETABS-verified.

### 3. Several expected behaviors conflict with the present design

| Topic | Benchmark convention | Current Quentra behavior / integration decision |
|---|---|---|
| `netConcreteM3` | Gross minus openings; member overlaps remain | Map to `OpeningAdjustedM3`. Never expose this as net physical concrete. |
| Stale demand, e.g. RREQ-004 | Includes 158.0676 kg, blocks finalization | Quentra leaves stale demand unquantified. Preserve this behavior and classify the case as a policy difference. |
| Exact steel | `ProvidedBars` and `ManualOverride` automatically counted as exact | Source category alone does not prove detailing completeness or accuracy. Keep evidence, completeness and review separate. |
| Completeness | Any successful supplied element steel components can mark steel complete | Quentra checks an explicitly required component set. Two bottom bars alone do not establish a complete member cage. |
| Duplicate IDs | Identical copies deduplicated; conflicting copies rejected individually | Quentra rejects duplicate snapshot IDs. Adapter-level handling needs an explicit decision and preserved raw evidence. |
| Story grouping | Entire element follows its story label | Quentra clips vertical/inclined geometry into elevation bands and assigns slabs by level. Do not silently force one convention onto the other. |
| Warnings | Exact `(code, subject)` sets, sometimes requiring no warnings | Quentra has different codes plus persistent basis/scope warnings. Compare relevant mapped diagnostics, not raw set equality. |
| Finalization | Warning-blocking rules alone | Quentra requires approved policy, reviewer, acknowledgments, and explicit partial-scope acceptance. Benchmark `snapshotStatus: Accepted` is not engineer approval. |
| Plausibility | Different dimension/density bands and some hard rejection | Existing bands are also provisional. Agree a policy, then version the cases affected. |
| Detailing algorithms | Hoops, crossties, discrete meshes, fixed masses, several demand integrators | Not all are production features. Keep them in a capability backlog; do not precalculate masses in a translator and claim the production algorithm passed. |
| Mesh openings | Mesh bars explicitly continue across openings | Quentra's distributed intensity integrates over remaining surface area. These represent different input/measurement methods. |

### 4. Regeneration needs a reproducible environment or tolerant comparison

`generate.py --check` compares complete file text. On this Mac/Python 3.14.7, RWALL-003 and BLDG-C expected files differ only in last-place floating-point values; the largest observed difference is approximately **5.82 × 10⁻¹¹ kg**, well below the suite's 0.001 kg tolerance.

The benchmark README names Python 3.11 but does not pin its dependencies. Pin the supported Python and dependency versions for generation, or compare numeric JSON fields with declared tolerances while checking nonnumeric content exactly. Keep committed expectations unchanged pending that choice; do not automatically regenerate golden values merely to make a local text comparison pass.

### 5. It is primarily a correctness corpus, not a scale or ETABS test

The largest building has 710 elements. It is useful for combined geometry and aggregation coverage but does not demonstrate 10k/100k throughput, UI responsiveness, memory use, COM behavior, model nonmutation or version-specific table semantics. Add distinct scale benchmarks and retain the Windows/ETABS 22.7 test plan.

There are no captured ETABS fixtures. Synthetic source flags and a version string are evidence labels, not certification of the API or the engineering assumptions.

## Recommended integration order

1. Keep the corpus unchanged as an external reference and record its version.
2. Promote the compatible geometry comparisons into the regular .NET test suite through a test-only adapter, with exclusions and their reasons explicit.
3. Harden the Python reference calculator using the four probes above, then add broader malformed station and source-ownership cases.
4. Map supported straight-bar and ratio cases directly to existing steel APIs. Add separate demand tests only for the supported integration policy and evidence states.
5. Record policy differences separately from product defects and unsupported capabilities. Agree warning, completeness, material grouping and ingestion rules before requiring whole-output parity.
6. Implement remaining detailing features only after their measurement conventions are approved; then activate the corresponding cases.
7. Add 1k/10k scale models and ETABS 22.7 captures independently. Do not relabel synthetic benchmark success as live certification.

## Reproducing this review

The two audit scripts are deliberately outside production code. They do not modify source fixtures or expected outputs. Following the fixes, both are included in CI with their stated scopes.

```sh
# Use the local SDK/environment described in README.md to build first.
.tools/dotnet/dotnet build Quentra.slnx -c Release
.tools/dotnet/dotnet test Quentra.slnx -c Release --no-build

# Geometry audit uses only Python's standard library; choose a new output directory.
python3 scripts/audit_benchmark_geometry.py --output output/benchmark-geometry-audit

# Python reference checks require Shapely and jsonschema.
.tools/benchmark-venv/bin/python quentra-benchmarks/tests/generators/validate.py
.tools/benchmark-venv/bin/python quentra-benchmarks/tests/generators/generate.py --check
.tools/benchmark-venv/bin/python scripts/audit_benchmark_oracle.py
```

At the original review, the oracle audit exited 1 for the four issues and the generator text check exited 1 for two floating-point differences. Both now exit 0. The updated geometry audit exits 0 with 111 matches and 21 exclusions.

Local evidence: [geometry comparison](../output/benchmark-review-2026-09-27-normalized/comparison.json), [oracle probes](../output/benchmark-review-2026-09-27/oracle-probes.json). These output files are ignored by Git; the audit scripts reproduce them. Production source and benchmark expected values were not changed by this review.
