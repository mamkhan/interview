using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Reload;
using MeteringConsumption.SupplyPoints.Application.Features.Store;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Reload;

// The real MM320 sample stored 10900000320 first: MCC16, 25, DG1, E from 2026-09-30 with NRN 019000000320, estimates
// for 01D, 01N and 01P, and meter 90000320 with registers 001 to 003. Then V1's load for the same supply point states
// MCC01, 01, DG1 from 2026-08-03 and only the 24H factors: the message's codes and usage factors go from history and
// current alike, its meter and registers stay.
public class WhenASupplyPointStoredFromAMessageIsLoadedFromV1
{
    private readonly ReloadRig _rig = new();

    public WhenASupplyPointStoredFromAMessageIsLoadedFromV1()
    {
        _rig.Handler<MeterConfigurationChangeReceivedEventHandler>()
            .HandleAsync(new MeterConfigurationChangeReceivedBuilder().AsTheRealMm320States().Build(), CancellationToken.None)
            .GetAwaiter().GetResult();
        _rig.Handler<SupplyPointLoadedFromV1EventHandler>()
            .HandleAsync(new SupplyPointLoadedFromV1Builder().ForMprn("10900000320").Build(), CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    [Fact]
    public void ReplacesTheMessagesCodesWithV1s()
    {
        var loaded = new Dated<MeterPointValues>(new DateOnly(2026, 8, 3), new MeterPointValues("MCC01", "01", "DG1", null, null));

        _rig.Repository.MeterPoint("10900000320").History.Should().Equal(loaded);
        _rig.Repository.MeterPoint("10900000320").Current.Should().Equal(loaded);
    }

    [Fact]
    public void RemovesTheUsageFactorsV1DoesNotState()
    {
        foreach (var timeslot in new[] { "01D", "01N", "01P" })
        {
            _rig.Repository.UsageFactor("10900000320", timeslot).History.Should().BeEmpty();
            _rig.Repository.UsageFactor("10900000320", timeslot).Current.Should().BeEmpty();
        }

        _rig.Repository.UsageFactor("10900000320", "24H").History.Should().HaveCount(2);
    }

    [Fact]
    public void KeepsTheMeterAndItsRegisters()
    {
        _rig.Repository.Meter("10900000320", "90000320").Current.Should().ContainSingle()
            .Which.EffectiveDate.Should().Be(new DateOnly(2026, 9, 30));
        _rig.Repository.Register("10900000320", "90000320", "001").Current.Should().ContainSingle();
    }
}
