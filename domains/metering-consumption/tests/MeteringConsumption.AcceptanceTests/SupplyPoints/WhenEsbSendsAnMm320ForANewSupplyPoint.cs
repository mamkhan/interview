using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.SupplyPoints.Scenarios;

namespace MeteringConsumption.AcceptanceTests.SupplyPoints;

// ESB sends an MM320, a change of supplier, for a supply point SupplyPoints has never seen, through the running
// environment's drop container, and SupplyPoints records the meter in place and its registers with their reads. The
// values are MarketMessageXmlBuilder's MM320: read date 2026-06-24, meter 32300001 (category RM106, location 01),
// register 1 (00D, read 12345) and register 2 (00N, read 6789), both type 01 in KWH with multiplier 1.
public sealed class WhenEsbSendsAnMm320ForANewSupplyPoint(EsbSentAnMm320ForANewSupplyPointScenario sent)
    : IClassFixture<EsbSentAnMm320ForANewSupplyPointScenario>
{
    private static readonly DateTime ReadDate = new(2026, 6, 24);

    [Fact]
    public void PutsTheMeterInPlaceFromTheChangeOfSupplierReadDate()
    {
        var meter = sent.Meters.Should().ContainSingle().Subject;

        meter.SerialNumber.Should().Be("32300001");
        meter.EffectiveDate.Should().Be(ReadDate);
        meter.MeterCategoryCode.Should().Be("RM106");
        meter.MeterLocationCode.Should().Be("01");
        sent.MeterHistory.Should().Equal(meter);
    }

    [Fact]
    public void KeepsEachRegisterWithItsChangeOfSupplierRead() =>
        sent.Registers.Select(register => (register.RegisterKey, register.TimeslotCode, register.EffectiveDate, register.ReadingValue))
            .Should().Equal(
                ("1", "00D", ReadDate, (decimal?)12345m),
                ("2", "00N", ReadDate, 6789m));
}
