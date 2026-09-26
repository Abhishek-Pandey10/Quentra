# Quentra — structural engineer meeting brief

**Date:** 26 September 2026  
**Purpose:** Agree the measurement rules, reinforcement basis and validation examples that Quentra's quantities will be judged against.  
**Status:** Draft for discussion. Every rule below is a proposal until you approve it, and nothing has been approved yet.

Background detail is in the [design specification](../quentradesigndoc.md), especially §§28, 31 and 33. This brief is the meeting agenda.

---

## 1. What Quentra is for

Quentra reads a concrete building model and reports how much concrete and reinforcing steel it contains. It lists everything it measured, everything it left out, and why. The intended first use is **design-stage estimating and checking model quantities against other estimates**. Tender/BOQ use requires an agreed measurement standard. Procurement or construction quantities need detailing that an analysis model does not contain.

Quentra does not redesign the building, does not make the analysis model a construction record, and never fills a gap with a silent zero. Anything it cannot measure is reported as **unknown**, with a reason.

## 2. Where the work stands

The calculations run today from saved model snapshots. Live connection to ETABS has not been built yet; it needs a Windows machine with licensed ETABS. The first target version is **ETABS 22.7**.

| Capability | State |
|---|---|
| Straight rectangular beams, rectangular and circular columns | Working offline |
| Flat slabs and walls (any orientation) with openings | Working offline |
| Splitting quantities by story | Working offline |
| Steel from supplied inputs: ETABS longitudinal demand, assigned bars, distributed intensity, approved ratios | Working offline |
| Recorded overrides, exclusions, reviewer acceptance, Excel/CSV/JSON export | Working offline |
| Reading from a live ETABS model | Not started |
| Net concrete (removing member intersections) | Later version |
| Laps, anchorage, hooks, waste, detailing | Not calculated; shown as excluded |

Your design-document examples A–E already give the hand-calculated answers exactly (§5 below). This checks the arithmetic, not the rules, which is why your review is needed.

## 3. What we need from the model

For each concrete object, Quentra reads:

- **Identity and geometry:** its unique name and type, its endpoints or boundary, and its story.
- **Section:** section name and dimensions, or slab/wall thickness.
- **Material:** material name and grade.
- **Openings:** each opening, and which slab or wall it belongs to.
- **Design results:** for beams and columns, the design results, and whether they come from design mode or check mode and are up to date.

It **cannot** see anything the model doesn't contain:

- Foundations represented only by supports or springs.
- Unmodelled openings, drops or thickenings.
- Bar layouts beyond what ETABS stores.

Please tell us how your models typically represent foundations, drops, coupling beams and boundary zones (questions 8–9 below).

## 4. Proposed rules

Items in **bold** are what Quentra does now. Each needs your approval, change or rejection. IDs refer to the assumption register in design specification §31.19.

### Concrete

