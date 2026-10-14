using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Consumption;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ConsumptionApplication = MeteringConsumption.Consumption.Application.Extensions.ApplicationRegistration;
using ConsumptionInfrastructure = MeteringConsumption.Consumption.Infrastructure.Extensions.InfrastructureRegistration;
using SupplyPointsApplication = MeteringConsumption.SupplyPoints.Application.Extensions.ApplicationRegistration;
using SupplyPointsInfrastructure = MeteringConsumption.SupplyPoints.Infrastructure.Extensions.InfrastructureRegistration;

namespace MeteringConsumption.SupplyPoints.IntegrationTests.Scenarios;

// One host runs Consumption's Profile and SupplyPoints' Store on one database (their tables are apart), each on its
// own topics, and every file reaches both as Market Messaging publishes it: Profile its meter configuration and usage
// factors, Store the whole file as one change. For 10012345678, in this order: the MM332 effective 2026-09-21
// (MCC01/01/DG1/E, with its usage factors); an MM300 effective 2026-10-01 stating only MCC02; a late MM301 effective
// 2026-08-01 stating DG2; a correction effective 2026-10-01 stating load profile 03. Each module's latest values are
// read back.
public sealed class ProfileAndStoreReadTheSameMessagesScenario : IAsyncLifetime
{
    private const string Mprn = "10012345678";
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    private SuiteScope _scope = null!;
    private IHost _host = null!;
    private ServiceBusClient _bus = null!;

    public SupplyPointProfile? Profile { get; private set; }

    public (string? Mcc, string? LoadProfile, string? Duos, string? Status)? TodaysMeterPoint { get; private set; }

