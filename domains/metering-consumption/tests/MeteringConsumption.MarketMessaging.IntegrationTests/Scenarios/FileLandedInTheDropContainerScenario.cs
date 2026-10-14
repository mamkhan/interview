using MeteringConsumption.MarketMessaging.Application.Extensions;
using MeteringConsumption.MarketMessaging.Infrastructure.Extensions;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Contracts;
using MeteringConsumption.MarketMessaging.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.MarketMessaging.IntegrationTests.Scenarios;

public sealed class FileLandedInTheDropContainerScenario : IAsyncLifetime
{
    private const string EventId = "9aeb0fdf-c01e-0131-0922-9eb54906e209";
    private const string FileName = "DSO_MIM_343_1782352213415_3805737.xml";

    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private SuiteScope? _scope;
    private IHost? _host;
    private ServiceBusClient? _bus;

    public System.Text.Json.JsonSerializerOptions Json { get; } = new(System.Text.Json.JsonSerializerDefaults.Web);

    public ServiceBusReceivedMessage FirstReceipt { get; private set; } = null!;

    public ServiceBusReceivedMessage? SecondReceipt { get; private set; }

    public List<string> ArchiveBlobs { get; private set; } = [];

    public List<string> ArchiveBlobsAfterSecond { get; private set; } = [];

    public IDictionary<string, string> ArchiveMetadata { get; private set; } = new Dictionary<string, string>();

    public List<string> DroppedFilesAfterFirst { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenAFileLandsInTheDropContainer), new SuiteResources
        {
            Queues = [new QueueEntity(MarketMessagingTopology.Queues.ReceiveBlobCreated)],
            Topics = [new BusEntity(MarketMessagingTopology.Topics.MarketMessageReceived, Suite.TestReader)],
            BlobContainers = [MarketMessagingTopology.Containers.EsbDrop, MarketMessagingTopology.Containers.MarketMessageArchive],
        }, ct);

        var blobs = new BlobServiceClient(scope.Endpoints.AzuriteConnectionString,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03));
        _bus = new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString);
        var drop = blobs.GetBlobContainerClient(scope.BlobContainer(MarketMessagingTopology.Containers.EsbDrop));
        var archive = blobs.GetBlobContainerClient(scope.BlobContainer(MarketMessagingTopology.Containers.MarketMessageArchive));

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{EntityNamingOptions.Section}:Prefix"] = $"{scope.Id}-",
            [$"{DropOptions.Section}:Container"] = drop.Name,
            [$"{DropOptions.Section}:Market"] = MeteringConsumptionTopology.Markets.RoiEsb,
            [$"{DropOptions.Section}:Fuel"] = MarketMessagingTopology.Fuels.Electricity,
        });
        builder.Services.TryAddTimeProvider();
        builder.Services.AddSingleton(_bus);
        builder.Services.AddSingleton(blobs);
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        builder.Services.AddReceiveApplication();
        builder.Services.AddReceiveInfrastructure(builder.Configuration);
        _host = builder.Build();
        await _host.StartAsync(ct);

        var payload = new MarketMessageXmlBuilder().WithMprn("10900000343").BuildBytes();
        await using var sender = _bus.CreateScopedSender(scope, MarketMessagingTopology.Queues.ReceiveBlobCreated);
        await using var receiver = _bus.CreateScopedReceiver(scope, MarketMessagingTopology.Topics.MarketMessageReceived, Suite.TestReader);

        await drop.UploadBlobAsync(FileName, new BinaryData(payload), ct);
        await sender.SendMessageAsync(new ServiceBusMessage(Event(drop.Name)) { MessageId = EventId }, ct);
        FirstReceipt = (await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), ct))!;
        DroppedFilesAfterFirst = await WaitUntilEmptyAsync(drop, ct);
        ArchiveBlobs = await NamesAsync(archive, ct);
        ArchiveMetadata = (await archive.GetBlobClient(ArchiveBlobs.Single()).GetPropertiesAsync(cancellationToken: ct)).Value.Metadata;

        await drop.UploadBlobAsync(FileName, new BinaryData(payload), ct);
        await sender.SendMessageAsync(new ServiceBusMessage(Event(drop.Name)) { MessageId = EventId }, ct);
        SecondReceipt = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), ct);
        await WaitUntilEmptyAsync(drop, ct);
        ArchiveBlobsAfterSecond = await NamesAsync(archive, ct);
    }

    private static string Event(string container) =>
        new BlobCreatedEventBuilder().WithId(EventId).WithContainer(container).WithBlobName(FileName).WithTime(CreatedAt).BuildJson();

    private static async Task<List<string>> NamesAsync(BlobContainerClient container, CancellationToken ct)
    {
        var names = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(cancellationToken: ct))
        {
            names.Add(blob.Name);
        }

        return names;
    }

    private static async Task<List<string>> WaitUntilEmptyAsync(BlobContainerClient container, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        var names = await NamesAsync(container, ct);
        while (names.Count > 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(200, ct);
            names = await NamesAsync(container, ct);
        }

        return names;
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
}
