using System.Text.Json;
using AwesomeAssertions;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// The two agreed ConsumptionRecorded payloads, as the bus carries them, read with the consumer's serializer
// settings. The first reading is the repo's real extract for MPRN 10900000343 on 2026-06-24 (periods 1 and 2
// of 48, IntervalValue × 0.5); the replacement is an illustrative RRVN 2 of that day in another file. Event ids
// are DeterministicGuid of the ledger entry id, derived offline in Python.
public class WhenReadingTheRecordedEventPayloads
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string FirstReadingJson = """
        {
          "eventId": "3ae8c6ab-82fe-fc3d-3b08-9677737a0a19",
          "publishingContext": "metering-consumption.consumption",
          "occurredAtUtc": "2026-06-25T02:51:03.417+00:00",
          "correlationId": "5c2a4f1e-7d3b-4e8a-9f61-0b2c3d4e5f60",
          "asOf": null,
          "ledgerEntryId": "e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304:10900000343:2026-06-24:1",
          "supplyPointId": "10900000343",
          "meterSerial": "90003431",
          "readDate": "2026-06-24",
          "rrvn": 1,
          "supersedesRRVN": null,
          "entryType": "ReadingReceived",
          "postedAtUtc": "2026-06-25T02:51:03.417+00:00",
          "meteringIntervalMinutes": 30,
          "uomCode": "KWT",
          "fuel": "electricity",
          "reversal": null,
          "reading": {
            "rrvn": 1,
            "periods": [
              { "period": 1, "kwh": 0.070, "status": "VVAK" },
              { "period": 2, "kwh": 0.0715, "status": "VVAK" }
            ],
            "totalKwh": 0.1415
          },
          "provenance": {
            "market": "roi-esb",
            "sourceMessageType": "343",
            "marketTimestamp": "2026-06-25T02:50:10.918+00:00",
            "blobPath": "roi-esb/e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304.xml",
            "contentHash": "e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304",
            "receivedFileName": "DSO_MIM_343_1782352213415_3805737.xml"
          }
        }
        """;

    private const string ReplacementJson = """
        {
          "eventId": "935f5855-0d00-8505-375b-2acfff6e9508",
          "publishingContext": "metering-consumption.consumption",
          "occurredAtUtc": "2026-06-27T02:48:55.102+00:00",
          "correlationId": "9e1d2c3b-4a5f-4c6d-8e7f-a0b1c2d3e4f5",
          "asOf": null,
          "ledgerEntryId": "5b9dc5a2fb1410b70265f947f13f93d8df2c3eddfaa5e8592977f145f03eff26:10900000343:2026-06-24:2",
          "supplyPointId": "10900000343",
          "meterSerial": "90003431",
          "readDate": "2026-06-24",
          "rrvn": 2,
          "supersedesRRVN": 1,
          "entryType": "ReplacementReadingReceived",
          "postedAtUtc": "2026-06-27T02:48:55.102+00:00",
          "meteringIntervalMinutes": 30,
          "uomCode": "KWT",
          "fuel": "electricity",
          "reversal": {
            "rrvn": 1,
            "periods": [
              { "period": 1, "kwh": -0.070, "status": "VVAK" },
              { "period": 2, "kwh": -0.0715, "status": "VVAK" }
            ],
            "totalKwh": -0.1415
          },
          "reading": {
            "rrvn": 2,
            "periods": [
              { "period": 1, "kwh": 0.065, "status": "VCHG" },
              { "period": 2, "kwh": 0.0715, "status": "VVAK" }
            ],
            "totalKwh": 0.1365
          },
          "provenance": {
            "market": "roi-esb",
            "sourceMessageType": "343",
            "marketTimestamp": "2026-06-27T02:47:31.006+00:00",
            "blobPath": "roi-esb/5b9dc5a2fb1410b70265f947f13f93d8df2c3eddfaa5e8592977f145f03eff26.xml",
            "contentHash": "5b9dc5a2fb1410b70265f947f13f93d8df2c3eddfaa5e8592977f145f03eff26",
            "receivedFileName": "DSO_MIM_343_1782500000000_3806001.xml"
          }
        }
        """;

    [Fact]
    public void ReadsAFirstReadingWithNoReversal()
    {
        var recorded = JsonSerializer.Deserialize<ConsumptionRecorded>(FirstReadingJson, Web)!;

        recorded.EventId.Should().Be(Guid.Parse("3ae8c6ab-82fe-fc3d-3b08-9677737a0a19"));
        recorded.SupplyPointId.Should().Be("10900000343");
        recorded.ReadDate.Should().Be(new DateOnly(2026, 6, 24));
        recorded.EntryType.Should().Be("ReadingReceived");
        recorded.SupersedesRRVN.Should().BeNull();
        recorded.Fuel.Should().Be("electricity");
        recorded.Reversal.Should().BeNull();
        recorded.Reading.RRVN.Should().Be(1);
        recorded.Reading.Periods.Should().Equal(
            new LedgerPeriod(1, 0.070m, "VVAK"),
            new LedgerPeriod(2, 0.0715m, "VVAK"));
        recorded.Reading.TotalKwh.Should().Be(0.1415m);
    }

    [Fact]
    public void ReadsAReplacementWithTheCancelledVersionBesideTheNewOne()
    {
        var recorded = JsonSerializer.Deserialize<ConsumptionRecorded>(ReplacementJson, Web)!;

        recorded.EntryType.Should().Be("ReplacementReadingReceived");
        recorded.RRVN.Should().Be(2);
        recorded.SupersedesRRVN.Should().Be(1);
        recorded.Reversal!.RRVN.Should().Be(1);
        recorded.Reversal.Periods.Should().Equal(
            new LedgerPeriod(1, -0.070m, "VVAK"),
            new LedgerPeriod(2, -0.0715m, "VVAK"));
        recorded.Reversal.TotalKwh.Should().Be(-0.1415m);
        recorded.Reading.Periods[0].Should().Be(new LedgerPeriod(1, 0.065m, "VCHG"));
        recorded.Reading.TotalKwh.Should().Be(0.1365m);
    }

    [Fact]
    public void WritesBackTheFieldsItRead()
    {
        var recorded = JsonSerializer.Deserialize<ConsumptionRecorded>(ReplacementJson, Web)!;

        var roundTripped = JsonSerializer.Deserialize<ConsumptionRecorded>(JsonSerializer.Serialize(recorded, Web), Web);

        roundTripped.Should().BeEquivalentTo(recorded);
    }
}
