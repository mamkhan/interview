using Growth.Contracts;

namespace MeteringConsumption.Consumption.Domain;

// What ESB states is in effect at the supply point: the meter point's codes and the estimated usage factors (EUF) of
// the 24H, 00D and 00N timeslots, in kWh a year.
public sealed record MeterPointFacts(
    string? MeterConfigurationCode,
    string? LoadProfileCode,
    string? DuosGroup,
    string? MeterPointStatusCode,
    decimal? Euf24H,
    decimal? Euf00D,
    decimal? Euf00N);

// What the customer's tariff and V1's smart meter flag state for the supply point. The effective load profile is
// named as V1 names it (LP2).
public sealed record TariffFacts(bool? IsNsmp, bool? IsTou, string? CalcType, string? EffectiveLoadProfileCode);

// The tiers a forecast multiplies: one whole-day tier; a night and a day tier; or 48 half-hour tiers, each on the
// whole-day curve or on its band's.
public enum CurveLayout
{
    SingleRate,
    DayNight,
    FlatTimeOfUse,
    BandedTimeOfUse,
}

public enum NotForecastReason
{
    NoConfigurationInEffect,
    NoTariffValues,
    UnsupportedMcc,
    NoCalcType,
    NoProfileShape,
}

// The forecast's view of a supply point, merged from the two sources. OwnLoadProfile is the meter's ESB profile, which
// the own-use factor is measured against; CurveLoadProfile is the one the forecast is shaped by, the tariff's for an
// NSMP meter and the meter's own otherwise.
public sealed record ForecastProfile
{
    public required int Mcc { get; init; }

    public required string OwnLoadProfile { get; init; }

    public required string CurveLoadProfile { get; init; }

    public required CurveLayout Layout { get; init; }

    public required bool IsNsmp { get; init; }

    public required bool IsTou { get; init; }

    public string? CalcType { get; init; }

    public string? DuosGroup { get; init; }

    public decimal? Euf24H { get; init; }

    public decimal? Euf00D { get; init; }

    public decimal? Euf00N { get; init; }

    // MCC12 and MCC16 try the meter's own use first; MCC1 and MCC2 try ESB's estimate first.
    public bool OwnUseFirst => Mcc is 12 or 16;

    public static ProfileResolution Resolve(MeterPointFacts? configuration, TariffFacts? tariff)
    {
        if (configuration is null)
        {
            return new ProfileResolution.Unresolved(NotForecastReason.NoConfigurationInEffect);
        }

        if (tariff is not { IsNsmp: { } isNsmp, IsTou: { } isTou })
        {
            return new ProfileResolution.Unresolved(NotForecastReason.NoTariffValues);
        }

        if (MccNumber(configuration.MeterConfigurationCode) is not { } mcc)
        {
            return new ProfileResolution.Unresolved(NotForecastReason.UnsupportedMcc);
        }

        if (LoadProfileName(configuration.LoadProfileCode) is not { } ownLoadProfile)
        {
            return new ProfileResolution.Unresolved(NotForecastReason.NoProfileShape);
        }

        CurveLayout layout;
        if (isTou)
        {
            if (mcc is not (1 or 2 or 12 or 16))
            {
                return new ProfileResolution.Unresolved(NotForecastReason.UnsupportedMcc);
            }

            if (isNsmp && tariff.CalcType is null)
            {
                return new ProfileResolution.Unresolved(NotForecastReason.NoCalcType);
            }

            var banded = isNsmp ? tariff.CalcType == TariffCalcTypes.DayNight : mcc == 2;
            layout = banded ? CurveLayout.BandedTimeOfUse : CurveLayout.FlatTimeOfUse;
        }
        else
        {
            switch (mcc)
            {
                case 1:
                    layout = CurveLayout.SingleRate;
                    break;
                case 2:
                    layout = CurveLayout.DayNight;
                    break;
                default:
                    return new ProfileResolution.Unresolved(NotForecastReason.UnsupportedMcc);
            }
        }

        return new ProfileResolution.Resolved(new ForecastProfile
        {
            Mcc = mcc,
            OwnLoadProfile = ownLoadProfile,
            CurveLoadProfile = isTou && isNsmp ? tariff.EffectiveLoadProfileCode ?? ownLoadProfile : ownLoadProfile,
            Layout = layout,
            IsNsmp = isNsmp,
            IsTou = isTou,
            CalcType = tariff.CalcType,
            DuosGroup = configuration.DuosGroup,
            Euf24H = configuration.Euf24H,
            Euf00D = configuration.Euf00D,
            Euf00N = configuration.Euf00N,
        });
    }

    // ESB codes are numbers ("02", "25"); reference data names a profile LP and that number (LP2, LP25). V1 strips
    // every zero instead, which turns 10 into LP1.
    public static string? LoadProfileName(string? esbCode) =>
        int.TryParse(esbCode, out var number) && number > 0 ? $"LP{number}" : null;

    public static int? MccNumber(string? meterConfigurationCode) =>
        meterConfigurationCode is { Length: > 3 } code
        && code.StartsWith("MCC", StringComparison.Ordinal)
        && int.TryParse(code[3..], out var number)
            ? number
            : null;
}

public abstract record ProfileResolution
{
    private ProfileResolution()
    {
    }

    public sealed record Resolved(ForecastProfile Profile) : ProfileResolution;

    public sealed record Unresolved(NotForecastReason Reason) : ProfileResolution;
}
