using System.Text.Json;
using AwesomeAssertions;
using Growth.Contracts.Events;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.Consumption.UnitTests.Profile;

// The payloads agreed for the events the Profile reads, as the bus carries them, read with the serializer settings
// the consumer uses. The market payloads come from one illustrative MM332 meter exchange, which states no smart
// meter flag; the V1 reader's come from its run of 2026-10-07 at 04:00 UTC.
public class WhenReadingTheAgreedEventPayloads
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string MeterConfigurationJson = """
        {
          "eventId": "e1407479-3136-56c0-9908-bb02fb0339e2",
          "publishingContext": "metering-consumption.market-messaging",
          "occurredAtUtc": "2026-09-28T02:50:14.201+00:00",
          "correlationId": "71039e6c-de90-565a-8cd7-89b263271615",
          "asOf": null,
          "mprn": "10012345678",
          "meterConfigurationCode": "MCC01",
          "loadProfileCode": "01",
          "duosGroup": "DG1",
          "meterPointStatusCode": "E",
          "effectiveFromDate": "2026-09-21",
          "readDate": null,
          "networksReferenceNumber": "000000123456",
          "provenance": {
            "market": "roi-esb",
            "sourceMessageType": "332",
            "marketTimestamp": "2026-09-27T21:14:03.112+00:00",
            "blobPath": "roi-esb/801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25.xml",
            "contentHash": "801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25",
            "receivedFileName": "DSO_MIM_332_1790000000000_4100001.xml"
          }
        }
        """;

    private const string UsageFactorsJson = """
        {
          "eventId": "3c480084-0bec-5ee3-a530-e96a1170273a",
          "publishingContext": "metering-consumption.market-messaging",
          "occurredAtUtc": "2026-09-28T02:50:14.201+00:00",
          "correlationId": "71039e6c-de90-565a-8cd7-89b263271615",
          "asOf": null,
          "mprn": "10012345678",
          "readDate": "2026-09-21",
          "networksReferenceNumber": "000000123456",
          "usageFactors": [
            { "timeslotCode": "24H", "effectiveFromDate": "2026-07-16", "estimatedUsageFactor": null, "actualUsageFactor": 3842.5173 },
            { "timeslotCode": "24H", "effectiveFromDate": "2026-09-21", "estimatedUsageFactor": 3910.0, "actualUsageFactor": null }
          ],
          "provenance": {
            "market": "roi-esb",
            "sourceMessageType": "332",
            "marketTimestamp": "2026-09-27T21:14:03.112+00:00",
            "blobPath": "roi-esb/801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25.xml",
            "contentHash": "801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25",
            "receivedFileName": "DSO_MIM_332_1790000000000_4100001.xml"
          }
        }
        """;

    private const string V1SmartMeterFlagJson = """
        {
          "eventId": "8b7c2e1a-4d5f-5a9b-8c3e-1f2a3b4c5d6e",
          "publishingContext": "platform.v1-reader",
          "occurredAtUtc": "2026-10-07T04:00:00+00:00",
          "correlationId": "v1-reader:smart-meter-flag:2026-10-07",
          "asOf": "2026-10-07T04:00:00+00:00",
          "mprn": "10012345678",
          "meterConfigurationCode": null,
          "loadProfileCode": null,
          "duosGroup": null,
          "meterPointStatusCode": null,
          "effectiveFromDate": null,
          "readDate": null,
          "networksReferenceNumber": null,
          "isNsmp": true,
          "provenance": {
            "market": "roi-esb",
            "sourceMessageType": "V1",
            "marketTimestamp": "2026-10-07T04:00:00+00:00",
            "blobPath": "",
            "contentHash": "v1-reader:2026-10-07:1",
            "receivedFileName": ""
          }
        }
        """;

    private const string TariffResolvedJson = """
        {
          "eventId": "3f1d2c64-8a51-5b0e-9c47-2e6a9b1d7f05",
          "publishingContext": "platform.v1-reader",
          "occurredAtUtc": "2026-10-07T04:00:00+00:00",
          "correlationId": "v1-reader:tariff-values:2026-10-07",
          "asOf": "2026-10-07T04:00:00+00:00",
          "mprn": "10012345678",
          "market": "roi-esb",
          "isTou": true,
          "tariffCalcType": "TwentyFourHour",
          "effectiveLoadProfileCode": "LP1",
          "source": "v1-reader/tariff-values"
        }
        """;

    [Fact]
    public void ReadsEveryMeterConfigurationField()
    {
        var @event = JsonSerializer.Deserialize<MeterConfigurationReceived>(MeterConfigurationJson, Web)!;

        @event.MPRN.Should().Be("10012345678");
        @event.MeterConfigurationCode.Should().Be("MCC01");
        @event.LoadProfileCode.Should().Be("01");
        @event.DuosGroup.Should().Be("DG1");
        @event.MeterPointStatusCode.Should().Be("E");
        @event.EffectiveFromDate.Should().Be(new DateOnly(2026, 9, 21));
        @event.ReadDate.Should().BeNull();
        @event.NetworksReferenceNumber.Should().Be("000000123456");
        @event.Provenance.ContentHash.Should().Be("801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25");
        @event.Provenance.SourceMessageType.Should().Be("332");
    }

    [Fact]
    public void ReadsEveryUsageFactorWithItsDecimals()
    {
        var @event = JsonSerializer.Deserialize<UsageFactorsReceived>(UsageFactorsJson, Web)!;

        @event.MPRN.Should().Be("10012345678");
        @event.ReadDate.Should().Be(new DateOnly(2026, 9, 21));
        @event.UsageFactors.Should().Equal(
            new UsageFactor("24H", new DateOnly(2026, 7, 16), EstimatedUsageFactor: null, ActualUsageFactor: 3842.5173m),
            new UsageFactor("24H", new DateOnly(2026, 9, 21), EstimatedUsageFactor: 3910.0m, ActualUsageFactor: null));
        @event.Provenance.Market.Should().Be("roi-esb");
    }

    [Fact]
    public void ReadsAMarketPayloadThatStatesNoFlagAsEmpty() =>
        JsonSerializer.Deserialize<MeterConfigurationReceived>(MeterConfigurationJson, Web)!.IsNsmp.Should().BeNull();

    [Fact]
    public void ReadsTheV1ReadersSmartMeterFlagWithEveryOtherFieldEmpty()
    {
        var @event = JsonSerializer.Deserialize<MeterConfigurationReceived>(V1SmartMeterFlagJson, Web)!;

        @event.IsNsmp.Should().BeTrue();
        @event.AsOf.Should().Be(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
        @event.MeterConfigurationCode.Should().BeNull();
        @event.EffectiveFromDate.Should().BeNull();
        @event.ReadDate.Should().BeNull();
        @event.Provenance.SourceMessageType.Should().Be("V1");
        @event.Provenance.ContentHash.Should().Be("v1-reader:2026-10-07:1");
        @event.Provenance.BlobPath.Should().BeEmpty();
    }

    [Fact]
    public void ReadsEveryTariffField()
    {
        var @event = JsonSerializer.Deserialize<TariffResolved>(TariffResolvedJson, Web)!;

        @event.MPRN.Should().Be("10012345678");
        @event.Market.Should().Be("roi-esb");
        @event.IsTou.Should().BeTrue();
        @event.TariffCalcType.Should().Be("TwentyFourHour");
        @event.EffectiveLoadProfileCode.Should().Be("LP1");
        @event.AsOf.Should().Be(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
        @event.Source.Should().Be("v1-reader/tariff-values");
    }
}
