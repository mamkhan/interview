using Growth.Contracts;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.TestsShared;

// Defaults are a 48-rate tariff on the 24-hour curve with load profile LP1, as the V1 reader reads it on its run
// of 2026-10-07 at 05:00 Dublin time (04:00 UTC).
public sealed class TariffStatementBuilder
{
    public const string DefaultSource = "v1-reader/tariff-values";

    public static readonly DateTimeOffset DefaultAsOf = new(2026, 10, 7, 4, 0, 0, TimeSpan.Zero);

    private bool? _isTou = true;
    private string? _calcType = TariffCalcTypes.TwentyFourHour;
    private string? _effectiveLoadProfileCode = "LP1";
    private DateTimeOffset _asOf = DefaultAsOf;
    private string _source = DefaultSource;

    public TariffStatementBuilder WithIsTou(bool? isTou)
    {
        _isTou = isTou;
        return this;
    }

    public TariffStatementBuilder WithCalcType(string? calcType)
    {
        _calcType = calcType;
        return this;
    }

    public TariffStatementBuilder WithEffectiveLoadProfileCode(string? code)
    {
        _effectiveLoadProfileCode = code;
        return this;
    }

    // V1 holds no tariff for the meter, so every value is empty.
    public TariffStatementBuilder WithNoTariff()
    {
        _isTou = null;
        _calcType = null;
        _effectiveLoadProfileCode = null;
        return this;
    }

    public TariffStatementBuilder ReadAt(DateTimeOffset asOf)
    {
        _asOf = asOf;
        return this;
    }

    public TariffStatementBuilder FromSource(string source)
    {
        _source = source;
        return this;
    }

    public TariffStatement Build() => TariffStatement.Of(_isTou, _calcType, _effectiveLoadProfileCode, _asOf, _source);
}
