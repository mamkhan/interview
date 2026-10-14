namespace MeteringConsumption.Consumption.Domain;

public enum UsageFactorSource
{
    Puf,
    Aau,
    Euf,
    None,
}

// A DUoS group's average annual use, in kWh, per timeslot for the anchor's year.
public sealed record AnnualAverageUsage(decimal? Aau24H, decimal? Aau00D, decimal? Aau00N);

// The annual kWh each tier of the layout is scaled by, from one source for every tier. An own-use factor keeps the
// kWh measured per tier and the load-profile sum it was divided by, so the forecast can be explained.
public sealed record UsageFactors(
    UsageFactorSource Source,
    IReadOnlyList<decimal> PerTier,
    IReadOnlyList<decimal>? Numerators = null,
    IReadOnlyList<decimal>? Denominators = null);

// The first valid usage factor in the meter's order, by V1's validity rules, each family whole or not at all.
public static class UsageFactorSelection
{
    private const int TimeOfUseTiers = ReadDay.SlotsPerDay;

    public static UsageFactors? Select(
        ForecastProfile profile,
        ConsumptionWindow window,
        LoadProfileCurve? ownCurve,
        AnnualAverageUsage? annualAverage,
        ForecastSettings settings)
    {
        var pufWindow = window.FindPufWindow(settings);
        Func<UsageFactors?>[] candidates = profile.Layout switch
        {
            CurveLayout.SingleRate =>
            [
                () => SingleRateEuf(profile, settings),
                () => SingleRatePuf(pufWindow, ownCurve),
                () => SingleRateAau(annualAverage),
            ],
            CurveLayout.DayNight =>
            [
                () => DayNightEuf(profile, settings),
                () => DayNightPuf(pufWindow, ownCurve),
                () => DayNightAau(annualAverage),
            ],
            _ => profile.OwnUseFirst
                ? [() => TimeOfUsePuf(pufWindow, ownCurve), () => TimeOfUseAau(annualAverage), () => TimeOfUseEuf(profile, settings)]
                : [() => TimeOfUseEuf(profile, settings), () => TimeOfUsePuf(pufWindow, ownCurve), () => TimeOfUseAau(annualAverage)],
        };

        return candidates.Select(candidate => candidate()).FirstOrDefault(factors => factors is not null);
    }

    private static UsageFactors? SingleRateEuf(ForecastProfile profile, ForecastSettings settings) =>
        IsEufInRange(profile.Euf24H, settings) ? new(UsageFactorSource.Euf, [profile.Euf24H!.Value]) : null;

    private static UsageFactors? SingleRatePuf(PufWindow? window, LoadProfileCurve? ownCurve)
    {
        var denominator = WindowSum(window, ownCurve, CurveBand.Flat);
        if (window is null || denominator is not > 0)
        {
            return null;
        }

        var puf = window.TotalKwh / denominator.Value;
        return puf > 0 ? new(UsageFactorSource.Puf, [puf], [window.TotalKwh], [denominator.Value]) : null;
    }

    private static UsageFactors? SingleRateAau(AnnualAverageUsage? annualAverage) =>
        annualAverage?.Aau24H is > 0 and var aau ? new(UsageFactorSource.Aau, [aau]) : null;

    // A 24H estimate takes the night tier and leaves the day tier empty, so only a 00N and 00D pair can win.
    private static UsageFactors? DayNightEuf(ForecastProfile profile, ForecastSettings settings)
    {
        var night = profile.Euf24H > 0 ? profile.Euf24H : profile.Euf00N;
        var day = profile.Euf24H > 0 ? 0m : profile.Euf00D;
        return night > 0 && day > 0 && IsEufInRange(night + day, settings)
            ? new(UsageFactorSource.Euf, [night!.Value, day!.Value])
            : null;
    }

    private static UsageFactors? DayNightPuf(PufWindow? window, LoadProfileCurve? ownCurve)
    {
        var night = WindowSum(window, ownCurve, CurveBand.Night);
        var day = WindowSum(window, ownCurve, CurveBand.Day);
        if (window is null || night is not > 0 || day is not > 0)
        {
            return null;
        }

        var nightPuf = window.NightKwh / night.Value;
        var dayPuf = window.DayKwh / day.Value;
        return nightPuf > 0 && dayPuf > 0
            ? new(UsageFactorSource.Puf, [nightPuf, dayPuf], [window.NightKwh, window.DayKwh], [night.Value, day.Value])
            : null;
    }

    private static UsageFactors? DayNightAau(AnnualAverageUsage? annualAverage) =>
        annualAverage is { Aau00N: > 0, Aau00D: > 0 }
            ? new(UsageFactorSource.Aau, [annualAverage.Aau00N.Value, annualAverage.Aau00D.Value])
            : null;

    // As V1 does, the meter's own use is divided by the curve of its own ESB load profile, not the tariff's: that
    // profile's whole-day curve on every tier when it has one over the window, otherwise its own night and day
    // bands. A zero tier is still a valid own-use factor here.
    private static UsageFactors? TimeOfUsePuf(PufWindow? window, LoadProfileCurve? ownCurve)
    {
        if (window is null)
        {
            return null;
        }

        var denominators = new decimal[TimeOfUseTiers];
        var flat = WindowSum(window, ownCurve, CurveBand.Flat);
        var night = WindowSum(window, ownCurve, CurveBand.Night);
        var day = WindowSum(window, ownCurve, CurveBand.Day);
        if (flat is > 0)
        {
            Array.Fill(denominators, flat.Value);
        }
        else if (night is > 0 && day is > 0)
        {
            for (var tier = 0; tier < TimeOfUseTiers; tier++)
            {
                denominators[tier] = ReadDay.IsDaySlot(tier) ? day.Value : night.Value;
            }
        }
        else
        {
            return null;
        }

        var pufs = window.SlotTotals.Select((kwh, tier) => kwh / denominators[tier]).ToArray();
        return new(UsageFactorSource.Puf, pufs, window.SlotTotals, denominators);
    }

    private static UsageFactors? TimeOfUseAau(AnnualAverageUsage? annualAverage) =>
        annualAverage?.Aau24H is > 0 and var aau ? new(UsageFactorSource.Aau, Fanned(aau)) : null;

    private static UsageFactors? TimeOfUseEuf(ForecastProfile profile, ForecastSettings settings) =>
        IsEufInRange(profile.Euf24H, settings) ? new(UsageFactorSource.Euf, Fanned(profile.Euf24H!.Value)) : null;

    private static decimal[] Fanned(decimal annualKwh) => Enumerable.Repeat(annualKwh / TimeOfUseTiers, TimeOfUseTiers).ToArray();

    private static decimal? WindowSum(PufWindow? window, LoadProfileCurve? curve, CurveBand band) =>
        window is null ? null : curve?.Sum(window.FirstCountedDay, window.Days, band);

    private static bool IsEufInRange(decimal? euf, ForecastSettings settings) =>
        euf > settings.EufMinAcceptableKwh && euf < settings.EufMaxAcceptableKwh;
}
