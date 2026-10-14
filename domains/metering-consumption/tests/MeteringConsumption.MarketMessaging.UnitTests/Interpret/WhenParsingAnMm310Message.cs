using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The builder's MM310 bound onto the schema the way the parser binds the real files: the meter point, its usage
// factors and its one meter's registers. Frozen literals from MarketMessageXmlBuilder: MPRN 10900000343, read date
// 2026-06-24, DAY 2.5000 and NIGHT 1.2000 actual, meter 32300001 with registers 12345 and 6789 read since
// 2026-04-24.
public class WhenParsingAnMm310Message
{
    private const string Mprn = "10900000343";

    private readonly Message<MIM310_ValidatedCoSReading> _parsed;

    public WhenParsingAnMm310Message()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("310").WithMprn(Mprn).BuildStream();
        _parsed = ESBMessageParser.ParseDocument<Message<MIM310_ValidatedCoSReading>>(payload);
    }

    private MIM310_ValidatedCoSReading Body => _parsed.MessageBody!;

    [Fact]
    public void ReadsTheMeterPoint()
    {
        Body.MPRN.Should().Be(Mprn);
        Body.NetworksReferenceNumber.Should().Be(MarketMessageXmlBuilder.NetworksReferenceNumber);
        Body.LoadProfileCode.Should().Be("01");
        Body.DUOS_Group.Should().Be("DG1");
        Body.MeterPointStatusCode.Should().Be("1");
        Body.ReadDate.Should().Be(new DateOnly(2026, 6, 24));
    }

    [Fact]
    public void ReadsEveryUsageFactor() =>
        Body.UsageFactors!.Select(f => (f.TimeslotCode, f.ActualUsageFactor)).Should().Equal(("00D", 2.5000m), ("00N", 1.2000m));

    [Fact]
    public void ReadsEachRegisterOfTheMeter()
    {
        var meter = Body.MeterIDs!.Should().ContainSingle().Subject;

        meter.SerialNumber.Should().Be("32300001");
        meter.RegisterLevelInfos!.Select(r => (r.TimeslotCode, r.ReadingValue)).Should().Equal(("00D", 12345m), ("00N", 6789m));
        meter.RegisterLevelInfos![0].PreviousReadDate.Should().Be(new DateOnly(2026, 4, 24));
        meter.RegisterLevelInfos![0].Consumption.Should().Be(812m);
        meter.RegisterLevelInfos![1].Consumption.Should().BeNull();
    }
}

// UsageFactors is 0..n on an MM310: a file without them parses to an empty list, not a missing one.
public class WhenParsingAnMm310MessageWithoutUsageFactors
{
    [Fact]
    public void GivesAnEmptyListOfUsageFactors()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("310").WithoutUsageFactors().BuildStream();

        ESBMessageParser.ParseDocument<Message<MIM310_ValidatedCoSReading>>(payload).MessageBody!.UsageFactors.Should().BeEmpty();
    }
}
