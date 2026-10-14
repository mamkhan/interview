using System.Text.Json;
using AwesomeAssertions;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// The payloads agreed for the event Store reads, as the bus carries them, read with the serializer settings the
// consumer uses: a whole configuration change from each of the real MM320 and MM331 samples, as MM320Strategy and
// MM331Strategy state it.
public class WhenReadingTheAgreedEventPayloads
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string ChangeOfSupplierJson = """
        {
          "eventId": "3e7b9d21-6f4a-5c8e-a0b2-4d6f8a1c3e5b",
          "publishingContext": "metering-consumption.market-messaging",
          "occurredAtUtc": "2026-10-01T06:56:02+00:00",
          "correlationId": "1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b",
          "asOf": null,
          "mprn": "10900000320",
          "networksReferenceNumber": "019000000320",
          "effectiveFromDate": null,
          "readDate": "2026-09-30",
          "configuration": {
            "meterConfigurationCode": "MCC16",
            "loadProfileCode": "25",
            "duosGroup": "DG1",
            "meterPointStatusCode": "E",
            "usageFactors": [
              { "timeslotCode": "01D", "effectiveFromDate": "2026-09-30", "actualUsageFactor": null, "estimatedUsageFactor": 4389.66894 },
              { "timeslotCode": "01N", "effectiveFromDate": "2026-09-30", "actualUsageFactor": null, "estimatedUsageFactor": 6402.89496 },
              { "timeslotCode": "01P", "effectiveFromDate": "2026-09-30", "actualUsageFactor": null, "estimatedUsageFactor": 886.4361 }
            ],
            "meters": [
              {
                "serialNumber": "90000320",
                "meterCategoryCode": "RM107",
                "meterLocationCode": "07",
                "exchangedMeterReference": null,
                "change": "in-place",
                "registers": [
                  {
                    "meterRegisterSequence": "001", "timeslotCode": "01D", "registerTypeCode": "78", "uomCode": "KWH",
                    "meterMultiplier": 1, "meteringInterval": null, "preDecimalDetails": "07", "postDecimalDetails": "0",
                    "read": { "readingValue": 0, "readTypeCode": "A", "readReasonCode": "26", "readStatusCode": null, "previousReadDate": null }
                  },
                  {
                    "meterRegisterSequence": "002", "timeslotCode": "01N", "registerTypeCode": "79", "uomCode": "KWH",
                    "meterMultiplier": 1, "meteringInterval": null, "preDecimalDetails": "07", "postDecimalDetails": "0",
                    "read": { "readingValue": 0, "readTypeCode": "A", "readReasonCode": "26", "readStatusCode": null, "previousReadDate": null }
                  },
                  {
                    "meterRegisterSequence": "003", "timeslotCode": "01P", "registerTypeCode": "80", "uomCode": "KWH",
                    "meterMultiplier": 1, "meteringInterval": null, "preDecimalDetails": "07", "postDecimalDetails": "0",
                    "read": { "readingValue": 0, "readTypeCode": "A", "readReasonCode": "26", "readStatusCode": null, "previousReadDate": null }
                  }
                ]
              }
            ]
          },
          "provenance": {
            "market": "roi-esb",
            "sourceMessageType": "320",
            "marketTimestamp": "2026-10-01T06:55:18.074+00:00",
            "blobPath": "roi-esb/6244b8d5c3a1de7d6387cf90f2d3085471f32a1ec72fc230fe2feb592da29bfc.xml",
            "contentHash": "6244b8d5c3a1de7d6387cf90f2d3085471f32a1ec72fc230fe2feb592da29bfc",
            "receivedFileName": "ESB_MM320_10900000320_0000000790000320.xml"
          }
        }
        """;

    private const string SmartMeterExchangeJson = """
        {
          "eventId": "c18f2a4d-7e3b-5a9c-b6d0-2e4f6a8c0b1d",
          "publishingContext": "metering-consumption.market-messaging",
          "occurredAtUtc": "2026-09-17T14:12:03+00:00",
          "correlationId": "5a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d",
          "asOf": null,
          "mprn": "10900000331",
          "networksReferenceNumber": null,
          "effectiveFromDate": "2026-09-16",
          "readDate": null,
          "configuration": {
            "meterConfigurationCode": "MCC12",
            "loadProfileCode": null,
            "duosGroup": "DG1",
            "meterPointStatusCode": "E",
            "usageFactors": [],
            "meters": [
              {
                "serialNumber": "90003311",
                "meterCategoryCode": "RM106",
                "meterLocationCode": "07",
                "exchangedMeterReference": "000000000090003311",
                "change": "new",
                "registers": [
                  {
                    "meterRegisterSequence": null, "timeslotCode": null, "registerTypeCode": "70", "uomCode": "KWT",
                    "meterMultiplier": null, "meteringInterval": 30, "preDecimalDetails": null, "postDecimalDetails": null, "read": null
                  },
                  {
                    "meterRegisterSequence": null, "timeslotCode": null, "registerTypeCode": "74", "uomCode": "KWH",
                    "meterMultiplier": null, "meteringInterval": 24, "preDecimalDetails": null, "postDecimalDetails": null, "read": null
                  }
                ]
              }
            ]
          },
          "provenance": {
            "market": "roi-esb",
            "sourceMessageType": "331",
            "marketTimestamp": "2026-09-17T14:11:42.057+00:00",
            "blobPath": "roi-esb/da5f00ba23fd17a7db68526b199868583c4dcb6835a12ece9562a2c8c556ec4b.xml",
            "contentHash": "da5f00ba23fd17a7db68526b199868583c4dcb6835a12ece9562a2c8c556ec4b",
            "receivedFileName": "ESB_MM331_10900000331_0000000790000331.xml"
          }
        }
        """;

    [Fact]
    public void ReadsAWholeChangeOfSupplierWithEveryUsageFactorDecimalAndEveryRegistersRead() =>
        JsonSerializer.Deserialize<MeterConfigurationChangeReceived>(ChangeOfSupplierJson, Web).Should().BeEquivalentTo(
            new MeterConfigurationChangeReceivedBuilder().AsTheRealMm320States().Build(), options => options.ComparingRecordsByMembers());

    [Fact]
    public void ReadsANewSmartMeterWithItsExchangedReferenceAndChannels() =>
        JsonSerializer.Deserialize<MeterConfigurationChangeReceived>(SmartMeterExchangeJson, Web).Should().BeEquivalentTo(
            new MeterConfigurationChangeReceivedBuilder().AsTheRealMm331States().Build(), options => options.ComparingRecordsByMembers());
}
