using System.Globalization;
using Quentra.Core;

namespace Quentra.Application;

// Review bands for catching unit slips and typing errors. Values outside a band are warned and
// must be acknowledged at acceptance; they are never rejected or altered. The bands are
// provisional until the engineer confirms them (engineer meeting brief, measurement policy).
public static class Plausibility
{
    public const double MinSectionM = 0.1, MaxSectionM = 3;
    public const double MinThicknessM = 0.05, MaxThicknessM = 2;
    public const double MaxFrameLengthM = 50;
    public const double MaxAreaExtentM = 300;
    public const double MaxSteelKgPerM3 = 600;
    public const double MinSteelDensityKgM3 = 7000, MaxSteelDensityKgM3 = 8500;

    public static IEnumerable<CalculationWarning> Frame(FrameSnapshot frame, double lengthM)
    {
        var dimensions = frame.Section?.Shape == SectionShape.Circle
            ? new[] { ("Diameter", frame.Section.Diameter) }
            : [("Width", frame.Section?.Width), ("Depth", frame.Section?.Depth)];
        foreach (var (name, value) in dimensions)
            if (value is not null && (value.Metres < MinSectionM || value.Metres > MaxSectionM))
                yield return Dimension($"{name} {F(value.Metres)} m is outside the {F(MinSectionM)}–{F(MaxSectionM)} m review band");
        if (lengthM > MaxFrameLengthM)
            yield return Dimension($"Axis length {F(lengthM)} m exceeds the {F(MaxFrameLengthM)} m review band");
    }

    public static IEnumerable<CalculationWarning> Area(AreaSnapshot area, double thicknessM, double metresPerUnit)
    {
        if (thicknessM < MinThicknessM || thicknessM > MaxThicknessM)
            yield return Dimension($"Thickness {F(thicknessM)} m is outside the {F(MinThicknessM)}–{F(MaxThicknessM)} m review band");
        var extent = new[] { area.Boundary.Max(p => p.X) - area.Boundary.Min(p => p.X), area.Boundary.Max(p => p.Y) - area.Boundary.Min(p => p.Y),
            area.Boundary.Max(p => p.Z) - area.Boundary.Min(p => p.Z) }.Max() * metresPerUnit;
        if (extent > MaxAreaExtentM)
            yield return Dimension($"Boundary extent {F(extent)} m exceeds the {F(MaxAreaExtentM)} m review band");
    }

    public static CalculationWarning? SteelIntensity(double knownKg, double? concreteM3) =>
        concreteM3 is > 0 && knownKg / concreteM3.Value > MaxSteelKgPerM3
            ? new("IMPLAUSIBLE_STEEL_INTENSITY", $"Known steel is {F(knownKg / concreteM3.Value)} kg per m³ of concrete, above the {F(MaxSteelKgPerM3)} kg/m³ review band. Check rates, bar areas and units.")
            : null;

    public static CalculationWarning? Policy(TakeoffPolicy policy) =>
        policy.SteelDensityKgM3 < MinSteelDensityKgM3 || policy.SteelDensityKgM3 > MaxSteelDensityKgM3
            ? new("IMPLAUSIBLE_POLICY", $"Steel density {F(policy.SteelDensityKgM3)} kg/m³ is outside the {F(MinSteelDensityKgM3)}–{F(MaxSteelDensityKgM3)} kg/m³ review band.")
            : null;

    private static CalculationWarning Dimension(string message) => new("IMPLAUSIBLE_DIMENSION", message + ". Check the value and its units.");

    // Invariant formatting: warning text is part of the hashed result.
    public static string F(double value) => value.ToString("G6", CultureInfo.InvariantCulture);
}
