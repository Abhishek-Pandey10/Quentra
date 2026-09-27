using System.Collections.Immutable;

namespace Quentra.Core;

public static class SteelCalculator
{
    public static SteelQuantity Calculate(SteelInput input, ElementTakeoff element, TakeoffPolicy policy)
    {
        var evidence = input.Method switch
        {
            SteelMethod.DemandEquivalent => "DesignDemandEquivalent",
            SteelMethod.VolumeFraction or SteelMethod.KgPerCubicMetre => "Estimated",
            _ => "ModelAssignedEquivalent"
        };
        var covers = input.Component == "AllIn" ? input.CoversComponents : ImmutableArray.Create(input.Component);
        SteelQuantity Unknown(string message) => new(input.ObjectId, input.Component, evidence, null, covers,
            input.SourceReference, "Not calculated", input.Assumption, input.ApprovedBy, input.ApprovedAt,
            [new("STEEL_UNAVAILABLE", message)]);
        // Out-of-scope elements (excluded or non-concrete) need no steel, so they raise no warning to acknowledge.
        if (element.Status == QuantityStatus.OutOfScope)
            return Unknown("") with { Formula = "Not calculated: element is out of scope", Warnings = [] };
        if (element.Status != QuantityStatus.Quantified) return Unknown("Supported geometry is required for this steel component.");
        if (string.IsNullOrWhiteSpace(input.ApprovedBy) || input.ApprovedAt == default || string.IsNullOrWhiteSpace(input.Assumption))
            return Unknown("A named approval, date and measurement assumption are required.");
        try
        {
            double volume; string formula;
            switch (input.Method)
            {
                case SteelMethod.VolumeFraction:
                case SteelMethod.KgPerCubicMetre:
                    var ratio = Nonnegative(input.Ratio);
                    if (input.Method == SteelMethod.VolumeFraction && ratio > 1)
                        throw new ArgumentException("A volumetric fraction must be between 0 and 1.");
                    if (input.Method == SteelMethod.KgPerCubicMetre && ratio > policy.SteelDensityKgM3)
                        throw new ArgumentException(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                            $"A rate of {ratio:G6} kg/m³ exceeds the steel density of {policy.SteelDensityKgM3:G6} kg/m³. Check the rate and its units."));
                    var concrete = input.ConcreteBasis == ConcreteBasis.GrossModeled ? element.GrossM3 : element.OpeningAdjustedM3;
                    if (concrete is null) throw new ArgumentException("The estimate's concrete basis is unavailable.");
                    var mass = ratio * concrete.Value * (input.Method == SteelMethod.VolumeFraction ? policy.SteelDensityKgM3 : 1);
                    formula = input.Method == SteelMethod.VolumeFraction ? "volume fraction × declared concrete volume × steel density" : "kg/m³ × declared concrete volume";
                    return Make(mass, formula);
                case SteelMethod.DemandEquivalent:
                    if (input.DesignEvidence != DesignEvidence.VerifiedCurrent)
                        throw new ArgumentException($"Design demand is only counted when designEvidence is VerifiedCurrent; this entry is {input.DesignEvidence}: " + input.DesignEvidence switch
                        {
                            DesignEvidence.Missing => "there is no design evidence for it.",
                            DesignEvidence.Stale => "the model changed after the design run.",
                            DesignEvidence.Failed => "the design failed, so its demand is not a valid reinforcement area.",
                            _ => "its values are check ratios, not reinforcement areas."
                        });
                    if (string.IsNullOrWhiteSpace(input.DesignCode))
                        throw new ArgumentException("Design demand needs the design code and edition in designCode.");
                    if (element.AxisLengthM is null) throw new ArgumentException("Longitudinal demand requires a supported frame.");
                    var start = Nonnegative(input.DomainStartM); var end = Positive(input.DomainEndM);
                    var maxGap = Positive(input.MaximumStationGapM);
                    if (end <= start || end > element.AxisLengthM.Value + 1e-8)
                        throw new ArgumentException("Demand domain must be inside the modeled axis length.");
                    if (input.Stations.IsDefault || input.Stations.Length < 2) throw new ArgumentException("At least two demand stations are required.");
                    var stations = input.Stations.OrderBy(x => x.PositionM).ToArray();
                    if (Math.Abs(stations[0].PositionM - start) > 1e-9 || Math.Abs(stations[^1].PositionM - end) > 1e-9)
                        throw new ArgumentException("Stations must cover the declared demand domain without extrapolation.");
                    volume = 0;
                    for (var i = 0; i < stations.Length; i++)
                    {
                        Nonnegative(stations[i].PositionM); Nonnegative(stations[i].AreaM2);
                        if (i == 0) continue;
                        var interval = stations[i].PositionM - stations[i - 1].PositionM;
                        if (interval <= 0 || interval > maxGap + 1e-9) throw new ArgumentException("Duplicate stations or an unapproved station gap require review.");
                        volume += Math.Max(stations[i - 1].AreaM2, stations[i].AreaM2) * interval;
                    }
                    formula = "sum(max(endpoint As) × interval length) × density; declared domain only";
                    var demandResult = Make(volume * policy.SteelDensityKgM3, formula);
                    var fullDomain = start <= 1e-9 && Math.Abs(end - element.AxisLengthM.Value) <= 1e-9;
                    var partial = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Demand stations cover {start:0.###}–{end:0.###} m of the {element.AxisLengthM.Value:0.###} m axis. The mass counts that length only, and the component stays missing from coverage until the full length is covered.");
                    return demandResult with
                    {
                        CoversComponents = fullDomain ? covers : [],
                        Warnings = [fullDomain
                            ? new("DEMAND_EQUIVALENT", "Sample-based demand equivalent; unsampled peaks, end zones outside the declared domain, laps, anchorage and transverse bars are not inferred.")
                            : new("PARTIAL_DEMAND_DOMAIN", partial)]
                    };
                case SteelMethod.AssignedBars:
                    if (input.Bars.IsDefaultOrEmpty) throw new ArgumentException("Bar count, area and straight length are required.");
                    volume = 0;
                    foreach (var bar in input.Bars)
                    {
                        if (bar.Count <= 0) throw new ArgumentException("Bar count must be positive.");
                        volume += bar.Count * Positive(bar.AreaM2) * Positive(bar.LengthM);
                    }
                    return Make(volume * policy.SteelDensityKgM3, "sum(count × bar area × declared straight length) × density");
                case SteelMethod.DistributedIntensity:
                    if (element.SurfaceAreaM2 is null) throw new ArgumentException("Distributed intensity requires supported area geometry.");
                    return Make(Nonnegative(input.IntensityM2PerM) * element.SurfaceAreaM2.Value * policy.SteelDensityKgM3,
                        "declared face/direction intensity sum × opening-adjusted surface area × density");
                default: throw new ArgumentException("Unsupported steel method.");
            }
        }
        catch (ArgumentException ex) { return Unknown(ex.Message); }

        SteelQuantity Make(double mass, string formula)
        {
            Nonnegative(mass);
            return new(input.ObjectId, input.Component, evidence, mass, covers, input.SourceReference,
                formula, input.Assumption, input.ApprovedBy, input.ApprovedAt, []);
        }
    }

    private static double Nonnegative(double? value) => value is { } x && double.IsFinite(x) && x >= 0 ? x : throw new ArgumentException("A finite nonnegative value is required.");
    private static double Positive(double? value) => value is { } x && double.IsFinite(x) && x > 0 ? x : throw new ArgumentException("A finite positive value is required.");
}
