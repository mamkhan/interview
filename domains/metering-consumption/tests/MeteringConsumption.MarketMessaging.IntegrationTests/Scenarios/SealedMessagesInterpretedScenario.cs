using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Extensions;
using MeteringConsumption.MarketMessaging.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.MarketMessaging.IntegrationTests.Scenarios;

// The Interpret role alone, booted through the module's own registration on this class's scope: the real MM343 extract
// and the builder's MM332 are sealed in the archive and announced on the bus, and every topic the two files publish to
// is read back. Between them the files reach every topic Market Messaging publishes on.
public sealed class SealedMessagesInterpretedScenario : IAsyncLifetime
{
    public const string ExtractReceiptId = "9aeb0fdf-c01e-0131-0922-9eb54906e209";
    public const string ConfigurationReceiptId = "7c6b5a49-3827-4165-a0f9-e8d7c6b5a493";
    public const string ConfigurationMprn = "10012345678";

    private const string ExtractHash = "e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304";
    private const string ConfigurationHash = "5b0e3f1c2d4a6b8c9e0f1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f7";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] ConfigurationTopics =
    [
        MeteringConsumptionTopology.Topics.MeterConfigurationReceived,
        MeteringConsumptionTopology.Topics.UsageFactorsReceived,
        MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived,
    ];

    private SuiteScope? _scope;
    private IHost? _host;
    private ServiceBusClient? _bus;

    public Dictionary<string, MarketMessageProcessed> Outcomes { get; } = [];

    public Dictionary<string, string> OutcomeMessageIds { get; } = [];

    public List<(IntervalReadingReceived Reading, ServiceBusReceivedMessage Message)> Readings { get; } = [];

    // The one message the MM332 put on each configuration topic, by topic.
    public Dictionary<string, ServiceBusReceivedMessage> Configuration { get; } = [];

    public T Body<T>(string topic) => Configuration[topic].Body.ToObjectFromJson<T>(Json)!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenSealedMessagesAreInterpreted), new SuiteResources
        {
            Topics =
            [
                new BusEntity(MarketMessagingTopology.Topics.MarketMessageReceived, MarketMessagingTopology.Subscriptions.InterpretMarketMessageReceived),
                new BusEntity(MarketMessagingTopology.Topics.MarketMessageProcessed, Suite.TestReader),
                new BusEntity(MeteringConsumptionTopology.Topics.IntervalReadingReceived, Suite.TestReader, RequiresSession: true),
                .. ConfigurationTopics.Select(topic => new BusEntity(topic, Suite.TestReader, RequiresSession: true)),
            ],
            BlobContainers = [MarketMessagingTopology.Containers.MarketMessageArchive],
        }, ct);

        var blobs = new BlobServiceClient(scope.Endpoints.AzuriteConnectionString,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03));
        _bus = new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{EntityNamingOptions.Section}:Prefix"] = $"{scope.Id}-",
        });
        builder.Services.TryAddTimeProvider();
        builder.Services.AddSingleton(_bus);
        builder.Services.AddSingleton(blobs);
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        builder.Services.AddInterpretApplication();
        builder.Services.AddInterpretInfrastructure(builder.Configuration);
        _host = builder.Build();
        await _host.StartAsync(ct);

        var archive = blobs.GetBlobContainerClient(scope.BlobContainer(MarketMessagingTopology.Containers.MarketMessageArchive));
        await SealAndAnnounceAsync(archive, ExtractReceiptId, ExtractHash, "DSO_MIM_343_1782352213415_3805737.xml",
            await File.ReadAllBytesAsync("samples/DSO_MIM_343_1782352213415_3805737.extract.xml", ct), ct);
        await SealAndAnnounceAsync(archive, ConfigurationReceiptId, ConfigurationHash, "DSO_MIM_332_1782352213415_3805739.xml",
            new MarketMessageXmlBuilder().WithMessageTypeCode("332").WithMprn(ConfigurationMprn).BuildBytes(), ct);

        await using var outcomes = _bus.CreateScopedReceiver(scope, MarketMessagingTopology.Topics.MarketMessageProcessed, Suite.TestReader);
        while (Outcomes.Count < 2)
        {
            var message = await outcomes.ReceiveMessageAsync(TimeSpan.FromSeconds(60), ct)
                ?? throw new InvalidOperationException($"Only {Outcomes.Count} of 2 outcomes were published.");
            var outcome = message.Body.ToObjectFromJson<MarketMessageProcessed>(Json)!;
            Outcomes[outcome.ReceiptId] = outcome;
            OutcomeMessageIds[outcome.ReceiptId] = message.MessageId;
            await outcomes.CompleteMessageAsync(message, ct);
        }

        for (var i = 0; i < 2; i++)
        {
            var message = await ReceiveOnTheNextSessionAsync(MeteringConsumptionTopology.Topics.IntervalReadingReceived, ct);
            Readings.Add((message.Body.ToObjectFromJson<IntervalReadingReceived>(Json)!, message));
        }

        foreach (var topic in ConfigurationTopics)
        {
            Configuration[topic] = await ReceiveOnTheNextSessionAsync(topic, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_bus is not null)
        {
            await _bus.DisposeAsync();
        }

        if (_scope is not null)
        {
            await _scope.DisposeAsync();
        }
    }

    private async Task SealAndAnnounceAsync(BlobContainerClient archive, string receiptId, string hash, string fileName,
        byte[] payload, CancellationToken ct)
    {
        var blobPath = $"roi-esb/{hash}.xml";
        await archive.UploadBlobAsync(blobPath, new BinaryData(payload), ct);
        await _host!.Services.GetRequiredService<IEventPublisher>().PublishAsync(new MarketMessageReceived
        {
            EventId = Guid.Parse(receiptId),
            PublishingContext = "metering-consumption.market-messaging",
            OccurredAtUtc = new DateTimeOffset(2026, 9, 16, 10, 0, 3, TimeSpan.Zero),
            CorrelationId = receiptId,
            ReceiptId = receiptId,
            Market = MeteringConsumptionTopology.Markets.RoiEsb,
            Fuel = MarketMessagingTopology.Fuels.Electricity,
            FileName = fileName,
            ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
            Hash = hash,
            BlobPath = blobPath,
        }, new EventMetadata { MessageId = receiptId, CorrelationId = receiptId }, ct);
    }

    private async Task<ServiceBusReceivedMessage> ReceiveOnTheNextSessionAsync(string topic, CancellationToken ct)
    {
        await using var session = await _bus!.AcceptNextSessionAsync(_scope!.Topic(topic), Suite.TestReader, cancellationToken: ct);
        var message = await session.ReceiveMessageAsync(TimeSpan.FromSeconds(30), ct)
            ?? throw new InvalidOperationException($"Nothing was published on {topic}.");
        await session.CompleteMessageAsync(message, ct);
        return message;
    }
}
