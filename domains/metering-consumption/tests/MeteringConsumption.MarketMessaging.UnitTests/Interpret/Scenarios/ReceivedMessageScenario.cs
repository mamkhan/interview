using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;
using Microsoft.Extensions.Logging.Abstractions;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret.Scenarios;

// The received-message handler wired to fakes, shared by the Interpret scenarios; each scenario changes only what it asserts.
internal sealed class ReceivedMessageScenario
{
    public const string ReceiptId = "9aeb0fdf-c01e-0131-0922-9eb54906e209";
    public const string Hash = "5bb7202821a6c2e2e6ea6baec50e0e8408c6670fc94cba9c838a888bed67c724";

    public static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 5, TimeSpan.Zero);

    public CallLog Log { get; } = new();

    public FakeArchiveReader Archive { get; }

    public RecordingInterpreter Mm343 { get; }

    public RecordingOutcomePublisher Publisher { get; }

    public IMessageClassifier Classifier { get; set; } = new StubClassifier("343");

    public ReceivedMessageScenario()
    {
        Archive = new FakeArchiveReader(Log, () => new MarketMessageXmlBuilder().WithMprn("10900000343").BuildStream());
        Mm343 = new RecordingInterpreter(Log, MeteringConsumptionTopology.Markets.RoiEsb, "343", readings: 2);
        Publisher = new RecordingOutcomePublisher(Log);
    }

    public InterpretMarketMessageReceivedEventHandler Handler() => new(
        Archive,
        new MessageClassifiers { [MeteringConsumptionTopology.Markets.RoiEsb] = Classifier },
        new MessageStrategy([Mm343]),
        Publisher,
        new FixedTimeProvider(Now),
        NullLogger<InterpretMarketMessageReceivedEventHandler>.Instance);

    public static MarketMessageReceived Receipt(string receiptId = ReceiptId) => new()
    {
        EventId = Guid.Parse(receiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 16, 10, 0, 3, TimeSpan.Zero),
        CorrelationId = receiptId,
        ReceiptId = receiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = "DSO_MIM_343_1782352213415_3805737.xml",
        ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        Hash = Hash,
        BlobPath = $"roi-esb/{Hash}.xml",
    };

    public MarketMessageProcessed Processed => (MarketMessageProcessed)Publisher.Published.Single().Event;

    public MarketMessageFailed Failed => (MarketMessageFailed)Publisher.Published.Single().Event;
}
