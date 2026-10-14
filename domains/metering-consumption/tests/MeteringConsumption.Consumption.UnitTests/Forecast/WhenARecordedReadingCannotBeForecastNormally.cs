using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Forecast;

// The persona with three read days, too few for its own use, and no area average for DG1 has no usable usage factor:
// V1's 62 zeros, stored and announced as such. With a source missing it gets no forecast at all.
public sealed class WhenARecordedReadingCannotBeForecastNormally
{
    private readonly ForecastHandling _handling = new();

    [Fact]
    public async Task StoresAndAnnouncesZerosMarkedAsHavingNoUsageFactor()
    {
        _handling.WithReadDays(3);
        _handling.ReferenceData.Averages.Clear();

        await _handling.DeliverAsync(ForecastHandling.Recorded());

        var announced = _handling.Announced.Single();
        announced.UsageFactorSource.Should().Be("None");
        announced.TotalKwh.Should().Be(0m);
        announced.Daily.Should().HaveCount(62).And.OnlyContain(day => day.Kwh == 0m);
    }

    [Fact]
    public async Task ForecastsNothingWhenSupplyPointsHasNoConfigurationInEffect()
    {
        _handling.Configurations.InEffect.Clear();

        await _handling.DeliverAsync(ForecastHandling.Recorded());

        _handling.Store.Versions.Should().BeEmpty();
        _handling.Publisher.Published.Should().BeEmpty();
        _handling.Logger.Entries.Should().ContainSingle(entry => entry.Message.Contains(nameof(NotForecastReason.NoConfigurationInEffect)));
    }

    [Fact]
    public async Task ForecastsNothingWhenTheProfileHasNoTariffValues()
    {
        _handling.Tariffs.Tariffs.Clear();

        await _handling.DeliverAsync(ForecastHandling.Recorded());

        _handling.Store.Versions.Should().BeEmpty();
        _handling.Publisher.Published.Should().BeEmpty();
        _handling.Logger.Entries.Should().ContainSingle(entry => entry.Message.Contains(nameof(NotForecastReason.NoTariffValues)));
    }

    [Fact]
    public async Task RefusesAReadingTheLedgerDoesNotHold()
    {
        _handling.Ledger.Days.Clear();

        var act = () => _handling.DeliverAsync(ForecastHandling.Recorded());

        await act.Should().ThrowAsync<ForecastInputRejectedException>();
    }
}
