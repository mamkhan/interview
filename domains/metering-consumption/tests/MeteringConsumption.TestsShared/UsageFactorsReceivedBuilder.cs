using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.TestsShared;

// Defaults are the agreed UsageFactorsReceived example: an illustrative MM332 meter exchange for MPRN
// 10012345678 with the removed meter's actual 24H factor, 3842.5173 effective 2026-07-16, and the new meter's
// estimated 24H factor, 3910.0 effective 2026-09-21.
public sealed class UsageFactorsReceivedBuilder
{
    private string _mprn = "10012345678";
    private string _contentHash = MeterConfigurationReceivedBuilder.ContentHash;
    private List<UsageFactor> _factors =
    [
        new("24H", new DateOnly(2026, 7, 16), EstimatedUsageFactor: null, ActualUsageFactor: 3842.5173m),
        new("24H", new DateOnly(2026, 9, 21), EstimatedUsageFactor: 3910.0m, ActualUsageFactor: null),
    ];

    public UsageFactorsReceivedBuilder ForMprn(string mprn)
    {
        _mprn = mprn;
        return this;
    }

    public UsageFactorsReceivedBuilder FromFile(string contentHash)
    {
        _contentHash = contentHash;
        return this;
    }

    public UsageFactorsReceivedBuilder WithFactors(params UsageFactor[] factors)
    {
        _factors = [.. factors];
        return this;
    }

    public UsageFactorsReceivedBuilder WithNoFactors() => WithFactors();

    public UsageFactorsReceived Build() => new()
    {
        EventId = Guid.Parse("3c480084-0bec-5ee3-a530-e96a1170273a"),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 28, 2, 50, 14, 201, TimeSpan.Zero),
        CorrelationId = "71039e6c-de90-565a-8cd7-89b263271615",
        MPRN = _mprn,
        NetworksReferenceNumber = "000000123456",
        ReadDate = new DateOnly(2026, 9, 21),
        UsageFactors = _factors,
        Provenance = new ReadingProvenance(
            "roi-esb",
            "332",
            new DateTimeOffset(2026, 9, 27, 21, 14, 3, 112, TimeSpan.Zero),
            $"roi-esb/{_contentHash}.xml",
            _contentHash,
            "DSO_MIM_332_1790000000000_4100001.xml"),
    };
}
