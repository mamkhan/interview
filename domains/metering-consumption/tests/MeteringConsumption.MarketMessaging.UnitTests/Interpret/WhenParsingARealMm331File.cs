using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// A real ESB smart meter exchange, ESB_MM331_10900001331 from the rail's esb-inbound container, its identifiers replaced by
// fake ones and every other value as ESB sent it, bound onto the schema the way the parser binds every file. Expected
// values read offline with Python's xml.etree: MPRN 10900001331, DUoS DG1, status E, MCC12, effective 2026-09-17; new
// meter 90013311 (RM107, location 07) with channels 70 KWT every 30 and 74 KWH every 24; removed meter 90013312 (RM106).
public class WhenParsingARealMm331File
{
    private readonly Message<MIM331_IntervalMeterTechnicalDetails> _parsed;

    public WhenParsingARealMm331File()
    {
        using var payload = File.OpenRead("samples/ESB_MM331_10900001331_0000000790001331.xml");
        _parsed = ESBMessageParser.ParseDocument<Message<MIM331_IntervalMeterTechnicalDetails>>(payload);
    }

    private MIM331_IntervalMeterTechnicalDetails Body => _parsed.MessageBody!;

    [Fact]
    public void ReadsTheMeterPoint()
    {
        Body.MPRN.Should().Be("10900001331");
        Body.DUOS_Group.Should().Be("DG1");
        Body.MeterPointStatusCode.Should().Be("E");
        Body.MeterConfigurationCode.Should().Be("MCC12");
        Body.EffectiveFromDate.Should().Be(new DateOnly(2026, 9, 17));
        Body.NetworksReferenceNumber.Should().BeNull();
    }

    [Fact]
    public void ReadsTheNewMeterWithItsChannels()
    {
        var meter = Body.NewMeterRegisters.Should().ContainSingle().Subject;

        meter.SerialNumber.Should().Be("90013311");
        meter.MeterCategoryCode.Should().Be("RM107");
        meter.MeterLocationCode.Should().Be("07");
        meter.ExchangedMeterReference.Should().Be("000000000090013311");
        meter.RegisterChannelInformations!.Select(channel => (channel.RegisterTypeCode, channel.UOM_Code, channel.MeteringInterval))
            .Should().Equal(("70", "KWT", 30), ("74", "KWH", 24));
        meter.RegisterLevelInfos.Should().BeEmpty();
    }

    [Fact]
    public void ReadsTheRemovedMeter()
    {
        var meter = Body.RemovedMeterRegisters.Should().ContainSingle().Subject;

        meter.SerialNumber.Should().Be("90013312");
        meter.MeterCategoryCode.Should().Be("RM106");
        meter.ExchangedMeterReference.Should().Be("000000000090013312");
    }
}
