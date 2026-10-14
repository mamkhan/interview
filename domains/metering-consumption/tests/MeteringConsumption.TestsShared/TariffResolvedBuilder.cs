using Growth.Contracts;
using Growth.Contracts.Events;

namespace MeteringConsumption.TestsShared;

// Defaults are the V1 reader's TariffResolved for MPRN 10012345678 on its run of 2026-10-07 at 05:00 Dublin time
// (04:00 UTC): a 48-rate tariff on the 24-hour curve with load profile LP1.
public sealed class TariffResolvedBuilder
{
    public static readonly DateTimeOffset DefaultAsOf = new(2026, 10, 7, 4, 0, 0, TimeSpan.Zero);

    private string _mprn = "10012345678";
    private bool? _isTou = true;
    private string? _calcType = TariffCalcTypes.TwentyFourHour;
    private string? _effectiveLoadProfileCode = "LP1";
    private DateTimeOffset? _asOf = DefaultAsOf;

    public TariffResolvedBuilder ForMprn(string mprn)
    {
        _mprn = mprn;
        return this;
    }

    public TariffResolvedBuilder WithCalcType(string? calcType)
    {
        _calcType = calcType;
        return this;
    }

    public TariffResolvedBuilder WithNoTariff()
    {
        _isTou = null;
        _calcType = null;
        _effectiveLoadProfileCode = null;
        return this;
    }

    public TariffResolvedBuilder ReadAt(DateTimeOffset asOf)
    {
        _asOf = asOf;
        return this;
    }

    public TariffResolvedBuilder WithoutAsOf()
    {
        _asOf = null;
        return this;
    }

    public TariffResolved Build() => new()
    {
        EventId = Guid.Parse("3f1d2c64-8a51-5b0e-9c47-2e6a9b1d7f05"),
        PublishingContext = "platform.v1-reader",
        OccurredAtUtc = DefaultAsOf,
        CorrelationId = "v1-reader:tariff-values:2026-10-07",
        AsOf = _asOf,
        MPRN = _mprn,
        Market = "roi-esb",
        IsTou = _isTou,
        TariffCalcType = _calcType,
        EffectiveLoadProfileCode = _effectiveLoadProfileCode,
        Source = "v1-reader/tariff-values",
    };
}
