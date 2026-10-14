namespace MeteringConsumption.Consumption.Domain;

public sealed record ForecastedDay(DateOnly Date, decimal Kwh);

// A forecast and everything it was made from. The own-use window is kept only when the own-use factor won.
public sealed record ForecastResult(
    DateOnly AnchorDate,
    IReadOnlyList<ForecastedDay> Days,
    ForecastProfile Profile,
    UsageFactors Factors,
    PufWindow? PufWindow)
{
    public decimal TotalKwh => Days.Sum(day => day.Kwh);
}

public abstract record ForecastOutcome
{
    private ForecastOutcome()
    {
    }

    public sealed record Computed(ForecastResult Result) : ForecastOutcome;

    // No usable usage factor: as V1, a forecast of zeros, marked so it cannot pass for a real one.
    public sealed record ZeroForecast(ForecastResult Result) : ForecastOutcome;

    public sealed record NotForecast(NotForecastReason Reason) : ForecastOutcome;
}

// Each tier's hourly kWh is its curve's value for the hour times its usage factor, at full precision; a day is the
// sum of its 24 hours over every tier. Every day is 24 hours, as in V1.
public static class ForecastCalculation
{
    private const int HoursPerDay = 24;

    public static ForecastOutcome Calculate(
        ConsumptionWindow window,
        MeterPointFacts? configuration,
        TariffFacts? tariff,
        Func<string, LoadProfileCurve?> curves,
        AnnualAverageUsage? annualAverage,
        ForecastSettings settings)
    {
        var resolution = ForecastProfile.Resolve(configuration, tariff);
        if (resolution is ProfileResolution.Unresolved unresolved)
        {
            return new ForecastOutcome.NotForecast(unresolved.Reason);
        }

        var profile = ((ProfileResolution.Resolved)resolution).Profile;
        var anchor = window.Anchor;
        if (TierCurves(profile, curves(profile.CurveLoadProfile), anchor, settings.HorizonDays) is not { } tierCurves)
        {
            return new ForecastOutcome.NotForecast(NotForecastReason.NoProfileShape);
        }

        var factors = UsageFactorSelection.Select(profile, window, curves(profile.OwnLoadProfile), annualAverage, settings);
        if (factors is null)
        {
            var zeros = new UsageFactors(UsageFactorSource.None, new decimal[tierCurves.Length]);
            return new ForecastOutcome.ZeroForecast(new ForecastResult(anchor, Days(tierCurves, zeros, anchor, settings), profile, zeros, null));
        }

        var pufWindow = factors.Source == UsageFactorSource.Puf ? window.FindPufWindow(settings) : null;
        return new ForecastOutcome.Computed(new ForecastResult(anchor, Days(tierCurves, factors, anchor, settings), profile, factors, pufWindow));
    }

    private static IReadOnlyList<decimal>[]? TierCurves(
        ForecastProfile profile, LoadProfileCurve? curve, DateOnly anchor, int horizonDays)
    {
        if (curve is null)
        {
            return null;
        }

        var flat = curve.Hours(anchor, horizonDays, CurveBand.Flat);
        var night = curve.Hours(anchor, horizonDays, CurveBand.Night);
        var day = curve.Hours(anchor, horizonDays, CurveBand.Day);
        return profile.Layout switch
        {
            CurveLayout.SingleRate => flat is null ? null : [flat],
            CurveLayout.DayNight => night is null || day is null ? null : [night, day],
            CurveLayout.FlatTimeOfUse => flat is null ? null : Enumerable.Repeat(flat, ReadDay.SlotsPerDay).ToArray(),
            _ => night is null || day is null
                ? null
                : Enumerable.Range(0, ReadDay.SlotsPerDay).Select(tier => ReadDay.IsDaySlot(tier) ? day : night).ToArray(),
        };
    }

    private static ForecastedDay[] Days(
        IReadOnlyList<decimal>[] tierCurves, UsageFactors factors, DateOnly anchor, ForecastSettings settings)
    {
        var days = new ForecastedDay[settings.HorizonDays];
        for (var day = 0; day < settings.HorizonDays; day++)
        {
            var kwh = 0m;
            for (var tier = 0; tier < tierCurves.Length; tier++)
            {
                var tierKwh = 0m;
                for (var hour = 0; hour < HoursPerDay; hour++)
                {
                    tierKwh += tierCurves[tier][(day * HoursPerDay) + hour] * factors.PerTier[tier];
                }

                kwh += tierKwh;
            }

            days[day] = new ForecastedDay(anchor.AddDays(day), kwh);
        }

        return days;
    }
}
