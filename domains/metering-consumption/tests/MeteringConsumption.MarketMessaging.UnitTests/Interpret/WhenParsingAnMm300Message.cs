using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The builder's MM300 bound onto the schema the way the parser binds the real files: the meter point, its usage
// factors, its one meter's registers and the meter reader's remark. Frozen literals from MarketMessageXmlBuilder:
// MPRN 10900000343, read date 2026-06-24, DAY 2.4100 and NIGHT 1.1300, meter 32300001 with registers 12345 and 6789
// read since 2026-04-24, remark R07.
public class WhenParsingAnMm300Message
{
    private const string Mprn = "10900000343";

    private readonly Message<MIM300_ValidatedNonIntervalReadingsScheduled> _parsed;

    public WhenParsingAnMm300Message()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("300").WithMprn(Mprn).BuildStream();
        _parsed = ESBMessageParser.ParseDocument<Message<MIM300_ValidatedNonIntervalReadingsScheduled>>(payload);
    }

    private MIM300_ValidatedNonIntervalReadingsScheduled Body => _parsed.MessageBody!;

    [Fact]
    public void ReadsTheMeterPoint()
    {
        Body.MPRN.Should().Be(Mprn);
        Body.NetworksReferenceNumber.Should().Be(MarketMessageXmlBuilder.NetworksReferenceNumber);
        Body.LoadProfileCode.Should().Be("01");
        Body.DUOS_Group.Should().Be("DG1");
        Body.MeterPointStatusCode.Should().Be("1");
        Body.ReadDate.Should().Be(new DateOnly(2026, 6, 24));
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
        meter.RegisterLevelInfos.Should().AllSatisfy(r => r.ReadStatusCode.Should().Be("V"));
    }

    [Fact]
    public void ReadsTheMeterReadersRemarkIncludingItsText()
    {
        var remark = Body.MeterReaderRemarks!.Should().ContainSingle().Subject;

        remark.MeterReaderRemarkCode.Should().Be("R07");
        remark.RemarkText.Should().Be("Meter behind locked gate");
    }
}

// UsageFactors is 0..n on an MM300: a file without them parses to an empty list, not a missing one.
public class WhenParsingAnMm300MessageWithoutUsageFactors
{
    [Fact]
    public void GivesAnEmptyListOfUsageFactors()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("300").WithoutUsageFactors().BuildStream();

        ESBMessageParser.ParseDocument<Message<MIM300_ValidatedNonIntervalReadingsScheduled>>(payload).MessageBody!.UsageFactors.Should().BeEmpty();
    }
}
