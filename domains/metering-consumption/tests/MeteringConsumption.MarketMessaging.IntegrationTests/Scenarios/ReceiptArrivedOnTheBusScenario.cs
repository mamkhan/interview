using MeteringConsumption.MarketMessaging.Application.Extensions;
using MeteringConsumption.MarketMessaging.Infrastructure.Extensions;
using MeteringConsumption.MarketMessaging.Domain;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.MarketMessaging.IntegrationTests.Scenarios;

public sealed class ReceiptArrivedOnTheBusScenario : IAsyncLifetime
{
    private const string ReceiptId = "9aeb0fdf-c01e-0131-0922-9eb54906e209";

    private SuiteScope? _scope;
    private IHost? _host;

    public MarketMessage? Row { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenAReceiptArrivesOnTheBus), new SuiteResources
        {
            Migrate = MarketMessagingSchema.ApplyMarketMessagingSchemaAsync,
            Topics = [new BusEntity(MarketMessagingTopology.Topics.MarketMessageReceived, MarketMessagingTopology.Subscriptions.AuditMarketMessageReceived)],
        }, ct);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{EntityNamingOptions.Section}:Prefix"] = $"{scope.Id}-",
            [$"ConnectionStrings:{MarketMessagingSchema.ConnectionName}"] = scope.SqlConnectionString,
        });
        builder.Services.TryAddTimeProvider();
        builder.Services.AddSingleton(new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString));
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        builder.Services.AddAuditApplication();
        builder.Services.AddAuditInfrastructure(builder.Configuration);
        _host = builder.Build();
        await _host.StartAsync(ct);

        var publisher = _host.Services.GetRequiredService<IEventPublisher>();
        var receipt = new MarketMessageReceived
        {
            EventId = Guid.Parse(ReceiptId),
            PublishingContext = "metering-consumption.market-messaging",
            OccurredAtUtc = new DateTimeOffset(2026, 9, 16, 10, 0, 3, TimeSpan.Zero),
            CorrelationId = ReceiptId,
            ReceiptId = ReceiptId,
            Market = MeteringConsumptionTopology.Markets.RoiEsb,
            Fuel = MarketMessagingTopology.Fuels.Electricity,
            FileName = "DSO_MIM_343_1782352213415_3805737.xml",
            ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
            Hash = "5bb7202821a6c2e2e6ea6baec50e0e8408c6670fc94cba9c838a888bed67c724",
            BlobPath = "roi-esb/5bb7202821a6c2e2e6ea6baec50e0e8408c6670fc94cba9c838a888bed67c724.xml",
        };
        var metadata = new EventMetadata { MessageId = ReceiptId, CorrelationId = ReceiptId };

        await publisher.PublishAsync(receipt, metadata, ct);
        Row = await PollRowAsync(scope.SqlConnectionString!, ct);
    }

    private static async Task<MarketMessage?> PollRowAsync(string connectionString, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var connection = new SqlConnection(connectionString);
            var row = await connection.QuerySingleOrDefaultAsync<MarketMessage>(
                "SELECT * FROM dbo.MarketMessage WHERE ReceiptId = @ReceiptId", new { ReceiptId });
            if (row is not null)
            {
                return row;
            }

            await Task.Delay(200, ct);
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_scope is not null)
        {
            await _scope.DisposeAsync();
        }
    }
}
