using System.Text.Json;
using AwesomeAssertions;
using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.UnitTests.Reload;

// The payload agreed for the event Reload reads, as the V1 reader puts it on the bus, read with the serializer
// settings the consumer uses: the builder's supply point, an estimate dated with the codes and a later actual.
public class WhenReadingTheAgreedEventPayloads
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string LoadedJson = """
        {
          "eventId": "4d2f8a61-3c7b-5e9a-8b1d-6f0e2a4c8e13",
          "publishingContext": "platform.v1-reader",
          "occurredAtUtc": "2026-10-12T05:00:00+00:00",
          "correlationId": "v1-reader:meter-point-sync:20261012T050000000Z",
          "asOf": "2026-10-12T05:00:00+00:00",
          "mprn": "10012345678",
          "market": "roi-esb",
          "codesFrom": "2026-08-03",
          "meterConfigurationCode": "MCC01",
          "loadProfileCode": "01",
          "duosGroup": "DG1",
          "usageFactors": [
            { "timeslotCode": "24H", "effectiveFromDate": "2026-08-03", "estimatedUsageFactor": 4127.5, "actualUsageFactor": null },
            { "timeslotCode": "24H", "effectiveFromDate": "2026-09-01", "estimatedUsageFactor": null, "actualUsageFactor": 3980.25 }
          ]
        }
        """;

    [Fact]
    public void ReadsTheCodesWithTheirDateAndEachUsageFactorWithItsOwn() =>
        JsonSerializer.Deserialize<SupplyPointLoadedFromV1>(LoadedJson, Web).Should().BeEquivalentTo(
            new SupplyPointLoadedFromV1Builder().Build(), options => options.ComparingRecordsByMembers());
}
