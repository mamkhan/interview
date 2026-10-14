using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.TestsShared;

// Defaults are the agreed example MeterConfigurationReceivedBuilder and UsageFactorsReceivedBuilder carry, as SupplyPoints
// receives it: an illustrative MM332 meter exchange for MPRN 10012345678, MCC01, load profile 01, DUoS group DG1,
// energised, effective 2026-09-21, the removed meter's actual 24H factor 3842.5173 from 2026-07-16 and the new meter's
// estimated 24H factor 3910.0 from 2026-09-21. It lists no meter.
public sealed class MeterConfigurationChangeReceivedBuilder
{
    private MeterConfigurationChangeReceived _event = new()
    {
        EventId = Guid.Parse("9a3c5e71-2b4d-5f6a-8c9e-0d1f2a3b4c5d"),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 28, 2, 50, 14, 201, TimeSpan.Zero),
        CorrelationId = "71039e6c-de90-565a-8cd7-89b263271615",
        MPRN = "10012345678",
        NetworksReferenceNumber = "000000123456",
        EffectiveFromDate = new DateOnly(2026, 9, 21),
        Configuration = new MeterConfiguration
        {
            MeterConfigurationCode = "MCC01",
            LoadProfileCode = "01",
            DuosGroup = "DG1",
            MeterPointStatusCode = "E",
            UsageFactors =
            [
                new UsageFactor("24H", new DateOnly(2026, 7, 16), ActualUsageFactor: 3842.5173m, EstimatedUsageFactor: null),
                new UsageFactor("24H", new DateOnly(2026, 9, 21), ActualUsageFactor: null, EstimatedUsageFactor: 3910.0m),
            ],
            Meters = [],
        },
        Provenance = new ReadingProvenance(
            "roi-esb",
            "332",
            new DateTimeOffset(2026, 9, 27, 21, 14, 3, 112, TimeSpan.Zero),
            $"roi-esb/{MeterConfigurationReceivedBuilder.ContentHash}.xml",
            MeterConfigurationReceivedBuilder.ContentHash,
            "DSO_MIM_332_1790000000000_4100001.xml"),
    };

    // The real MM320 sample ESB_MM320_10900000320 as MM320Strategy states it: a change of supplier read on 2026-09-30
    // with no effective date, MCC16, load profile 25, DG1, E, an estimate for each of 01D, 01N and 01P, and meter
    // 90000320 (RM107, location 07) in place with registers 001 to 003, each with its change-of-supplier read 0.
    public MeterConfigurationChangeReceivedBuilder AsTheRealMm320States()
    {
        var readDate = new DateOnly(2026, 9, 30);
        var read = new RegisterRead(0m, "A", "26", null, null);
        _event = _event with
        {
            EventId = Guid.Parse("3e7b9d21-6f4a-5c8e-a0b2-4d6f8a1c3e5b"),
            OccurredAtUtc = new DateTimeOffset(2026, 10, 1, 6, 56, 2, 0, TimeSpan.Zero),
            CorrelationId = "1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b",
            MPRN = "10900000320",
            NetworksReferenceNumber = "019000000320",
            EffectiveFromDate = null,
            ReadDate = readDate,
            Configuration = new MeterConfiguration
            {
                MeterConfigurationCode = "MCC16",
                LoadProfileCode = "25",
                DuosGroup = "DG1",
                MeterPointStatusCode = "E",
                UsageFactors =
                [
                    new UsageFactor("01D", readDate, ActualUsageFactor: null, EstimatedUsageFactor: 4389.66894m),
                    new UsageFactor("01N", readDate, ActualUsageFactor: null, EstimatedUsageFactor: 6402.89496m),
                    new UsageFactor("01P", readDate, ActualUsageFactor: null, EstimatedUsageFactor: 886.4361m),
                ],
                Meters =
                [
                    new ConfiguredMeter
                    {
                        SerialNumber = "90000320",
                        MeterCategoryCode = "RM107",
                        MeterLocationCode = "07",
                        Change = MeterChanges.InPlace,
                        Registers =
                        [
                            Register("001", "01D", "78", read),
                            Register("002", "01N", "79", read),
                            Register("003", "01P", "80", read),
                        ],
                    },
                ],
            },
            Provenance = new ReadingProvenance(
                "roi-esb",
                "320",
                new DateTimeOffset(2026, 10, 1, 6, 55, 18, 74, TimeSpan.Zero),
                "roi-esb/6244b8d5c3a1de7d6387cf90f2d3085471f32a1ec72fc230fe2feb592da29bfc.xml",
                "6244b8d5c3a1de7d6387cf90f2d3085471f32a1ec72fc230fe2feb592da29bfc",
                "ESB_MM320_10900000320_0000000790000320.xml"),
        };
        return this;
    }

    // The real MM331 sample ESB_MM331_10900000331 as MM331Strategy states it: effective 2026-09-16, MCC12, no load
    // profile, DG1, E, no usage factors, and new smart meter 90003311 (RM106, location 07, exchanged meter reference
    // 000000000090003311) with channel 70 (KWT, every 30 minutes) and channel 74 (KWH, every 24).
    public MeterConfigurationChangeReceivedBuilder AsTheRealMm331States()
    {
        _event = _event with
        {
            EventId = Guid.Parse("c18f2a4d-7e3b-5a9c-b6d0-2e4f6a8c0b1d"),
            OccurredAtUtc = new DateTimeOffset(2026, 9, 17, 14, 12, 3, 0, TimeSpan.Zero),
            CorrelationId = "5a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d",
            MPRN = "10900000331",
            NetworksReferenceNumber = null,
            EffectiveFromDate = new DateOnly(2026, 9, 16),
            ReadDate = null,
            Configuration = new MeterConfiguration
            {
                MeterConfigurationCode = "MCC12",
                LoadProfileCode = null,
                DuosGroup = "DG1",
                MeterPointStatusCode = "E",
                UsageFactors = [],
                Meters =
                [
                    new ConfiguredMeter
                    {
                        SerialNumber = "90003311",
                        MeterCategoryCode = "RM106",
                        MeterLocationCode = "07",
                        ExchangedMeterReference = "000000000090003311",
                        Change = MeterChanges.New,
                        Registers =
                        [
                            new ConfiguredRegister { RegisterTypeCode = "70", UOMCode = "KWT", MeteringInterval = 30 },
                            new ConfiguredRegister { RegisterTypeCode = "74", UOMCode = "KWH", MeteringInterval = 24 },
                        ],
                    },
                ],
            },
            Provenance = new ReadingProvenance(
                "roi-esb",
                "331",
                new DateTimeOffset(2026, 9, 17, 14, 11, 42, 57, TimeSpan.Zero),
                "roi-esb/da5f00ba23fd17a7db68526b199868583c4dcb6835a12ece9562a2c8c556ec4b.xml",
                "da5f00ba23fd17a7db68526b199868583c4dcb6835a12ece9562a2c8c556ec4b",
                "ESB_MM331_10900000331_0000000790000331.xml"),
        };
        return this;
    }

    public MeterConfigurationChangeReceivedBuilder WithEventId(Guid eventId)
    {
        _event = _event with { EventId = eventId };
        return this;
    }

    public MeterConfigurationChangeReceivedBuilder EffectiveFrom(DateOnly date)
    {
        _event = _event with { EffectiveFromDate = date, ReadDate = null };
        return this;
    }

    // The shape a 320 sends: no effective date of its own, only the change-of-supplier reading's date.
    public MeterConfigurationChangeReceivedBuilder ReadOn(DateOnly date)
    {
        _event = _event with { EffectiveFromDate = null, ReadDate = date };
        return this;
    }

    public MeterConfigurationChangeReceivedBuilder WithNoDates()
    {
        _event = _event with { EffectiveFromDate = null, ReadDate = null };
        return this;
    }

    public MeterConfigurationChangeReceivedBuilder WithCodes(string? meterConfigurationCode, string? loadProfileCode, string? duosGroup,
        string? meterPointStatusCode)
    {
        _event = _event with
        {
            Configuration = _event.Configuration with
            {
                MeterConfigurationCode = meterConfigurationCode,
                LoadProfileCode = loadProfileCode,
                DuosGroup = duosGroup,
                MeterPointStatusCode = meterPointStatusCode,
            },
        };
        return this;
    }

    public MeterConfigurationChangeReceivedBuilder WithoutUsageFactors()
    {
        _event = _event with { Configuration = _event.Configuration with { UsageFactors = [] } };
        return this;
    }

    public MeterConfigurationChangeReceivedBuilder WithLoadProfileCode(string? code)
    {
        _event = _event with { Configuration = _event.Configuration with { LoadProfileCode = code } };
        return this;
    }

    public MeterConfigurationChangeReceived Build() => _event;

    private static ConfiguredRegister Register(string sequence, string timeslotCode, string registerTypeCode, RegisterRead read) => new()
    {
        MeterRegisterSequence = sequence,
        TimeslotCode = timeslotCode,
        RegisterTypeCode = registerTypeCode,
        UOMCode = "KWH",
        MeterMultiplier = 1m,
        PreDecimalDetails = "07",
        PostDecimalDetails = "0",
        Read = read,
    };
}
