using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.SupplyPoints.Application.Extensions;
using MeteringConsumption.SupplyPoints.Infrastructure.Extensions;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Contracts;
using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// The Reload role booted through the module's own registration on one test class's scope, with the platform's topic and
// the production subscription, and a sender that publishes the way the V1 reader does: no session, the event's id as
// the message id.
internal sealed class ReloadOnTheBus : IAsyncDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly SuiteScope _scope;
    private readonly IHost _host;
    private readonly ServiceBusClient _bus;

    private ReloadOnTheBus(SuiteScope scope, IHost host, ServiceBusClient bus)
    {
        _scope = scope;
        _host = host;
        _bus = bus;
    }

    public static async Task<ReloadOnTheBus> StartAsync(string testClass, CancellationToken ct)
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, testClass, new SuiteResources
        {
            Migrate = SupplyPointSchema.ApplySupplyPointSchemaAsync,
            Topics =
            [
                new BusEntity(PlatformTopology.Topics.SupplyPointLoadedFromV1, SupplyPointTopology.Subscriptions.SupplyPointLoadedFromV1),
            ],
        }, ct);

        var bus = new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{EntityNamingOptions.Section}:Prefix"] = $"{scope.Id}-",
            [$"ConnectionStrings:{SupplyPointSchema.ConnectionName}"] = scope.SqlConnectionString,
        });
        builder.Services.TryAddTimeProvider();
        builder.Services.AddSingleton(bus);
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        builder.Services.AddReloadApplication();
        builder.Services.AddReloadInfrastructure(builder.Configuration);
        var host = builder.Build();
        await host.StartAsync(ct);

        return new ReloadOnTheBus(scope, host, bus);
    }

    public async Task SendAsync(SupplyPointLoadedFromV1 @event, CancellationToken ct)
    {
        await using var sender = _bus.CreateScopedSender(_scope, PlatformTopology.Topics.SupplyPointLoadedFromV1);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(@event, Json))
        {
            MessageId = @event.EventId.ToString(),
            ContentType = "application/json",
        }, ct);
    }

    // Read until the rows satisfy the caller, so a test waits for what it sent rather than for a fixed time.
    public Task<IReadOnlyList<(DateTime EffectiveDate, string? DuosGroup)>> PollMeterPointAsync(
        string xprn, Func<IReadOnlyList<(DateTime EffectiveDate, string? DuosGroup)>, bool> arrived, CancellationToken ct) =>
        PollAsync("""
            SELECT mp.[EffectiveDate], mp.[DuosGroup] FROM [supply].[MeterPoint] mp
            JOIN [supply].[SupplyPoint] sp ON sp.[SupplyPointId] = mp.[SupplyPointId]
            WHERE sp.[XPRN] = @xprn ORDER BY mp.[EffectiveDate];
            """, xprn, arrived, ct);

    public Task<IReadOnlyList<(DateTime EffectiveDate, decimal? Estimated, decimal? Actual)>> ReadUsageFactorsAsync(
        string table, string xprn, CancellationToken ct) =>
        PollAsync<(DateTime, decimal?, decimal?)>($"""
            SELECT t.[EffectiveDate], t.[EstimatedUsageFactor], t.[ActualUsageFactor] FROM [supply].[{table}] t
            JOIN [supply].[SupplyPoint] sp ON sp.[SupplyPointId] = t.[SupplyPointId]
            WHERE sp.[XPRN] = @xprn ORDER BY t.[TimeslotCode], t.[EffectiveDate];
            """, xprn, _ => true, ct);

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _bus.DisposeAsync();
        await _scope.DisposeAsync();
    }

    private async Task<IReadOnlyList<T>> PollAsync<T>(string sql, string xprn, Func<IReadOnlyList<T>, bool> arrived, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        IReadOnlyList<T> found = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var connection = new SqlConnection(_scope.SqlConnectionString);
            found = [.. await connection.QueryAsync<T>(new CommandDefinition(sql, new { xprn }, cancellationToken: ct))];
            if (arrived(found))
            {
                return found;
            }

            await Task.Delay(250, ct);
        }

        return found;
    }
}
