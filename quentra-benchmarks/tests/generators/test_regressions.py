"""Run with python -m unittest discover -s tests/generators -p 'test_*.py'."""
import copy
import math
import unittest
from unittest.mock import patch

from quentra_bench.cases import reinforcement, design_guards
from quentra_bench.evaluator import evaluate
from quentra_bench.comparison import expected_equal
from quentra_bench.case import run_case
from validate import invariants


class DesignValidationTests(unittest.TestCase):
    def setUp(self):
        self.base = copy.deepcopy(next(c for c in reinforcement.all_cases() if c.id == "RREQ-003").snapshot)

    def assert_unknown(self, snapshot, code="INVALID_DESIGN_RESULT"):
        result, _ = evaluate(snapshot)
        self.assertIsNone(result["elements"][0]["steelKg"])
        self.assertFalse(result["finalization"]["allowed"])
        self.assertIn(code, result["finalization"]["blockingCodes"])
        self.assertEqual([], invariants(result))

    def test_guard_fixtures_have_independent_expected_rejections(self):
        codes = ["DESIGN_RESULT_NOT_DESIGNED", "INVALID_DESIGN_RESULT", "PROVENANCE_INCONSISTENT",
                 "DESIGN_RESULT_OWNER_MISMATCH", "INVALID_DESIGN_RESULT", "INVALID_DESIGN_RESULT",
                 "INVALID_DESIGN_RESULT", "PROVENANCE_INCONSISTENT"]
        for case, code in zip(design_guards.all_cases(), codes):
            with self.subTest(case=case.id):
                result, _ = run_case(case)
                self.assertIn(code, result["finalization"]["blockingCodes"])
                self.assertEqual([], invariants(result))

    def test_every_station_value_must_be_finite_nonnegative_and_numeric(self):
        for field in ("station", "topRequiredArea", "bottomRequiredArea", "shearRequiredAreaPerLength"):
            for value in (-1, math.nan, math.inf, -math.inf, None, True, "0.001"):
                with self.subTest(field=field, value=value):
                    s = copy.deepcopy(self.base)
                    s["designResults"][0]["stations"][1][field] = value
                    self.assert_unknown(s)

    def test_missing_station_field_is_rejected(self):
        del self.base["designResults"][0]["stations"][0]["topRequiredArea"]
        self.assert_unknown(self.base)

    def test_unknown_status_is_rejected(self):
        self.base["designResults"][0]["status"] = "Failed"
        self.assert_unknown(self.base, "DESIGN_RESULT_NOT_DESIGNED")

    def test_capture_cannot_contain_simulated_result(self):
        self.base["provenance"].update(type="captured_etabs_snapshot", etabs_verified=True, etabs_version="22.7-test")
        result, _ = evaluate(self.base)
        self.assertEqual("Rejected", result["snapshotStatus"])
        self.assertIn("PROVENANCE_INCONSISTENT", result["finalization"]["blockingCodes"])

    def test_current_result_retains_reference_quantity(self):
        result, _ = evaluate(self.base)
        self.assertAlmostEqual(120.9057, result["elements"][0]["steelKg"], places=8)

    def test_stale_policy_is_preserved_without_enabling_finalization(self):
        self.base["designResults"][0]["status"] = "Stale"
        result, _ = evaluate(self.base)
        self.assertAlmostEqual(120.9057, result["elements"][0]["steelKg"], places=8)
        self.assertFalse(result["finalization"]["allowed"])

    def test_overflowed_mass_is_unknown(self):
        for station in self.base["designResults"][0]["stations"]:
            station["topRequiredArea"] = 1e308
        self.assert_unknown(self.base, "INVALID_REINFORCEMENT")


class ExpectedComparisonTests(unittest.TestCase):
    def setUp(self):
        self.base, _ = run_case(next(c for c in reinforcement.all_cases() if c.id == "RREQ-003"))

    def test_last_bit_quantity_difference_is_accepted(self):
        changed = copy.deepcopy(self.base)
        changed["modelTotals"]["steelKg"] += 1e-10
        self.assertTrue(expected_equal(self.base, changed))

    def test_material_quantity_difference_is_rejected(self):
        changed = copy.deepcopy(self.base)
        changed["modelTotals"]["steelKg"] += .01
        self.assertFalse(expected_equal(self.base, changed))

    def test_counts_labels_nulls_and_tolerances_remain_exact(self):
        changes = [lambda s: s["elements"][0].update(occurrence=1),
                   lambda s: s["elements"][0].update(steelKg=None),
                   lambda s: s["finalization"].update(allowed=False),
                   lambda s: s["tolerance"].update(steelKg=100),
                   lambda s: s["elements"][0].update(status="Rejected"),
                   lambda s: s["elements"][0].update(occurrence=False)]
        for change in changes:
            changed = copy.deepcopy(self.base); change(changed)
            self.assertFalse(expected_equal(self.base, changed))

    def test_nonfinite_expected_quantities_are_rejected(self):
        for value in (math.nan, math.inf):
            changed = copy.deepcopy(self.base)
            changed["modelTotals"]["steelKg"] = value
            self.assertFalse(expected_equal(changed, changed))

    def test_generation_refuses_an_invalid_expected_result(self):
        import generate
        original = generate.run_case
        def corrupt(case):
            result, ev = original(case)
            result["modelTotals"]["netConcreteM3"] = -100
            return result, ev
        with patch.object(generate, "run_case", corrupt):
            with self.assertRaisesRegex(AssertionError, "violates invariants"):
                generate.build(write=False)


if __name__ == "__main__":
    unittest.main()
