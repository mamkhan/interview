using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// ESB states an MM343 interval as KWT, the average power over the interval in kW, so its energy is the value
// times the interval in hours. The real extract's first half-hour, 0.14 KWT over 30 minutes, is 0.070 kWh, and
// its day of 17.441 KWT is 8.7205 kWh: the frozen totals of the proof of concept and the acceptance suite.
public class WhenConvertingAnIntervalValueToEnergy
{
    [Fact]
    public void HalvesAHalfHoursAveragePower() =>
        IntervalEnergy.ToKwh(0.14m, "KWT", 30).Should().Be(0.070m);

    [Fact]
    public void QuartersAQuarterHoursAveragePower() =>
        IntervalEnergy.ToKwh(1.2m, "KWT", 15).Should().Be(0.3m);

    [Fact]
    public void RejectsAUnitItDoesNotKnow()
    {
        var converting = () => IntervalEnergy.ToKwh(0.14m, "KVARH", 30);

        converting.Should().Throw<LedgerEntryRejectedException>();
    }

    [Fact]
    public void RejectsAnIntervalOfNoLength()
    {
        var converting = () => IntervalEnergy.ToKwh(0.14m, "KWT", 0);

        converting.Should().Throw<LedgerEntryRejectedException>();
    }
}
