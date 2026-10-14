using Platform.Contracts.Events.V1;

namespace MeteringConsumption.TestsShared;

// Defaults to a run started 2026-10-12 05:00 UTC loading 10012345678: MCC01, load profile 01 and DG1 from 2026-08-03,
// the 24H estimate 4127.5 from 2026-08-03 and the 24H actual 3980.25 from 2026-09-01.
public sealed class SupplyPointLoadedFromV1Builder
{
    private Guid _eventId = Guid.Parse("4d2f8a61-3c7b-5e9a-8b1d-6f0e2a4c8e13");
    private DateTimeOffset _occurredAtUtc = new(2026, 10, 12, 5, 0, 0, TimeSpan.Zero);
    private string _mprn = "10012345678";
    private DateOnly _codesFrom = new(2026, 8, 3);
    private string? _meterConfigurationCode = "MCC01";
    private string? _loadProfileCode = "01";
    private string? _duosGroup = "DG1";
    private IReadOnlyList<LoadedUsageFactor> _usageFactors =
    [
        new("24H", new DateOnly(2026, 8, 3), 4127.5m, null),
        new("24H", new DateOnly(2026, 9, 1), null, 3980.25m),
    ];

    public SupplyPointLoadedFromV1Builder ForMprn(string mprn)
    {
        _mprn = mprn;
        return this;
    }

    public SupplyPointLoadedFromV1Builder WithEventId(Guid eventId)
    {
        _eventId = eventId;
        return this;
    }

    public SupplyPointLoadedFromV1Builder StartedAt(DateTimeOffset occurredAtUtc)
    {
        _occurredAtUtc = occurredAtUtc;
        return this;
    }

    public SupplyPointLoadedFromV1Builder WithCodes(DateOnly codesFrom, string? meterConfigurationCode, string? loadProfileCode,
        string? duosGroup)
    {
        _codesFrom = codesFrom;
        _meterConfigurationCode = meterConfigurationCode;
        _loadProfileCode = loadProfileCode;
        _duosGroup = duosGroup;
        return this;
    }

    public SupplyPointLoadedFromV1Builder WithUsageFactors(params LoadedUsageFactor[] usageFactors)
    {
        _usageFactors = usageFactors;
        return this;
    }

    public SupplyPointLoadedFromV1 Build() => new()
    {
        EventId = _eventId,
        PublishingContext = "platform.v1-reader",
        OccurredAtUtc = _occurredAtUtc,
        CorrelationId = $"v1-reader:meter-point-sync:{_occurredAtUtc.UtcDateTime:yyyyMMdd'T'HHmmssfff'Z'}",
        AsOf = _occurredAtUtc,
        MPRN = _mprn,
        Market = "roi-esb",
        CodesFrom = _codesFrom,
        MeterConfigurationCode = _meterConfigurationCode,
        LoadProfileCode = _loadProfileCode,
        DuosGroup = _duosGroup,
        UsageFactors = _usageFactors,
    };
}
