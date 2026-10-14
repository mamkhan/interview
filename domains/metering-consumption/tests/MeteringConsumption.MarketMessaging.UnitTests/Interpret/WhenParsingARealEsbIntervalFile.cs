using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;
using System.Xml.Linq;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// A real production MM343: the first two MPRN blocks of DSO_MIM_343_1782352213415_3805737.xml with the
// file's own header, so every value below came out of ESB rather than out of a fixture writer. The parser
// binds by type and property name, so this is also the test that fails if a Structure type is renamed.
//
// Expected values read offline with a stdlib XML reader, sharing no code with the parser under test:
//   MPRN 10900000343: 48 periods, IntervalValue[0] = 0.14, raw day sum 17.441
//   MPRN 10900001343: 48 periods, IntervalValue[0] = 0.069, raw day sum 6.373
public class WhenParsingARealEsbIntervalFile
{
    private const string SamplePath = "samples/DSO_MIM_343_1782352213415_3805737.extract.xml";

    private readonly Message<MIM343_SMARTMeteringIntervalData> _parsed;

    public WhenParsingARealEsbIntervalFile()
    {
        using var file = File.OpenRead(SamplePath);
        _parsed = ESBMessageParser.ParseTree<Message<MIM343_SMARTMeteringIntervalData>>(XDocument.Load(file).Root!);
    }

    private IReadOnlyList<MPRNLevelInfo> MPRNs => _parsed.MessageBody!.MPRNLevelInfos!;

    private static IReadOnlyList<IntervalInfo> IntervalsOf(MPRNLevelInfo mprn) =>
        mprn.MeterIDs![0].ChannelInfos![0].IntervalInfos!;

    [Fact]
    public void ReadsEverySupplyPointInTheFile() =>
        MPRNs.Select(m => m.MPRN).Should().Equal("10900000343", "10900001343");

    [Fact]
    public void CarriesTheOperatorsOwnIdentifiers()
    {
        var mprn = MPRNs[0];

        mprn.MeterIDs![0].SerialNumber.Should().Be("90003431");
        mprn.ReadDate.Should().Be(new DateOnly(2026, 6, 24));
        mprn.ReadingReplacementVersionNumber.Should().Be(1);
    }

    [Fact]
    public void ReadsTheChannelAsHalfHourlyKilowatts()
    {
        var channel = MPRNs[0].MeterIDs![0].ChannelInfos![0];

        channel.MeteringInterval.Should().Be(30);

        // KWT, not KWH: the operator states average power over the interval, so a consumer that wants
        // energy multiplies by the interval. MM343Strategy does that conversion.
        channel.UOM_Code.Should().Be("KWT");
    }

    [Fact]
    public void ReadsAFullDayAsFortyEightIntervals() =>
        MPRNs.Should().AllSatisfy(mprn => IntervalsOf(mprn).Should().HaveCount(48));

    [Fact]
    public void CarriesIntervalValuesVerbatim()
    {
        IntervalsOf(MPRNs[0])[0].IntervalValue.Should().Be(0.14m);
        IntervalsOf(MPRNs[1])[0].IntervalValue.Should().Be(0.069m);
    }

    [Fact]
    public void MatchesTheOfflineDayTotals()
    {
        IntervalsOf(MPRNs[0]).Sum(i => i.IntervalValue!.Value).Should().Be(17.441m);
        IntervalsOf(MPRNs[1]).Sum(i => i.IntervalValue!.Value).Should().Be(6.373m);
    }

    [Fact]
    public void ReadsTheMarketTimestampFromTheMessageHeaderAsUtc()
    {
        var marketTimestamp = _parsed.MessageHeader!.MarketTimestamp!.Value;

        marketTimestamp.Should().Be(new DateTimeOffset(2026, 6, 25, 2, 50, 10, 918, TimeSpan.Zero));
        marketTimestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void CarriesOperatorStatusCodesVerbatim() =>
        IntervalsOf(MPRNs[0])[0].IntervalStatusCode.Should().Be("VVAK");
}
