using Growth.Contracts;

namespace MeteringConsumption.Consumption.Domain;

// The tariff values the customer's tariff states for a supply point, as read at AsOf. A null value means the
// tariff states none. Source names who resolved them.
public sealed record TariffStatement(
    bool? IsTou,
    string? CalcType,
    string? EffectiveLoadProfileCode,
    DateTimeOffset AsOf,
    string Source)
{
    private static readonly string[] CalcTypes = [TariffCalcTypes.TwentyFourHour, TariffCalcTypes.DayNight];

    public static TariffStatement Of(
        bool? isTou, string? calcType, string? effectiveLoadProfileCode, DateTimeOffset asOf, string source)
    {
        if (calcType is not null && !CalcTypes.Contains(calcType))
        {
            throw new ProfileStatementRejectedException(
                $"The tariff calc type '{calcType}' is not one of {string.Join(", ", CalcTypes)}.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ProfileStatementRejectedException("The tariff values have no source.");
        }

        return new(isTou, calcType, effectiveLoadProfileCode, asOf, source);
    }

    public bool StatesTheSameValuesAs(TariffStatement other) =>
        IsTou == other.IsTou && CalcType == other.CalcType && EffectiveLoadProfileCode == other.EffectiveLoadProfileCode;
}
