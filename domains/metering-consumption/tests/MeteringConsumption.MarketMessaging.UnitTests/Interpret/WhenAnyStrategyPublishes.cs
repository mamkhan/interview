using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Extensions;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;
using MeteringConsumption.MarketMessaging.Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// What every strategy publishes the same way, whatever its type: each event on its supply point's session under its own
// reading id, carrying the receipt as correlation, traced to the sealed file and the market timestamp of the file's
// header, with an event id of its own, and the smart meter flag left for V1 to state. Asserted once here over each
// type's builder file rather than in every type's class; what differs by type stays there. The receipt is this class's
// own; the header's MarketTimestamp 2026-06-25T02:50:10.918Z and read date 2026-06-24 (RRVN 1) are the builder's.
public class WhenAnyStrategyPublishes
{
    private const string Mprn = "10001234567";
    private const string Hash = "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";
    private const string ReceiptId = "4c2b1a09-8f7e-4d6c-9b5a-0e1f2d3c4b5a";

    private static readonly DateTimeOffset HeaderTimestamp = new(2026, 6, 25, 2, 50, 10, 918, TimeSpan.Zero);

    public static readonly TheoryData<string> EveryType =
        ["101", "105", "300", "300S", "301", "306", "307", "310", "320", "331", "332", "343"];

    // The types whose file states a meter configuration, which V1, not the market, says is smart.
    public static readonly TheoryData<string> EveryTypeWithAMeterConfiguration =
        ["101", "105", "300", "300S", "301", "306", "307", "310", "320", "332"];

    [Theory]
    [InlineData("101", new[] { "2026-06-24", "meter-configuration-change" })]
    [InlineData("105", new[] { "2026-06-24", "meter-configuration-change" })]
    [InlineData("300", new[] { "2026-06-24", "2026-06-24", "meter-configuration-change" })]
    [InlineData("300S", new[] { "2026-06-24", "2026-06-24", "meter-configuration-change" })]
    [InlineData("301", new[] { "2026-06-24", "meter-configuration-change" })]
    [InlineData("306", new[] { "2026-06-24", "2026-06-24", "meter-configuration-change" })]
    [InlineData("307", new[] { "2026-06-24", "2026-06-24", "meter-configuration-change" })]
    [InlineData("310", new[] { "2026-06-24", "2026-06-24", "meter-configuration-change" })]
    [InlineData("320", new[] { "2026-06-24", "2026-06-24", "meter-configuration-change" })]
    [InlineData("331", new[] { "meter-configuration-change" })]
    [InlineData("332", new[] { "2026-06-24", "2026-06-24", "meter-configuration-change" })]
    [InlineData("343", new[] { "2026-06-24:1" })]
    public async Task KeysEveryEventByTheMprnSessionAndItsReadingId(string messageTypeCode, string[] readingIds)
    {
        var published = await InterpretAsync(messageTypeCode);

        published.Should().AllSatisfy(p => p.Metadata.SessionId.Should().Be(Mprn));
        published.Select(p => p.Metadata.MessageId).Should().Equal(readingIds.Select(id => $"{Hash}:{Mprn}:{id}"));
        published.Should().AllSatisfy(p => p.Metadata.MessageId.Should().Be(p.Event.ReadingId));
    }

    [Theory]
    [MemberData(nameof(EveryType))]
    public async Task CopiesTheCorrelationIdFromTheReceipt(string messageTypeCode) =>
        (await InterpretAsync(messageTypeCode)).Should().AllSatisfy(p =>
        {
            p.Metadata.CorrelationId.Should().Be(ReceiptId);
            p.Event.CorrelationId.Should().Be(ReceiptId);
        });

    [Theory]
    [MemberData(nameof(EveryType))]
    public async Task TracesEveryEventToTheSealedFileAndTheMarketTimestampOfItsHeader(string messageTypeCode) =>
        (await InterpretAsync(messageTypeCode)).Select(p => p.Event.Provenance).Should().AllBeEquivalentTo(new ReadingProvenance(
            MeteringConsumptionTopology.Markets.RoiEsb, messageTypeCode, HeaderTimestamp, $"roi-esb/{Hash}.xml", Hash,
            FileNameFor(messageTypeCode)));

    [Theory]
    [MemberData(nameof(EveryType))]
    public async Task GivesEveryEventADistinctEventId(string messageTypeCode) =>
        (await InterpretAsync(messageTypeCode)).Select(p => ((EventEnvelope)p.Event).EventId).Should().OnlyHaveUniqueItems();

    [Theory]
    [MemberData(nameof(EveryTypeWithAMeterConfiguration))]
    public async Task LeavesTheSmartMeterFlagUnstated(string messageTypeCode) =>
        (await InterpretAsync(messageTypeCode)).Select(p => p.Event).OfType<MeterConfigurationReceived>()
            .Should().ContainSingle().Which.IsNsmp.Should().BeNull();

    private static string FileNameFor(string messageTypeCode) => $"DSO_MIM_{messageTypeCode}_1782352213415_3805750.xml";

    // The strategy as the Interpret role registers it, publishing to a recorder; the clock and the client are what a
    // host adds before the role, and the client never connects.
    private static async Task<IReadOnlyList<(IEvent Event, EventMetadata Metadata)>> InterpretAsync(string messageTypeCode)
    {
        var recorder = new Recorder();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(new ServiceBusClient(
            "Endpoint=sb://localhost;SharedAccessKeyName=unused;SharedAccessKey=unused;UseDevelopmentEmulator=true;"));
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        builder.Services.AddInterpretApplication();
        builder.Services.AddInterpretInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<IEventPublisher>(recorder);
        await using var services = builder.Services.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();

        var strategy = scope.ServiceProvider.GetRequiredService<MessageStrategy>().Find(MeteringConsumptionTopology.Markets.RoiEsb, messageTypeCode)!;
        await using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode(messageTypeCode).WithMprn(Mprn).BuildStream();
        await strategy.InterpretAsync(new MarketMessageReceived
        {
            EventId = Guid.Parse(ReceiptId),
            PublishingContext = "metering-consumption.market-messaging",
            OccurredAtUtc = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
            CorrelationId = ReceiptId,
            ReceiptId = ReceiptId,
            Market = MeteringConsumptionTopology.Markets.RoiEsb,
            Fuel = MarketMessagingTopology.Fuels.Electricity,
            FileName = FileNameFor(messageTypeCode),
            ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
            Hash = Hash,
            BlobPath = $"roi-esb/{Hash}.xml",
        }, payload, TestContext.Current.CancellationToken);

        return recorder.Published;
    }

    private sealed class Recorder : IEventPublisher
    {
        public List<(IEvent Event, EventMetadata Metadata)> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
            where TEvent : class => PublishAsync(message, typeof(TEvent), metadata, cancellationToken);

        public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken)
        {
            Published.Add(((IEvent)message, metadata));
            return Task.CompletedTask;
        }

        public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
