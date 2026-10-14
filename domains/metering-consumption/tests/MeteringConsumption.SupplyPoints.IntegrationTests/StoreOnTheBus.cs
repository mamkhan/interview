using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.SupplyPoints.Application.Extensions;
using MeteringConsumption.SupplyPoints.Infrastructure.Extensions;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// The Store role booted through the module's own registration on one test class's scope, with SupplyPoints' topic and
// its production subscription, and a sender that publishes the way Market Messaging does: the MPRN as session id.
internal sealed class StoreOnTheBus : IAsyncDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly SuiteScope _scope;
    private readonly IHost _host;
    private readonly ServiceBusClient _bus;

    private StoreOnTheBus(SuiteScope scope, IHost host, ServiceBusClient bus)
    {
        _scope = scope;
        _host = host;
        _bus = bus;
    }

    public static async Task<StoreOnTheBus> StartAsync(string testClass, CancellationToken ct)
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, testClass, new SuiteResources
        {
            Migrate = SupplyPointSchema.ApplySupplyPointSchemaAsync,
            Topics =
            [
                new BusEntity(MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived,
                    SupplyPointTopology.Subscriptions.MeterConfigurationChangeReceived, RequiresSession: true),
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
        builder.Services.AddStoreApplication();
        builder.Services.AddStoreInfrastructure(builder.Configuration);
        var host = builder.Build();
        await host.StartAsync(ct);

        return new StoreOnTheBus(scope, host, bus);
    }

    public Task SendAsync(MeterConfigurationChangeReceived @event, CancellationToken ct) =>
        SendAsync(MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived, @event, @event.MPRN, @event.EventId, ct);

    // Read until the table holds at least the given number of rows for the supply point, so a test waits for what it sent
    // rather than for a fixed time.
    public async Task<IReadOnlyList<(DateTime EffectiveDate, string? DuosGroup)>> PollMeterPointAsync(
        string table, string xprn, int rows, CancellationToken ct) =>
        await PollAsync<(DateTime, string?)>($"""
            SELECT t.[EffectiveDate], t.[DuosGroup] FROM [supply].[{table}] t
            JOIN [supply].[SupplyPoint] sp ON sp.[SupplyPointId] = t.[SupplyPointId]
            WHERE sp.[XPRN] = @xprn ORDER BY t.[EffectiveDate];
            """, xprn, rows, ct);

    public async Task<IReadOnlyList<(DateTime EffectiveDate, decimal? Estimated, decimal? Actual)>> PollUsageFactorAsync(
        string table, string xprn, int rows, CancellationToken ct) =>
        await PollAsync<(DateTime, decimal?, decimal?)>($"""
            SELECT t.[EffectiveDate], t.[EstimatedUsageFactor], t.[ActualUsageFactor] FROM [supply].[{table}] t
            JOIN [supply].[SupplyPoint] sp ON sp.[SupplyPointId] = t.[SupplyPointId]
            WHERE sp.[XPRN] = @xprn ORDER BY t.[TimeslotCode], t.[EffectiveDate];
            """, xprn, rows, ct);

    public async Task<IReadOnlyList<(string SerialNumber, DateTime EffectiveDate, string? Category)>> PollMeterAsync(
        string xprn, int rows, CancellationToken ct) =>
        await PollAsync<(string, DateTime, string?)>("""
            SELECT m.[SerialNumber], m.[EffectiveDate], m.[MeterCategoryCode] FROM [supply].[Meter] m
            JOIN [supply].[SupplyPoint] sp ON sp.[SupplyPointId] = m.[SupplyPointId]
            WHERE sp.[XPRN] = @xprn ORDER BY m.[SerialNumber], m.[EffectiveDate];
            """, xprn, rows, ct);

    public async Task<IReadOnlyList<(string SerialNumber, string Key, DateTime EffectiveDate, decimal? Read)>> PollRegisterAsync(
        string xprn, int rows, CancellationToken ct) =>
        await PollAsync<(string, string, DateTime, decimal?)>("""
            SELECT r.[SerialNumber], r.[RegisterKey], r.[EffectiveDate], r.[ReadingValue] FROM [supply].[RegisterLevelInfo] r
            JOIN [supply].[SupplyPoint] sp ON sp.[SupplyPointId] = r.[SupplyPointId]
            WHERE sp.[XPRN] = @xprn ORDER BY r.[SerialNumber], r.[RegisterKey], r.[EffectiveDate];
            """, xprn, rows, ct);

    public async Task<int> InboxMarkersAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(_scope.SqlConnectionString);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.ProcessedMessage", cancellationToken: ct));
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _bus.DisposeAsync();
        await _scope.DisposeAsync();
    }

    private async Task<IReadOnlyList<T>> PollAsync<T>(string sql, string xprn, int rows, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        IReadOnlyList<T> found = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var connection = new SqlConnection(_scope.SqlConnectionString);
            found = [.. await connection.QueryAsync<T>(new CommandDefinition(sql, new { xprn }, cancellationToken: ct))];
            if (found.Count >= rows)
            {
                return found;
            }

            await Task.Delay(250, ct);
        }

        return found;
    }

    private async Task SendAsync<TEvent>(string topic, TEvent @event, string mprn, Guid eventId, CancellationToken ct)
    {
        await using var sender = _bus.CreateScopedSender(_scope, topic);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(@event, Json))
        {
            MessageId = $"{eventId}:{Guid.NewGuid():N}",
            SessionId = mprn,
            ContentType = "application/json",
        }, ct);
    }
}
