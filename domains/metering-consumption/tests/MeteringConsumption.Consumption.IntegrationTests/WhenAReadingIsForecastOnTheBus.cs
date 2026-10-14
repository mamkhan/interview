using System.Data;
using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Consumption.Application.Extensions;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Consumption.Infrastructure.DataLake;
using MeteringConsumption.Consumption.Infrastructure.Extensions;
using MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Queries;
using MeteringConsumption.TestsShared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The Forecast role booted through the module's own registration on this class's scope, reading its production
// subscription, with SupplyPoints' query answered by a fake. 10001234567 (MCC1, ESB profile 01, DG1, not time-of-use)
// read 12 kWh a day from 2026-06-11 to 06-20, kept by Record's store and posted on 2026-06-21. Its 06-20 reading is
// announced twice, the way a redelivery reaches it, then revised to 24 kWh. Expected first days were derived offline
// from the real LP1 values: 108 kWh over the window sum 0.021990408 is 12.695349 kWh on 06-21, and 120 kWh after the
// revision is 14.105943.
public sealed class WhenAReadingIsForecastOnTheBus(WhenAReadingIsForecastOnTheBus.Recorded recorded)
    : IClassFixture<WhenAReadingIsForecastOnTheBus.Recorded>
{
    [Fact]
    public void AnnouncesTheRepeatUnderTheFirstIdAndTheRevisionAsTheNextVersion() =>
        recorded.Announced.Select(announced => announced.MessageId).Should().Equal(
            "10001234567:1", "10001234567:1", "10001234567:2");

    [Fact]
    public void ForecastsFromTheMetersOwnUse()
    {
        var first = recorded.Announced[0].Updated;

        first.UsageFactorSource.Should().Be("PUF");
        first.AnchorDate.Should().Be(new DateOnly(2026, 6, 21));
        first.Daily.Should().HaveCount(62);
        first.Daily[0].Kwh.Should().Be(12.695349m);
    }

    [Fact]
    public void ForecastsTheRevisionFromTheRevisedDay() =>
        recorded.Announced[2].Updated.Daily[0].Kwh.Should().Be(14.105943m);

    [Fact]
    public void AnnouncesOnTheSupplyPointsSession() =>
        recorded.Announced.Should().OnlyContain(announced => announced.SessionId == "10001234567");

    [Fact]
    public void KeepsOneVersionPerReadingAndOnlyTheLatestCurrent() =>
        recorded.Versions.Should().Equal((1L, false), (2L, true));

    public sealed class Recorded : IAsyncLifetime
    {
        private const string SupplyPointId = MeterDayReadingBuilder.SupplyPointId;

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private SuiteScope? _scope;
        private IHost? _host;
        private ServiceBusClient? _bus;

        public List<(ConsumptionForecastUpdated Updated, string MessageId, string SessionId)> Announced { get; } = [];

        public List<(long Version, bool IsCurrent)> Versions { get; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenAReadingIsForecastOnTheBus), new SuiteResources
            {
                Topics =
                [
                    new BusEntity(MeteringConsumptionTopology.Topics.ConsumptionRecorded,
                        ConsumptionTopology.Subscriptions.ForecastConsumptionRecorded, RequiresSession: true),
                    new BusEntity(MeteringConsumptionTopology.Topics.ConsumptionForecastUpdated, Suite.TestReader),
                ],
                BlobContainers = [MeteringConsumptionTopology.Containers.ConsumptionLedger],
                Migrate = async connectionString =>
                {
                    await ConsumptionSchema.ApplyConsumptionSchemaAsync(connectionString);
                    await ReferenceDataSchema.ApplyReferenceDataSchemaAsync(connectionString);
                },
            }, ct);

            var sql = scope.SqlConnectionString!;
            await SeedReferenceDataAsync(sql, ct);
            await SeedTariffAsync(sql, ct);

            var blobs = new BlobServiceClient(scope.Endpoints.AzuriteConnectionString,
                new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03));
            var ledger = new ParquetConsumptionLedgerStore(
                blobs, new EntityNames(Options.Create(new EntityNamingOptions { Prefix = $"{scope.Id}-" })));
            _bus = new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString);

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{EntityNamingOptions.Section}:Prefix"] = $"{scope.Id}-",
                [$"ConnectionStrings:{ConsumptionSchema.ConnectionName}"] = sql,
                [$"ConnectionStrings:{ReferenceDataSchema.ConnectionName}"] = sql,
            });
            builder.Services.TryAddTimeProvider();
            builder.Services.AddSingleton(_bus);
            builder.Services.AddKeyedSingleton(RecordOptions.LedgerConnectionName, blobs);
            builder.Services.AddSingleton<IMeterPointConfiguration>(new SingleRateConfiguration());
            builder.Services.AddBaseApplication();
            builder.Services.AddBaseInfrastructure(builder.Configuration);
            builder.Services.AddForecastApplication();
            builder.Services.AddForecastInfrastructure(builder.Configuration);
            _host = builder.Build();
            await _host.StartAsync(ct);

            await using var sender = _bus.CreateScopedSender(scope, MeteringConsumptionTopology.Topics.ConsumptionRecorded);
            await using var receiver = _bus.CreateScopedReceiver(scope, MeteringConsumptionTopology.Topics.ConsumptionForecastUpdated, Suite.TestReader);

            RecordedEntry? latest = null;
            for (var day = 11; day <= 20; day++)
            {
                latest = await KeepAsync(ledger, new MeterDay(SupplyPointId, new DateOnly(2026, 6, day)), ct);
            }

            await AnnounceAsync(sender, latest!, ct);
            await AnnounceAsync(sender, latest!, ct);
            await ReceiveAsync(receiver, 2, ct);

            var revisedDay = new MeterDay(SupplyPointId, new DateOnly(2026, 6, 20));
            var revision = new RecordedEntryBuilder(revisedDay)
                .Recording(new MeterDayReadingBuilder().ForDay(revisedDay).Build())
                .Recording(new MeterDayReadingBuilder().ForDay(revisedDay).WithRrvn(2).WithUniformKwh(0.5m).FromFile("revision").Build())
                .Build()[1];
            await ledger.AppendAsync(revision, ct);
            await AnnounceAsync(sender, revision, ct);
            await ReceiveAsync(receiver, 3, ct);

            await using var connection = new SqlConnection(sql);
            Versions.AddRange((await Dapper.SqlMapper.QueryAsync<(long, bool)>(connection, new Dapper.CommandDefinition(
                "SELECT ForecastVersion, IsCurrent FROM dbo.ConsumptionForecast ORDER BY ForecastVersion;", cancellationToken: ct))));
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

        private static async Task<RecordedEntry> KeepAsync(ParquetConsumptionLedgerStore ledger, MeterDay meterDay, CancellationToken ct)
        {
            var entry = new RecordedEntryBuilder(meterDay).Recording(new MeterDayReadingBuilder().ForDay(meterDay).Build()).Build()[0];
            await ledger.AppendAsync(entry, ct);
            return entry;
        }

        private static Task AnnounceAsync(ServiceBusSender sender, RecordedEntry kept, CancellationToken ct)
        {
            var entry = kept.Entry;
            var message = new ConsumptionRecorded
            {
                EventId = Guid.Empty,
                PublishingContext = ConsumptionTopology.PublishingContext,
                OccurredAtUtc = entry.PostedAtUtc,
                CorrelationId = "receipt-1",
                LedgerEntryId = entry.LedgerEntryId,
                SupplyPointId = entry.MeterDay.SupplyPointId,
                MeterSerial = entry.MeterSerial,
                ReadDate = entry.MeterDay.ReadDate,
                RRVN = entry.Rrvn,
                SupersedesRRVN = entry.SupersedesRrvn,
                EntryType = entry.EntryType.ToString(),
                PostedAtUtc = entry.PostedAtUtc,
                MeteringIntervalMinutes = entry.MeteringIntervalMinutes,
                UOMCode = entry.UomCode,
                Fuel = MeteringConsumptionTopology.Fuels.Electricity,
                Reading = new LedgerLeg(entry.Rrvn, [.. entry.Reading.Periods.Select(period => new LedgerPeriod(period.Period, period.Kwh, period.Status))]),
                Provenance = kept.Provenance,
            };
            return sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(message, Json))
            {
                MessageId = entry.LedgerEntryId,
                SessionId = entry.MeterDay.SupplyPointId,
                CorrelationId = message.CorrelationId,
                ContentType = "application/json",
            }, ct);
        }

        private async Task ReceiveAsync(ServiceBusReceiver receiver, int count, CancellationToken ct)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
            while (Announced.Count < count && DateTimeOffset.UtcNow < deadline)
            {
                if (await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), ct) is { } message)
                {
                    Announced.Add((message.Body.ToObjectFromJson<ConsumptionForecastUpdated>(Json)!, message.MessageId, message.SessionId));
                    await receiver.CompleteMessageAsync(message, ct);
                }
            }
        }

        // LP1 for the window and the horizon, as the reference-data loader would write it, and DG1's 2026 averages.
        private static async Task SeedReferenceDataAsync(string sql, CancellationToken ct)
        {
            var table = new DataTable();
            table.Columns.Add("LoadProfileCode", typeof(string));
            table.Columns.Add("ValueDate", typeof(DateTime));
            table.Columns.Add("Hour", typeof(byte));
            table.Columns.Add("H24", typeof(decimal));
            table.Columns.Add("D00", typeof(decimal));
            table.Columns.Add("N00", typeof(decimal));
            foreach (var fields in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "samples", "v1-load-profile-hours.csv"))
                         .Skip(1).Select(line => line.Split(',')).Where(fields => fields[0] == "LP1"))
            {
                table.Rows.Add(fields[0], DateTime.ParseExact(fields[1], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    byte.Parse(fields[2], CultureInfo.InvariantCulture), Value(fields[3]), Value(fields[4]), Value(fields[5]));
            }

            await using var connection = new SqlConnection(sql);
            await connection.OpenAsync(ct);
            using var bulk = new SqlBulkCopy(connection) { DestinationTableName = "dbo.LoadProfileHourlyValue" };
            foreach (DataColumn column in table.Columns)
            {
                bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulk.WriteToServerAsync(table, ct);
            await Dapper.SqlMapper.ExecuteAsync(connection, new Dapper.CommandDefinition(
                "INSERT INTO dbo.AnnualAverageUsage (DuosGroup, [Year], Aau24H, Aau00D, Aau00N) VALUES ('DG1', 2026, 3853, 2945, 1966);",
                cancellationToken: ct));
        }

        private static object Value(string field) =>
            field.Length == 0 ? DBNull.Value : decimal.Parse(field, NumberStyles.Float, CultureInfo.InvariantCulture);

        // The Profile's own repository writes the tariff values and flag, as the V1 reader's events would.
        private static async Task SeedTariffAsync(string sql, CancellationToken ct)
        {
            var profile = SupplyPointProfile.New(SupplyPointId, MeteringConsumptionTopology.Markets.RoiEsb);
            profile.Apply(TariffStatement.Of(false, null, null, new DateTimeOffset(2026, 6, 20, 21, 0, 0, TimeSpan.Zero), "v1-reader"));
            profile.Apply(SmartMeterFlagStatement.Of(false, new DateTimeOffset(2026, 6, 20, 21, 0, 0, TimeSpan.Zero), "v1-reader"));
            var repository = new SupplyPointProfileRepository(sql);
            await repository.SaveTariffAsync(profile, new DateTimeOffset(2026, 6, 20, 21, 0, 0, TimeSpan.Zero), ct);
            await repository.SaveSmartMeterFlagAsync(profile, new DateTimeOffset(2026, 6, 20, 21, 0, 0, TimeSpan.Zero), ct);
        }
    }

    // SupplyPoints' answer for the persona: MCC1 on ESB profile 01 in DG1, with no estimated usage factor.
    private sealed class SingleRateConfiguration : IMeterPointConfiguration
    {
        public Task<MeterPointConfiguration?> InEffectOnAsync(string mprn, DateOnly day, CancellationToken cancellationToken) =>
            Task.FromResult<MeterPointConfiguration?>(new MeterPointConfiguration
            {
                MPRN = mprn,
                Day = day,
                MeterConfigurationCode = "MCC01",
                LoadProfileCode = "01",
                DuosGroup = "DG1",
                MeterPointStatusCode = "E",
                UsageFactors = [],
            });
    }
}