| ID | Topic | Proposed rule |
|---|---|---|
| — | Measures reported | **Gross modeled volume** (each member's full solid) and **opening-adjusted volume** (after openings). Both keep member intersections, labelled as such. No net, BOQ or procurement figure is shown yet. |
| CQ-001 | Beam and column length | **Modelled 3D axis length.** Inclined members use true length, not story height. Rigid-end zones and offsets are not deducted. |
| CQ-002 | Openings | **Deduct every modelled opening, with no minimum size.** Overlapping openings are merged first, so no void is deducted twice. Openings running past an edge are trimmed to the slab or wall. |
| — | Slab and wall area | **True area in the member's own plane**, not a plan projection, so ramps and inclined walls are measured correctly. |
| CQ-004 | Story allocation | **Columns and walls are clipped at story levels.** Each story takes the interval (lower level, upper level]. A beam at a floor level belongs to the story whose top is that level; a beam at the base level is unallocated unless assigned. Slabs belong to their assigned ETABS story. Nothing is dropped: anything outside the declared stories is reported as "Unallocated". |
| CQ-003 | Intersections (for later net figures) | Foundations first, then columns, walls, slabs, and finally the beam portions outside them. Not applied until net concrete is built. |
| CQ-006 | Foundations | Excluded from the first release and listed as out of scope. |
| — | Unclear cases | Curved, tapered or layered members, unverified thickness, and openings with no clear host are **reported as unsupported, with a reason, and no quantity**. |

### Reinforcement

| ID | Topic | Proposed rule |
|---|---|---|
| RQ-001 | Mass | **Volume × 7850 kg/m³**, as a configurable project assumption. |
| RQ-002 | ETABS beam/column demand | **Integrated only between the ETABS output stations, and only over the declared length,** taking the larger area on each interval. Design-mode results only: check-mode utilisation is never converted to steel. Results must be current. Changing a member's dimensions makes its demand stale. |
| — | Evidence types kept separate | **ETABS demand, bars assigned in the model, and estimates are labelled separately** and never added together for the same component. |
| — | Required components | **Each element type has a list of required components** (for example, beam top, beam bottom and ties). Complete steel is **shown as unknown** while any required component has no source. |
| RQ-004 | Ties and stirrups | Unknown unless a tie layout or an approved ties-only estimate is supplied. Tie steel is not inferred from shear demand. |
| RQ-003 | Laps, development, anchorage, hooks | Excluded from the base quantity and stated in the report. They can be added later as separate approved allowances. |
| RQ-005 | Estimate ratios | **No default ratios.** Each ratio needs a source, an approver, a date, the concrete volume it applies to, and a statement of whether it covers all bars or one component. |
| RQ-006 | Waste | Not in base quantities; a separate procurement figure only if approved. |

### Review

- A report stays **Draft** until a named reviewer accepts it, and that acceptance requires every current warning to be acknowledged.
- A report with gaps can only be accepted as **"Accepted — partial"**.
- Any later change returns the report to Draft.
- Overrides record the original value, the replacement, a reason, the author and the date. The ETABS model is never changed.

## 5. Worked examples (design specification §33)

These are illustrative inputs, not ETABS results. Quentra currently reproduces every figure. The modelling choices it had to make are listed after the table and need your confirmation.

| Example | Concrete | Steel basis | Steel |
|---|---|---|---|
| A — beam 0.30 × 0.60 × 6.00 m | 1.080 m³ | Constant 1000 mm² top + 1500 mm² bottom over 6 m; ties excluded | 117.75 kg |
| B — column 0.40 × 0.40 × 3.60 m | 0.576 m³ | 8-D20 straight bars; ties and laps excluded | 71.03 kg |
| C — slab 6 × 5 × 0.20 m, 1 × 1 m opening | 6.000 m³ gross, 5.800 m³ adjusted | Four layers at 500 mm²/m over 29 m² | 455.30 kg |
| D — wall 4 × 3 × 0.20 m, 1 × 2.1 m door at base | 2.400 m³ gross, 1.980 m³ adjusted | Both faces, 600 + 400 mm²/m each, over 9.9 m² | 155.43 kg |
| E — typical bay (4 columns, 4 beams, slab, wall with door) | 15.192 m³ gross, 14.772 m³ adjusted | No schedule yet | Unknown |

C and D give the same totals when split into several objects across the opening. They also give the same totals when entered in millimetres and inches.

**Modelling choices for you to confirm:**

1. **Member and area placement:** beams and columns are placed on their centroidal axes, and slabs and walls on their mid-planes. In E this puts the beams at z = 2.7 m and measures them centre to centre (6 m and 5 m), keeping the joint overlap in gross.
2. **Required components:** in A–D, the required components match the exclusions stated in §33. That is why their steel shows as complete. Under a real policy, missing ties would make them partial.
3. **Example A's design results** are treated as current design-mode output only to exercise the calculation. Real ETABS design output must be captured from the actual designed model.
4. **Example E's steel:** E needs a component schedule, reviewed by you, before it has an expected steel value.

## 6. Decisions needed

The five highest-priority questions are marked ★. The full questionnaire is in design specification §31.24.

| # | Question | Proposed answer | Decision |
|---|---|---|---|
| 1 ★ | What will the first reports be used for? | Design-stage estimate and model reconciliation | |
| 2 ★ | Which ETABS version/build and design code/edition first? | ETABS 22.7 selected; please confirm the exact build number, license level and the concrete design code/edition used | |
| 3 ★ | Does "steel" mean ETABS required, model-provided, drawing-based or estimated steel? | Demand and approved estimates, shown separately | |
| 4 ★ | Which components are required for each element type? | Beams: top, bottom, ties. Columns: longitudinal, ties. Slabs: four layers. Walls: web; boundary if present. | |
| 5 ★ | Who approves the policy and each issued report? | Named engineer; policy approval first | |
| 6 | Accuracy expected for concrete and steel at this stage? | To be measured against pilot projects; no figure promised | |
| 7 | Minimum opening size to deduct? | None (deduct all modelled openings) | |
| 8 | How are drops, capitals, haunches, hidden and transfer beams modelled and measured? | Unsupported until a method is agreed | |
| 9 | Foundations: needed, how modelled, is SAFE authoritative? | Excluded from first release | |
| 10 | Story rule: (lower, upper] with floor-level members in the story below the level? | As proposed | |
| 11 | Group results by grade? Who approves material-to-grade mapping? | Report ETABS material names until a mapping is approved | |
| 12 | Which story/section/zone/pour breakdowns and existing Excel template? | Story, category, material, section; please share a template | |
| 13 | Is a steel density of 7850 kg/m³ acceptable? | Yes | |
| 14 | Should code minimum steel apply when demand is lower? Does ETABS demand already include it? | Not applied; needs checking per code | |
| 15 | Which checks would convince you the numbers are right? Which errors would make you distrust the tool? | Fixtures A–E, then pilot comparison | |

## 7. What we need from you

1. Answers or owners for the ★ decisions.
2. Review of the A–E modelling choices in §5, and the worksheet columns in design specification §33: raw ETABS geometry, manual geometry, design mode/code, adopted steel basis, expected quantity, software quantity, difference, tolerance, omissions, engineer and date.
3. A small ETABS model of A–E (or permission to build one). Please include its design run, so real demand can replace the illustrative values.
4. One or two pilot projects with independent quantities for comparison.
5. An example of the quantity report or BOQ layout you currently use.

## 8. Feedback

| Item | Accept / change / reject | Comment | Name | Date |
|---|---|---|---|---|
| Concrete rules (§4) | | | | |
| Reinforcement rules (§4) | | | | |
| Review rules (§4) | | | | |
| Worked examples and modelling choices (§5) | | | | |
| Decisions 1–15 (§6) | | | | |