    public (decimal? Estimated, decimal? Actual)? Todays24HUsageFactor { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenProfileAndStoreReadTheSameMessages), new SuiteResources
        {
            Migrate = async connectionString =>
            {
                await ConsumptionSchema.ApplyConsumptionSchemaAsync(connectionString);
                await SupplyPointSchema.ApplySupplyPointSchemaAsync(connectionString);
            },
            Topics =
            [
                new BusEntity(MeteringConsumptionTopology.Topics.MeterConfigurationReceived,
                    ConsumptionTopology.Subscriptions.ProfileMeterConfigurationReceived, RequiresSession: true),
                new BusEntity(MeteringConsumptionTopology.Topics.UsageFactorsReceived,
                    ConsumptionTopology.Subscriptions.ProfileUsageFactorsReceived, RequiresSession: true),
                new BusEntity(MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived,
                    SupplyPointTopology.Subscriptions.MeterConfigurationChangeReceived, RequiresSession: true),
            ],
        }, ct);

        _bus = new ServiceBusClient(_scope.Endpoints.ServiceBusConnectionString);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{EntityNamingOptions.Section}:Prefix"] = $"{_scope.Id}-",
            [$"ConnectionStrings:{ConsumptionSchema.ConnectionName}"] = _scope.SqlConnectionString,
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(_bus);
        ConsumptionApplication.AddBaseApplication(builder.Services);
        ConsumptionInfrastructure.AddBaseInfrastructure(builder.Services, builder.Configuration);
        SupplyPointsApplication.AddBaseApplication(builder.Services);
        SupplyPointsInfrastructure.AddBaseInfrastructure(builder.Services, builder.Configuration);
        ConsumptionApplication.AddProfileApplication(builder.Services);
        ConsumptionInfrastructure.AddProfileInfrastructure(builder.Services, builder.Configuration);
        SupplyPointsApplication.AddStoreApplication(builder.Services);
        SupplyPointsInfrastructure.AddStoreInfrastructure(builder.Services, builder.Configuration);
        _host = builder.Build();
        await _host.StartAsync(ct);

        var usageFactors = new UsageFactorsReceivedBuilder().Build() with { EventId = Id(332) };
        await SendAsync(new MeterConfigurationReceivedBuilder().Build(), usageFactors, ct);
        await SendAsync(new MeterConfigurationReceivedBuilder().EffectiveFrom(new DateOnly(2026, 10, 1)).FromFile("hash-mm300", "300")
            .Build() with { EventId = Id(300), LoadProfileCode = null, DuosGroup = null, MeterPointStatusCode = null, MeterConfigurationCode = "MCC02" }, null, ct);
        await SendAsync(new MeterConfigurationReceivedBuilder().EffectiveFrom(new DateOnly(2026, 8, 1)).FromFile("hash-mm301", "301")
            .Build() with { EventId = Id(301), MeterConfigurationCode = null, LoadProfileCode = null, DuosGroup = "DG2", MeterPointStatusCode = null }, null, ct);
        await SendAsync(new MeterConfigurationReceivedBuilder().EffectiveFrom(new DateOnly(2026, 10, 1)).FromFile("hash-correction", "300")
            .Build() with { EventId = Id(1), MeterConfigurationCode = null, LoadProfileCode = "03", DuosGroup = null, MeterPointStatusCode = null }, null, ct);
        await SendAsync(MeteringConsumptionTopology.Topics.UsageFactorsReceived, usageFactors, usageFactors.EventId, ct);

        await PollAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _bus.DisposeAsync();
        await _scope.DisposeAsync();
    }

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    // Profile reads the file's meter configuration now and its usage factors once every file has been sent; Store reads
    // the file whole, as Market Messaging builds the change from those same two events.
    private async Task SendAsync(MeterConfigurationReceived configuration, UsageFactorsReceived? usageFactors, CancellationToken ct)
    {
        await SendAsync(MeteringConsumptionTopology.Topics.MeterConfigurationReceived, configuration, configuration.EventId, ct);
        await SendAsync(MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived, new MeterConfigurationChangeReceived
        {
            EventId = configuration.EventId,
            PublishingContext = configuration.PublishingContext,
            OccurredAtUtc = configuration.OccurredAtUtc,
            CorrelationId = configuration.CorrelationId,
            MPRN = configuration.MPRN,
            NetworksReferenceNumber = configuration.NetworksReferenceNumber,
            EffectiveFromDate = configuration.EffectiveFromDate,
            ReadDate = configuration.ReadDate,
            Configuration = new MeterConfiguration
            {
                MeterConfigurationCode = configuration.MeterConfigurationCode,
                LoadProfileCode = configuration.LoadProfileCode,
                DuosGroup = configuration.DuosGroup,
                MeterPointStatusCode = configuration.MeterPointStatusCode,
                UsageFactors = usageFactors?.UsageFactors ?? [],
                Meters = [],
            },
            Provenance = configuration.Provenance,
        }, configuration.EventId, ct);
    }

    private async Task SendAsync<TEvent>(string topic, TEvent @event, Guid eventId, CancellationToken ct)
    {
        await using var sender = _bus.CreateScopedSender(_scope, topic);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(@event, Json))
        {
            MessageId = eventId.ToString(),
            SessionId = Mprn,
            ContentType = "application/json",
        }, ct);
    }

    // Both modules have read everything once Profile holds the correction's load profile and both usage factors, and
    // SupplyPoints' rows in effect today hold the correction's load profile and the actual.
    private async Task PollAsync(CancellationToken ct)
    {
        var repository = new SupplyPointProfileRepository(_scope.SqlConnectionString!);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        while (DateTimeOffset.UtcNow < deadline)
        {
            Profile = await repository.FindAsync(Mprn, ct);
            await using var connection = new SqlConnection(_scope.SqlConnectionString);
            TodaysMeterPoint = await connection.QuerySingleOrDefaultAsync<(string?, string?, string?, string?)?>(new CommandDefinition("""
                SELECT TOP 1 mp.[MeterConfigurationCode], mp.[LoadProfileCode], mp.[DuosGroup], mp.[MeterPointStatusCode]
                FROM [supply].[MeterPoint] mp JOIN [supply].[SupplyPoint] sp ON sp.[SupplyPointId] = mp.[SupplyPointId]
                WHERE sp.[XPRN] = @Mprn AND mp.[EffectiveDate] <= CAST(SYSUTCDATETIME() AS date) ORDER BY mp.[EffectiveDate] DESC;
                """, new { Mprn }, cancellationToken: ct));
            Todays24HUsageFactor = await connection.QuerySingleOrDefaultAsync<(decimal?, decimal?)?>(new CommandDefinition("""
                SELECT TOP 1 uf.[EstimatedUsageFactor], uf.[ActualUsageFactor]
                FROM [supply].[UsageFactor] uf JOIN [supply].[SupplyPoint] sp ON sp.[SupplyPointId] = uf.[SupplyPointId]
                WHERE sp.[XPRN] = @Mprn AND uf.[TimeslotCode] = '24H' AND uf.[EffectiveDate] <= CAST(SYSUTCDATETIME() AS date)
                ORDER BY uf.[EffectiveDate] DESC;
                """, new { Mprn }, cancellationToken: ct));

            var profileDone = Profile?.Configuration?.LoadProfileCode == "03" && Profile.UsageFactors.Count == 2;
            if (profileDone && TodaysMeterPoint?.LoadProfile == "03" && Todays24HUsageFactor?.Actual is not null)
            {
                return;
            }

            await Task.Delay(250, ct);
        }
    }
}
