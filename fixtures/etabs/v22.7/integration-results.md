# ETABS integration results

ETABS 22.7.0.4095; API C:\Program Files\Computers and Structures\ETABS 22\ETABSv1.dll; run 2026-09-27 15:59 UTC; 217 passed, 0 failed.

| Result | Test | Detail |
|---|---|---|
| PASS | 01-single-beam read-only | model file, lock state and present units unchanged by extraction |
| PASS | 01-single-beam knownGrossM3 | expected 1.080000, got 1.080000 |
| PASS | 01-single-beam knownOpeningAdjustedM3 | expected 1.080000, got 1.080000 |
| PASS | 01-single-beam completeGrossM3 | expected 1.080000, got 1.080000 |
| PASS | 01-single-beam completeOpeningAdjustedM3 | expected 1.080000, got 1.080000 |
| PASS | 01-single-beam quantified | expected 1, got 1 |
| PASS | 01-single-beam unsupported | expected 0, got 0 |
| PASS | 01-single-beam invalid | expected 0, got 0 |
| PASS | 01-single-beam outOfScope | expected 0, got 0 |
| PASS | 01-single-beam category Beam | expected 1.080000, got 1.080000 |
| PASS | 01-single-beam story Story1 | expected 1.080000, got 1.080000 |
| PASS | 01-single-beam no other stories | none |
| PASS | 01-single-beam raises ETABS_SOURCE | present |
| PASS | 01-single-beam raises ETABS_DESIGN_UNAVAILABLE | present |
| PASS | 01-single-beam raises PARTIAL_STEEL | present |
| PASS | 01-single-beam does not raise UNALLOCATED_STORY | absent |
| PASS | 01-single-beam does not raise IMPLAUSIBLE_DIMENSION | absent |
| PASS | 01-single-beam does not raise PARTIAL_CONCRETE | absent |
| PASS | 01-single-beam does not raise ETABS_API_CALL_FAILED | absent |
| PASS | 02-single-column read-only | model file, lock state and present units unchanged by extraction |
| PASS | 02-single-column knownGrossM3 | expected 0.840000, got 0.840000 |
| PASS | 02-single-column completeGrossM3 | expected 0.840000, got 0.840000 |
| PASS | 02-single-column quantified | expected 1, got 1 |
| PASS | 02-single-column category Column | expected 0.840000, got 0.840000 |
| PASS | 02-single-column story Story1 | expected 0.840000, got 0.840000 |
| PASS | 02-single-column no other stories | none |
| PASS | 02-single-column does not raise UNALLOCATED_STORY | absent |
| PASS | 02-single-column does not raise PARTIAL_CONCRETE | absent |
| PASS | 02-single-column does not raise ETABS_API_CALL_FAILED | absent |
| PASS | 03-circular-column read-only | model file, lock state and present units unchanged by extraction |
| PASS | 03-circular-column knownGrossM3 | expected 0.687223, got 0.687223 |
| PASS | 03-circular-column completeGrossM3 | expected 0.687223, got 0.687223 |
| PASS | 03-circular-column quantified | expected 1, got 1 |
| PASS | 03-circular-column category Column | expected 0.687223, got 0.687223 |
| PASS | 03-circular-column story Story1 | expected 0.687223, got 0.687223 |
| PASS | 03-circular-column no other stories | none |
| PASS | 03-circular-column does not raise UNSUPPORTED_SECTION | absent |
| PASS | 03-circular-column does not raise PARTIAL_CONCRETE | absent |
| PASS | 04-slab read-only | model file, lock state and present units unchanged by extraction |
| PASS | 04-slab knownGrossM3 | expected 9.600000, got 9.600000 |
| PASS | 04-slab knownOpeningAdjustedM3 | expected 9.600000, got 9.600000 |
| PASS | 04-slab completeGrossM3 | expected 9.600000, got 9.600000 |
| PASS | 04-slab quantified | expected 1, got 1 |
| PASS | 04-slab category Slab | expected 9.600000, got 9.600000 |
| PASS | 04-slab story Story1 | expected 9.600000, got 9.600000 |
| PASS | 04-slab no other stories | none |
| PASS | 04-slab raises ETABS_AREA_STEEL_NOT_EXTRACTED | present |
| PASS | 04-slab raises PARTIAL_STEEL | present |
| PASS | 04-slab does not raise UNALLOCATED_STORY | absent |
| PASS | 04-slab does not raise AREA_OVERLAP | absent |
| PASS | 05-slab-openings read-only | model file, lock state and present units unchanged by extraction |
| PASS | 05-slab-openings knownGrossM3 | expected 9.600000, got 9.600000 |
| PASS | 05-slab-openings knownOpeningAdjustedM3 | expected 8.550000, got 8.550000 |
| PASS | 05-slab-openings completeOpeningAdjustedM3 | expected 8.550000, got 8.550000 |
| PASS | 05-slab-openings quantified | expected 1, got 1 |
| PASS | 05-slab-openings category Slab | expected 9.600000, got 9.600000 |
| PASS | 05-slab-openings story Story1 | expected 9.600000, got 9.600000 |
| PASS | 05-slab-openings no other stories | none |
| PASS | 05-slab-openings raises ETABS_OPENINGS_ASSIGNED | present |
| PASS | 05-slab-openings raises OPENING_CROSSES_HOST | present |
| PASS | 05-slab-openings does not raise OPENING_OUTSIDE_HOST | absent |
| PASS | 05-slab-openings does not raise ETABS_OPENING_NO_HOST | absent |
| PASS | 06-wall read-only | model file, lock state and present units unchanged by extraction |
| PASS | 06-wall knownGrossM3 | expected 4.375000, got 4.375000 |
| PASS | 06-wall knownOpeningAdjustedM3 | expected 4.375000, got 4.375000 |
| PASS | 06-wall completeGrossM3 | expected 4.375000, got 4.375000 |
| PASS | 06-wall quantified | expected 1, got 1 |
| PASS | 06-wall category Wall | expected 4.375000, got 4.375000 |
| PASS | 06-wall story Story1 | expected 4.375000, got 4.375000 |
| PASS | 06-wall no other stories | none |
| PASS | 06-wall raises ETABS_WALL_OPENING_GAPS | present |
| PASS | 07-wall-opening read-only | model file, lock state and present units unchanged by extraction |
| PASS | 07-wall-opening knownGrossM3 | expected 5.250000, got 5.250000 |
| PASS | 07-wall-opening knownOpeningAdjustedM3 | expected 4.725000, got 4.725000 |
| PASS | 07-wall-opening completeOpeningAdjustedM3 | expected 4.725000, got 4.725000 |
| PASS | 07-wall-opening quantified | expected 1, got 1 |
| PASS | 07-wall-opening category Wall | expected 5.250000, got 5.250000 |
| PASS | 07-wall-opening story Story1 | expected 5.250000, got 5.250000 |
| PASS | 07-wall-opening no other stories | none |
| PASS | 07-wall-opening raises ETABS_OPENINGS_ASSIGNED | present |
| PASS | 07-wall-opening does not raise OPENING_CROSSES_HOST | absent |
| PASS | 07-wall-opening does not raise OPENING_OUTSIDE_HOST | absent |
| PASS | 08-one-story-frame read-only | model file, lock state and present units unchanged by extraction |
| PASS | 08-one-story-frame knownGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 08-one-story-frame knownOpeningAdjustedM3 | expected 17.240000, got 17.240000 |
| PASS | 08-one-story-frame completeGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 08-one-story-frame quantified | expected 10, got 10 |
| PASS | 08-one-story-frame category Column | expected 2.240000, got 2.240000 |
| PASS | 08-one-story-frame category Beam | expected 3.600000, got 3.600000 |
| PASS | 08-one-story-frame category Slab | expected 7.200000, got 7.200000 |
| PASS | 08-one-story-frame category Wall | expected 4.200000, got 4.200000 |
| PASS | 08-one-story-frame story Story1 | expected 17.240000, got 17.240000 |
| PASS | 08-one-story-frame no other stories | none |
| PASS | 08-one-story-frame does not raise UNALLOCATED_STORY | absent |
| PASS | 08-one-story-frame does not raise PARTIAL_CONCRETE | absent |
| PASS | 08-one-story-frame does not raise AREA_OVERLAP | absent |
| PASS | 08-one-story-frame does not raise COINCIDENT_FRAMES | absent |
| PASS | 11-units.kN-m knownGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kN-m knownOpeningAdjustedM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kN-m completeGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kN-m quantified | expected 10, got 10 |
| PASS | 11-units.kN-m category Column | expected 2.240000, got 2.240000 |
| PASS | 11-units.kN-m category Beam | expected 3.600000, got 3.600000 |
| PASS | 11-units.kN-m category Slab | expected 7.200000, got 7.200000 |
| PASS | 11-units.kN-m category Wall | expected 4.200000, got 4.200000 |
| PASS | 11-units.kN-m story Story1 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kN-m no other stories | none |
| PASS | 11-units.kN-m matches kN-m per element | largest relative difference from kN-m 0.00E+000 |
| PASS | 11-units.N-mm knownGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.N-mm knownOpeningAdjustedM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.N-mm completeGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.N-mm quantified | expected 10, got 10 |
| PASS | 11-units.N-mm category Column | expected 2.240000, got 2.240000 |
| PASS | 11-units.N-mm category Beam | expected 3.600000, got 3.600000 |
| PASS | 11-units.N-mm category Slab | expected 7.200000, got 7.200000 |
| PASS | 11-units.N-mm category Wall | expected 4.200000, got 4.200000 |
| PASS | 11-units.N-mm story Story1 | expected 17.240000, got 17.240000 |
| PASS | 11-units.N-mm no other stories | none |
| PASS | 11-units.N-mm matches kN-m per element | largest relative difference from kN-m 0.00E+000 |
| PASS | 11-units.kgf-m knownGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kgf-m knownOpeningAdjustedM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kgf-m completeGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kgf-m quantified | expected 10, got 10 |
| PASS | 11-units.kgf-m category Column | expected 2.240000, got 2.240000 |
| PASS | 11-units.kgf-m category Beam | expected 3.600000, got 3.600000 |
| PASS | 11-units.kgf-m category Slab | expected 7.200000, got 7.200000 |
| PASS | 11-units.kgf-m category Wall | expected 4.200000, got 4.200000 |
| PASS | 11-units.kgf-m story Story1 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kgf-m no other stories | none |
| PASS | 11-units.kgf-m matches kN-m per element | largest relative difference from kN-m 0.00E+000 |
| PASS | 11-units.kip-ft knownGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kip-ft knownOpeningAdjustedM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kip-ft completeGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kip-ft quantified | expected 10, got 10 |
| PASS | 11-units.kip-ft category Column | expected 2.240000, got 2.240000 |
| PASS | 11-units.kip-ft category Beam | expected 3.600000, got 3.600000 |
| PASS | 11-units.kip-ft category Slab | expected 7.200000, got 7.200000 |
| PASS | 11-units.kip-ft category Wall | expected 4.200000, got 4.200000 |
| PASS | 11-units.kip-ft story Story1 | expected 17.240000, got 17.240000 |
| PASS | 11-units.kip-ft no other stories | none |
| PASS | 11-units.kip-ft matches kN-m per element | largest relative difference from kN-m 3.70E-016 |
| PASS | 11-units.lb-in knownGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.lb-in knownOpeningAdjustedM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.lb-in completeGrossM3 | expected 17.240000, got 17.240000 |
| PASS | 11-units.lb-in quantified | expected 10, got 10 |
| PASS | 11-units.lb-in category Column | expected 2.240000, got 2.240000 |
| PASS | 11-units.lb-in category Beam | expected 3.600000, got 3.600000 |
| PASS | 11-units.lb-in category Slab | expected 7.200000, got 7.200000 |
| PASS | 11-units.lb-in category Wall | expected 4.200000, got 4.200000 |
| PASS | 11-units.lb-in story Story1 | expected 17.240000, got 17.240000 |
| PASS | 11-units.lb-in no other stories | none |
| PASS | 11-units.lb-in matches kN-m per element | largest relative difference from kN-m 2.11E-016 |
| PASS | 09-multi-story read-only | model file, lock state and present units unchanged by extraction |
| PASS | 09-multi-story knownGrossM3 | expected 39.056000, got 39.056000 |
| PASS | 09-multi-story completeGrossM3 | expected 39.056000, got 39.056000 |
| PASS | 09-multi-story quantified | expected 27, got 27 |
| PASS | 09-multi-story category Column | expected 6.656000, got 6.656000 |
| PASS | 09-multi-story category Beam | expected 10.800000, got 10.800000 |
| PASS | 09-multi-story category Slab | expected 21.600000, got 21.600000 |
| PASS | 09-multi-story story Story1 | expected 13.360000, got 13.360000 |
| PASS | 09-multi-story story Story2 | expected 12.848000, got 12.848000 |
| PASS | 09-multi-story story Story3 | expected 12.848000, got 12.848000 |
| PASS | 09-multi-story no other stories | none |
| PASS | 09-multi-story does not raise UNALLOCATED_STORY | absent |
| PASS | 09-multi-story does not raise PARTIAL_CONCRETE | absent |
| PASS | 10-meshed read-only | model file, lock state and present units unchanged by extraction |
| PASS | 10-meshed knownGrossM3 | expected 26.250000, got 26.250000 |
| PASS | 10-meshed knownOpeningAdjustedM3 | expected 25.125000, got 25.125000 |
| PASS | 10-meshed completeOpeningAdjustedM3 | expected 25.125000, got 25.125000 |
| PASS | 10-meshed category Beam | expected 1.800000, got 1.800000 |
| PASS | 10-meshed category Slab | expected 19.200000, got 19.200000 |
| PASS | 10-meshed category Wall | expected 5.250000, got 5.250000 |
| PASS | 10-meshed analysis mesh is finer than the objects | frame objects 3 -> line elements 5; area objects 8 -> area elements 116 |
| PASS | 10-meshed Beam volume unchanged | gross 1.800000 / 1.800000 m³ |
| PASS | 10-meshed Column volume unchanged | gross 0.000000 / 0.000000 m³ |
| PASS | 10-meshed Slab volume unchanged | gross 19.200000 / 19.200000 m³ |
| PASS | 10-meshed opening-adjusted total unchanged | 25.125000 / 25.125000 m³ |
| PASS | 10-meshed wall gross changes only by its opening deduction | wall gross 5.250000 - opening 0.525000 = 4.725000 m³ after meshing |
| PASS | 12-unsupported read-only | model file, lock state and present units unchanged by extraction |
| PASS | 12-unsupported knownGrossM3 | expected 1.080000, got 1.080000 |
| PASS | 12-unsupported completeGrossM3 | expected unknown, got unknown |
| PASS | 12-unsupported completeOpeningAdjustedM3 | expected unknown, got unknown |
| PASS | 12-unsupported quantified | expected 1, got 1 |
| PASS | 12-unsupported unsupported | expected 6, got 6 |
| PASS | 12-unsupported invalid | expected 1, got 1 |
| PASS | 12-unsupported outOfScope | expected 2, got 2 |
| PASS | 12-unsupported raises ETABS_SECTION_UNSUPPORTED | present |
| PASS | 12-unsupported raises UNSUPPORTED_GEOMETRY | present |
| PASS | 12-unsupported raises UNSUPPORTED_SECTION | present |
| PASS | 12-unsupported raises ETABS_FRAME_ROLE | present |
| PASS | 12-unsupported raises UNSUPPORTED_ROLE | present |
| PASS | 12-unsupported raises ETABS_AREA_PROPERTY_UNSUPPORTED | present |
| PASS | 12-unsupported raises AREA_UNSUPPORTED | present |
| PASS | 12-unsupported raises AREA_INVALID | present |
| PASS | 12-unsupported raises ETABS_NULL_AREA | present |
| PASS | 12-unsupported raises NON_CONCRETE | present |
| PASS | 12-unsupported raises ETABS_OPENING_NO_HOST | present |
| PASS | 12-unsupported raises PARTIAL_CONCRETE | present |
| PASS | 13-designed-frame read-only | model file, lock state and present units unchanged by extraction |
| PASS | 13-designed-frame knownGrossM3 | expected 13.040000, got 13.040000 |
| PASS | 13-designed-frame completeGrossM3 | expected 13.040000, got 13.040000 |
| PASS | 13-designed-frame quantified | expected 9, got 9 |
| PASS | 13-designed-frame category Column | expected 2.240000, got 2.240000 |
| PASS | 13-designed-frame category Beam | expected 3.600000, got 3.600000 |
| PASS | 13-designed-frame category Slab | expected 7.200000, got 7.200000 |
| PASS | 13-designed-frame raises PARTIAL_DEMAND_DOMAIN | present |
| PASS | 13-designed-frame raises ETABS_COLUMN_BARS_MODELED | present |
| PASS | 13-designed-frame raises PARTIAL_STEEL | present |
| PASS | 13-designed-frame does not raise DEMAND_EQUIVALENT | absent |
| PASS | 13-designed-frame does not raise ETABS_DESIGN_UNAVAILABLE | absent |
| PASS | 13-designed-frame does not raise ETABS_DESIGN_NOT_CURRENT | absent |
| PASS | 13-designed-frame does not raise ETABS_STEEL_UNAPPROVED | absent |
| PASS | 13-designed-frame does not raise STEEL_UNAVAILABLE | absent |
| PASS | 13-designed-frame steel ModelAssignedEquivalent | expected 69.061160, got 69.061163 |
| PASS | 13-designed-frame design demand integration | 15 demand components recomputed; largest difference 0.00E+000 kg |
| PASS | ETABS with no saved model | ETABS (process 636) has no saved model open (it reports '(Untitled)'). Open or save the model in ETABS, then extract again. |
| PASS | ETABS closed during extraction | ETABS (process 26616) closed or stopped responding during extraction (NullReferenceException: Object reference not set to an instance of an object.). Nothing was written; reopen the model and extract again. |
