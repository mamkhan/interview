using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Persistence;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Contracts.Queries;
using MeteringConsumption.SupplyPoints.Application.Extensions;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using MeteringConsumption.SupplyPoints.Infrastructure.Extensions;
using MeteringConsumption.SupplyPoints.Infrastructure.Persistence.Repositories;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// The Provide role booted through the module's own registration on a database built the way the migrator builds one,
// holding the Diagrams 4 example for 10000000001 recorded on 2026-10-09: meter point A from 2026-03-01 (MCC01, 01, DG1,
// E), B from 2026-06-15 (MCC02, 02), a backdated MM301 from 2026-05-01 (DG2); the 24H estimate 4127.5 from 2026-03-01;
// the 00D estimate 1500.0 from 2026-03-01 and actual 1400.0 from 2026-06-15. Current holds the meter point's and 00D's
// 2026-06-15 rows and 24H's 2026-03-01 row. Another module then asks what was in effect.
public sealed class WhenAnotherModuleReadsWhatIsInEffect(WhenAnotherModuleReadsWhatIsInEffect.Provided provided)
    : IClassFixture<WhenAnotherModuleReadsWhatIsInEffect.Provided>
{
    [Fact]
    public async Task AnswersTodayFromTheCurrentRows() =>
        (await provided.AskAsync(new DateOnly(2026, 10, 9))).Should().BeEquivalentTo(new MeterPointConfiguration
        {
            MPRN = "10000000001",
            Day = new DateOnly(2026, 10, 9),
            MeterConfigurationCode = "MCC02",
            LoadProfileCode = "02",
            DuosGroup = "DG2",
            MeterPointStatusCode = "E",
            UsageFactors = [new UsageFactorInEffect("00D", 1500.0m, 1400.0m), new UsageFactorInEffect("24H", 4127.5m, null)],
        });

    // No meter point or 00D current row is that old, so those come from history; 24H's current row reaches back.
    [Fact]
    public async Task AnswersADayBeforeCurrentFromWhatHistoryStatedByThen() =>
        (await provided.AskAsync(new DateOnly(2026, 5, 20))).Should().BeEquivalentTo(new MeterPointConfiguration
        {
            MPRN = "10000000001",
            Day = new DateOnly(2026, 5, 20),
            MeterConfigurationCode = "MCC01",
            LoadProfileCode = "01",
            DuosGroup = "DG2",
            MeterPointStatusCode = "E",
            UsageFactors = [new UsageFactorInEffect("00D", 1500.0m, null), new UsageFactorInEffect("24H", 4127.5m, null)],
        });

    public sealed class Provided : IAsyncLifetime
    {
        private static readonly DateOnly RecordedOn = new(2026, 10, 9);

        private IHost _host = null!;
        private ServiceBusClient _bus = null!;

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenAnotherModuleReadsWhatIsInEffect),
                new SuiteResources { Migrate = SupplyPointSchema.ApplySupplyPointSchemaAsync }, ct);
            await StoreTheExampleAsync(scope.SqlConnectionString!, ct);

            // The base registration's publisher needs a client to compose; Provide never sends, so it never connects.
            _bus = new ServiceBusClient("Endpoint=sb://localhost;SharedAccessKeyName=unused;SharedAccessKey=unused;UseDevelopmentEmulator=true;");
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SupplyPointSchema.ConnectionName}"] = scope.SqlConnectionString,
            });
            builder.Services.AddSingleton(_bus);
            builder.Services.AddBaseApplication();
            builder.Services.AddBaseInfrastructure(builder.Configuration);
            builder.Services.AddProvideApplication();
            builder.Services.AddProvideInfrastructure(builder.Configuration);
            _host = builder.Build();
        }

        public async ValueTask DisposeAsync()
        {
            _host.Dispose();
            await _bus.DisposeAsync();
        }

        public Task<MeterPointConfiguration?> AskAsync(DateOnly day) =>
            _host.Services.GetRequiredService<IMeterPointConfiguration>()
                .InEffectOnAsync("10000000001", day, TestContext.Current.CancellationToken);

        private static async Task StoreTheExampleAsync(string connectionString, CancellationToken ct)
        {
            await using var services = new ServiceCollection().AddUnitOfWork(connectionString).BuildServiceProvider();
            await using var unitOfWorkScope = services.CreateAsyncScope();
            var unitOfWork = unitOfWorkScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(ct);
            var repository = new SupplyPointRepository(unitOfWork);
            var xprn = new XPRN("10000000001");
            await repository.AddAsync(SupplyPoint.New(xprn, "roi-esb"), ct);

            async Task RecordAsync<TValues>(ConfigurationKey<TValues> key, Dated<TValues> statement)
                where TValues : class, ICarriedValues<TValues> =>
                await repository.SaveAsync(key, Recording.Decide(statement, RecordedOn,
                    await repository.FactsAsync(key, statement.EffectiveDate, ct)), ct);

            await RecordAsync(new MeterPointKey(xprn), new MeterPointStatementBuilder().Build());
            await RecordAsync(new MeterPointKey(xprn), new MeterPointStatementBuilder()
                .StartingOn(new DateOnly(2026, 6, 15)).WithCodes("MCC02", "02", null, null).WithNetworksReferenceNumber("NRN-hash-mm332").Build());
            await RecordAsync(new MeterPointKey(xprn), new MeterPointStatementBuilder()
                .StartingOn(new DateOnly(2026, 5, 1)).WithCodes(null, null, "DG2", null).WithNetworksReferenceNumber("NRN-hash-mm301").Build());
            await RecordAsync(new UsageFactorKey(xprn, "24H"), new SupplyPointUsageFactorStatementBuilder().Build());
            await RecordAsync(new UsageFactorKey(xprn, "00D"), new SupplyPointUsageFactorStatementBuilder().WithValues(1500.0m, null).Build());
            await RecordAsync(new UsageFactorKey(xprn, "00D"), new SupplyPointUsageFactorStatementBuilder()
                .StartingOn(new DateOnly(2026, 6, 15)).WithValues(null, 1400.0m).Build());
            await unitOfWork.CommitAsync(ct);
        }
    }
}
