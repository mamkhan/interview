using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Growth.Contracts;
using Growth.Contracts.Events;
using MeteringConsumption.Consumption.Application.Extensions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Extensions;
using MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The Profile role booted through the module's own registration on one test class's scope, with its three topics
// and their production subscriptions, and a sender that publishes the way its publishers do: the MPRN as session
// id, the content hash (or, for tariff values, the moment they were read), MPRN and event name as message id.
internal sealed class ProfileOnTheBus : IAsyncDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly SuiteScope _scope;
    private readonly IHost _host;
    private readonly ServiceBusClient _bus;
    private readonly SupplyPointProfileRepository _repository;

    private ProfileOnTheBus(SuiteScope scope, IHost host, ServiceBusClient bus)
    {
        _scope = scope;
        _host = host;
        _bus = bus;
        _repository = new SupplyPointProfileRepository(scope.SqlConnectionString!);
    }

    public static async Task<ProfileOnTheBus> StartAsync(string testClass, CancellationToken ct)
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, testClass, new SuiteResources
        {
            Migrate = ConsumptionSchema.ApplyConsumptionSchemaAsync,
            Topics =
            [
                new BusEntity(MeteringConsumptionTopology.Topics.MeterConfigurationReceived,
                    ConsumptionTopology.Subscriptions.ProfileMeterConfigurationReceived, RequiresSession: true),
                new BusEntity(MeteringConsumptionTopology.Topics.UsageFactorsReceived,
                    ConsumptionTopology.Subscriptions.ProfileUsageFactorsReceived, RequiresSession: true),
                new BusEntity(GrowthTopology.Topics.TariffResolved,
                    ConsumptionTopology.Subscriptions.ProfileTariffResolved, RequiresSession: true),
            ],
        }, ct);

        var bus = new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{EntityNamingOptions.Section}:Prefix"] = $"{scope.Id}-",
            [$"ConnectionStrings:{ConsumptionSchema.ConnectionName}"] = scope.SqlConnectionString,
        });
        builder.Services.TryAddTimeProvider();
        builder.Services.AddSingleton(bus);
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        builder.Services.AddProfileApplication();
        builder.Services.AddProfileInfrastructure(builder.Configuration);
        var host = builder.Build();
        await host.StartAsync(ct);

        return new ProfileOnTheBus(scope, host, bus);
    }

    public Task SendAsync(MeterConfigurationReceived @event, CancellationToken ct) =>
        SendAsync(MeteringConsumptionTopology.Topics.MeterConfigurationReceived, @event, @event.MPRN,
            @event.Provenance.ContentHash, ct);

    public Task SendAsync(UsageFactorsReceived @event, CancellationToken ct) =>
        SendAsync(MeteringConsumptionTopology.Topics.UsageFactorsReceived, @event, @event.MPRN,
            @event.Provenance.ContentHash, ct);

    public Task SendAsync(TariffResolved @event, CancellationToken ct) =>
        SendAsync(GrowthTopology.Topics.TariffResolved, @event, @event.MPRN, $"{@event.AsOf:yyyyMMddHHmmss}", ct);

    public async Task<SupplyPointProfile?> PollAsync(string supplyPointId, Func<SupplyPointProfile, bool> until, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var profile = await _repository.FindAsync(supplyPointId, ct);
            if (profile is not null && until(profile))
            {
                return profile;
            }

            await Task.Delay(250, ct);
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _bus.DisposeAsync();
        await _scope.DisposeAsync();
    }

    private async Task SendAsync<TEvent>(string topic, TEvent @event, string mprn, string contentHash, CancellationToken ct)
    {
        await using var sender = _bus.CreateScopedSender(_scope, topic);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(@event, Json))
        {
            MessageId = $"{contentHash}:{mprn}:{typeof(TEvent).Name}",
            SessionId = mprn,
            ContentType = "application/json",
        }, ct);
    }
}
