using System.Text.Json;
using AwesomeAssertions;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Consumption.UnitTests.Forecast;

// The two agreed ConsumptionForecastUpdated payloads, as the bus carries them, read with the consumer's serializer
// settings: a forecast from the meter's own use, and the 62 zeros of a meter with no usable usage factor. Both are
// illustrative, with the daily series cut to its first two days. Event ids are DeterministicGuid of the forecast
// id, derived offline in Python.
public class WhenReadingTheForecastEventPayloads
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string ComputedJson = """
        {
          "eventId": "4af83314-736a-4d41-fdea-68b253f9fb89",
          "publishingContext": "metering-consumption.consumption",
          "occurredAtUtc": "2026-06-25T02:51:04.120+00:00",
          "correlationId": "5c2a4f1e-7d3b-4e8a-9f61-0b2c3d4e5f60",
          "asOf": null,
          "supplyPointId": "10900000343",
          "forecastId": "10900000343:7",
          "forecastVersion": 7,
          "anchorDate": "2026-06-25",
          "horizonDays": 62,
          "totalKwh": 541.093217,
          "usageFactorSource": "PUF",
          "daily": [
            { "date": "2026-06-25", "kwh": 8.725931 },
            { "date": "2026-06-26", "kwh": 8.698214 }
          ],
          "triggerLedgerEntryId": "e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304:10900000343:2026-06-24:1",
          "computedAtUtc": "2026-06-25T02:51:04.120+00:00"
        }
        """;

    private const string ZeroForecastJson = """
        {
          "eventId": "20290b20-b48b-f0b7-8468-797b57b67620",
          "publishingContext": "metering-consumption.consumption",
          "occurredAtUtc": "2026-06-26T02:49:12.004+00:00",
          "correlationId": "9e1d2c3b-4a5f-4c6d-8e7f-a0b1c2d3e4f5",
          "asOf": null,
          "supplyPointId": "10900000343",
          "forecastId": "10900000343:8",
          "forecastVersion": 8,
          "anchorDate": "2026-06-26",
          "horizonDays": 62,
          "totalKwh": 0,
          "usageFactorSource": "None",
          "daily": [
            { "date": "2026-06-26", "kwh": 0 },
            { "date": "2026-06-27", "kwh": 0 }
          ],
          "triggerLedgerEntryId": "5b9dc5a2fb1410b70265f947f13f93d8df2c3eddfaa5e8592977f145f03eff26:10900000343:2026-06-25:1",
          "computedAtUtc": "2026-06-26T02:49:12.004+00:00"
        }
        """;

    [Fact]
    public void ReadsAForecastFromTheMetersOwnUse()
    {
        var updated = JsonSerializer.Deserialize<ConsumptionForecastUpdated>(ComputedJson, Web)!;

        updated.EventId.Should().Be(Guid.Parse("4af83314-736a-4d41-fdea-68b253f9fb89"));
        updated.SupplyPointId.Should().Be("10900000343");
        updated.ForecastId.Should().Be("10900000343:7");
        updated.ForecastVersion.Should().Be(7);
        updated.AnchorDate.Should().Be(new DateOnly(2026, 6, 25));
        updated.HorizonDays.Should().Be(62);
        updated.TotalKwh.Should().Be(541.093217m);
        updated.UsageFactorSource.Should().Be("PUF");
        updated.Daily.Should().Equal(
            new ForecastDay(new DateOnly(2026, 6, 25), 8.725931m),
            new ForecastDay(new DateOnly(2026, 6, 26), 8.698214m));
        updated.TriggerLedgerEntryId.Should().EndWith(":10900000343:2026-06-24:1");
    }

    [Fact]
    public void ReadsAZeroForecastMarkedAsHavingNoUsageFactor()
    {
        var updated = JsonSerializer.Deserialize<ConsumptionForecastUpdated>(ZeroForecastJson, Web)!;

        updated.UsageFactorSource.Should().Be("None");
        updated.TotalKwh.Should().Be(0m);
        updated.Daily.Should().OnlyContain(day => day.Kwh == 0m);
    }

    [Fact]
    public void WritesBackTheFieldsItRead()
    {
        var updated = JsonSerializer.Deserialize<ConsumptionForecastUpdated>(ComputedJson, Web)!;

        var roundTripped = JsonSerializer.Deserialize<ConsumptionForecastUpdated>(JsonSerializer.Serialize(updated, Web), Web);

        roundTripped.Should().BeEquivalentTo(updated);
    }
}
