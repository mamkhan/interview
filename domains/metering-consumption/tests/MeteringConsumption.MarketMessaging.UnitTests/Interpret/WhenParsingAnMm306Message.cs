using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The builder's MM306 bound onto the schema the way the parser binds the real files: the de-energised meter point,
// its usage factors and its one meter's closing registers. Frozen literals from MarketMessageXmlBuilder: MPRN
// 10900000343, status DR, effective 2026-06-24, essential plant 0, DAY 2.4100 and NIGHT 1.1300, meter 32300001 with
// registers 12345 and 6789 read since 2026-04-24.
public class WhenParsingAnMm306Message
{
    private const string Mprn = "10900000343";

    private readonly Message<MIM306_MeterPointStatusChangeDeEnergisationRead> _parsed;

    public WhenParsingAnMm306Message()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("306").WithMprn(Mprn).BuildStream();
        _parsed = ESBMessageParser.ParseDocument<Message<MIM306_MeterPointStatusChangeDeEnergisationRead>>(payload);
    }

    private MIM306_MeterPointStatusChangeDeEnergisationRead Body => _parsed.MessageBody!;

    [Fact]
    public void ReadsTheMeterPoint()
    {
        Body.MPRN.Should().Be(Mprn);
        Body.NetworksReferenceNumber.Should().Be(MarketMessageXmlBuilder.NetworksReferenceNumber);
        Body.MeterPointStatusCode.Should().Be("DR");
        Body.LoadProfileCode.Should().Be("01");
        Body.DUOS_Group.Should().Be("DG1");
        Body.EffectiveFromDate.Should().Be(new DateOnly(2026, 6, 24));
        Body.EssentialPlantFlag.Should().BeFalse();
        Body.MPBusinessReference.Should().BeNull();
    }

    [Fact]
    public void ReadsEveryUsageFactor() =>
        Body.UsageFactors!.Select(f => (f.TimeslotCode, f.EstimatedUsageFactor)).Should().Equal(("00D", 2.4100m), ("00N", 1.1300m));

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

// UsageFactors is 0..n on an MM306: a file without them parses to an empty list, not a missing one.
public class WhenParsingAnMm306MessageWithoutUsageFactors
{
    [Fact]
    public void GivesAnEmptyListOfUsageFactors()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("306").WithoutUsageFactors().BuildStream();

        ESBMessageParser.ParseDocument<Message<MIM306_MeterPointStatusChangeDeEnergisationRead>>(payload).MessageBody!.UsageFactors.Should().BeEmpty();
    }
}
