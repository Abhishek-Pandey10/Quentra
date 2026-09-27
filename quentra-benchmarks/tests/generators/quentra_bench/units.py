"""Unit conversion factors. Exact by definition (international yard and pound, 1959)."""

LENGTH_TO_M = {"m": 1.0, "mm": 0.001, "cm": 0.01, "ft": 0.3048, "in": 0.0254}

# 1 lb = 0.45359237 kg exactly; 1 ft = 0.3048 m exactly
LB_PER_FT3_TO_KG_PER_M3 = 0.45359237 / (0.3048 ** 3)
DENSITY_TO_KG_PER_M3 = {"kg/m3": 1.0, "lb/ft3": LB_PER_FT3_TO_KG_PER_M3}
MASS_TO_KG = {"kg": 1.0, "lb": 0.45359237}


def kg_m3_to(unit, value_kg_m3):
    return value_kg_m3 / DENSITY_TO_KG_PER_M3[unit]
