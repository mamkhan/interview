using System.Text.Json;
using AwesomeAssertions;

namespace MeteringConsumption.Consumption.UnitTests.Forecast;

// The persona's reading is recorded: ten days of 12 kWh give the own-use forecast whose first day is the proof of
// concept's Native 11.037274915196553439267315840 kWh (WhenForecastingASingleRateMeter). The event id is
// DeterministicGuid of the forecast id 10001234567:1, derived offline in Python.
public sealed class WhenForecastingARecordedReading
{
    private readonly ForecastHandling _handling = new();

    [Fact]
    public async Task AsksTheLedgerForTheWindowUpToTheDayTheReadingWasPosted()
    {
        await _handling.DeliverAsync(ForecastHandling.Recorded());

        _handling.Ledger.Asked.Should().Equal((ForecastHandling.SupplyPointId, new DateOnly(2026, 5, 1), 29));
    }

    [Fact]
    public async Task AsksSupplyPointsForTheConfigurationInEffectOnTheFirstForecastDay()
    {
        await _handling.DeliverAsync(ForecastHandling.Recorded());

        _handling.Configurations.AskedDays.Should().Equal(new DateOnly(2026, 5, 1));
    }

    [Fact]
    public async Task StoresTheForecastAsTheSupplyPointsFirstVersion()
    {
        await _handling.DeliverAsync(ForecastHandling.Recorded());

        var stored = _handling.Store.Versions.Single();
        stored.ForecastId.Should().Be("10001234567:1");
        stored.UsageFactorSource.Should().Be("PUF");
        stored.ComputedAtUtc.Should().Be(ForecastHandling.Now);
        stored.TriggerLedgerEntryId.Should().Be("hash:10001234567:2026-04-30:1");
        stored.Daily.Should().HaveCount(62);
        stored.Daily[0].Kwh.Should().Be(11.037274915196553439267315840m);
    }

    [Fact]
    public async Task AnnouncesTheStoredForecastOnTheSupplyPointsSession()
    {
        await _handling.DeliverAsync(ForecastHandling.Recorded());

        var (message, metadata) = _handling.Publisher.Published.Single();
        var announced = _handling.Announced.Single();
        metadata.MessageId.Should().Be("10001234567:1");
        metadata.SessionId.Should().Be("10001234567");
        metadata.CorrelationId.Should().Be("receipt-1");
        announced.EventId.Should().Be(Guid.Parse("fc3e4d84-7b3a-5b45-76b3-b393e2bdef4e"));
        announced.AnchorDate.Should().Be(new DateOnly(2026, 5, 1));
        announced.Daily[0].Kwh.Should().Be(11.037274915196553439267315840m);
        announced.TriggerLedgerEntryId.Should().Be("hash:10001234567:2026-04-30:1");
        message.Should().BeSameAs(announced);
    }

    [Fact]
    public async Task StoresBeforeItAnnounces()
    {
        await _handling.DeliverAsync(ForecastHandling.Recorded());

        _handling.Log.Calls.Should().Equal("save", "publish");
    }

    [Fact]
    public async Task RecordsTheInputsItWasMadeFrom()
    {
        await _handling.DeliverAsync(ForecastHandling.Recorded());

        using var inputs = JsonDocument.Parse(_handling.Store.Versions.Single().Inputs);
        var root = inputs.RootElement;
        root.GetProperty("configurationDay").GetString().Should().Be("2026-05-01");
        root.GetProperty("configuration").GetProperty("meterConfigurationCode").GetString().Should().Be("MCC01");
        root.GetProperty("tariff").GetProperty("tariffSource").GetString().Should().Be("v1-reader");
        root.GetProperty("pufWindow").GetProperty("baseline").GetString().Should().Be("2026-04-21");
        root.GetProperty("ledgerDays").GetArrayLength().Should().Be(10);
        root.GetProperty("factors").GetProperty("numerators")[0].GetDecimal().Should().Be(108m);
        root.GetProperty("factors").GetProperty("source").GetString().Should().Be("Puf");
        root.GetProperty("profile").GetProperty("layout").GetString().Should().Be("SingleRate");
    }
}
