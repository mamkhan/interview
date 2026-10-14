using AwesomeAssertions;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM331 interpreter over a real ESB smart meter put in place, ESB_MM331_10900000331 from the rail's esb-inbound
// container, its identifiers replaced by fake ones and every other value as ESB sent it. Expected values read offline with
// Python's xml.etree, sharing no code with the parser: MCC12, DUoS DG1, status E, effective 2026-09-16, meter 90003311
// (RM106, location 07, exchanged reference 000000000090003311) with channels 70 KWT every 30 and 74 KWH every 24, header
// MarketTimestamp 2026-09-17T14:11:42.057Z; sha256 of the file da5f00ba23fd17a7db68526b199868583c4dcb6835a12ece9562a2c8c556ec4b.
public class WhenInterpretingARealMm331File
{
    private const string FileName = "ESB_MM331_10900000331_0000000790000331.xml";
    private const string Mprn = "10900000331";
    private const string Hash = "da5f00ba23fd17a7db68526b199868583c4dcb6835a12ece9562a2c8c556ec4b";
    private const string ReceiptId = "d3c2b1a0-9f8e-4d7c-8b6a-5f4e3d2c1b0a";

    private static readonly DateOnly EffectiveFrom = new(2026, 9, 16);

    private readonly Mm332Scenario.RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm331File()
    {
        var interpreter = new MM331Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 9, 17, 14, 12, 3, TimeSpan.Zero)),
            NullLogger<MM331Strategy>.Instance);
        using var payload = File.OpenRead($"samples/{FileName}");
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void SendsSupplyPointsTheWholeConfigurationAsOneEvent() =>
        _publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Should().BeEquivalentTo(
            new MeterConfigurationChangeReceivedBuilder().AsTheRealMm331States().Build(), options => options
                .ComparingRecordsByMembers()
                .Excluding(change => change.EventId)
                .Excluding(change => change.OccurredAtUtc)
                .Excluding(change => change.CorrelationId));

    [Fact]
    public void ReturnsTheChangeAsPublished() => _outcome.ReadingsPublished.Should().Be(1);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 17, 14, 12, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 9, 17, 14, 12, 0, TimeSpan.Zero),
        Hash = Hash,
        BlobPath = $"roi-esb/{Hash}.xml",
    };
}
