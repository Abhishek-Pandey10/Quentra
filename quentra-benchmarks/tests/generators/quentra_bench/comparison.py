"""Tolerant quantity comparison; all structure, labels and counts remain exact."""
import math


def expected_equal(stored, generated):
    # Never let a modified tolerance block relax the comparison.
    if stored.get("tolerance") != generated.get("tolerance"):
        return False
    tolerance = generated["tolerance"]

    def equal(a, b, path=()):
        if isinstance(a, dict) and isinstance(b, dict):
            return a.keys() == b.keys() and all(equal(a[k], b[k], path + (k,)) for k in a)
        if isinstance(a, list) and isinstance(b, list):
            return len(a) == len(b) and all(equal(x, y, path) for x, y in zip(a, b))
        if isinstance(a, bool) or isinstance(b, bool):
            return type(a) is type(b) and a == b
        numeric = isinstance(a, (int, float)) and isinstance(b, (int, float))
        if numeric:
            absolute = None
            for field in reversed(path):
                if field == "tolerance":
                    break
                if field.endswith("M3"):
                    absolute = tolerance["volumeM3"]; break
                if field.endswith("M2"):
                    absolute = tolerance["areaM2"]; break
                if field == "lengthM":
                    absolute = tolerance["lengthM"]; break
                if field in {"steelKg", "steelExactKg", "steelApproximateKg", "steelComponentsKg", "steelKgBySource", "steelByMaterial"}:
                    absolute = tolerance["steelKg"]; break
            if absolute is not None:
                return math.isfinite(a) and math.isfinite(b) and math.isclose(a, b, rel_tol=tolerance["relative"], abs_tol=absolute)
        return type(a) is type(b) and a == b

    return equal(stored, generated)
