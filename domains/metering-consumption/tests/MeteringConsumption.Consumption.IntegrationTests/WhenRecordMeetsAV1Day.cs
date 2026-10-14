using System.Text.Json;
using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Consumption.Application.Extensions;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Consumption.Infrastructure.Extensions;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Contracts.Ledger;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.Consumption.IntegrationTests;

// Days the V1 reader wrote straight into the ledger, through the published file format, met by live readings through
// the Record role: for 10001234567 on 2026-06-20 ESB's version 1 as V1 held it (0.25 kWh a half-hour, VVAK), and for
// 10001234568 V1's estimate at version 0 (0.35 kWh, P, KWH). The persona's live version 1 (0.5 KWT, 0.25 kWh) then
// reaches both: the first is already kept and announced under V1's entry id; the second cancels the estimate.
public sealed class WhenRecordMeetsAV1Day(WhenRecordMeetsAV1Day.Met met)
    : IClassFixture<WhenRecordMeetsAV1Day.Met>
{
    private const string Reading = "10001234567";
    private const string Estimated = "10001234568";

    [Fact]
    public void WritesNothingForTheVersionV1AlreadyWrote() =>
        met.Files.Should().BeEquivalentTo(
            "10001234567/2026-06-20_1.parquet", "10001234568/2026-06-20_0.parquet", "10001234568/2026-06-20_1.parquet");

    [Fact]
    public void LeavesTheFileV1WroteAsItWas() =>
        met.ReadingFileAfter.Should().Equal(met.ReadingFileBefore);

    [Fact]
    public void AnnouncesTheKeptVersionUnderV1sEntryId() =>
        met.Announced[Reading].MessageId.Should().Be("v1-reader:343:103:10001234567:2026-06-20:1");

    [Fact]
    public void CancelsV1sEstimateInFullAndPostsTheReading()
    {
        var revision = met.Announced[Estimated].Recorded;

        revision.SupersedesRRVN.Should().Be(0);
        revision.Reversal!.Periods.Should().HaveCount(48).And.OnlyContain(period => period.Kwh == -0.35m && period.Status == "P");
        revision.Reading.Periods.Should().HaveCount(48).And.OnlyContain(period => period.Kwh == 0.25m);
    }

    public sealed class Met : IAsyncLifetime
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly DateOnly Day = new(2026, 6, 20);

        private SuiteScope? _scope;
        private IHost? _host;
        private ServiceBusClient? _bus;

        public Dictionary<string, (ConsumptionRecorded Recorded, string MessageId)> Announced { get; } = [];

        public List<string> Files { get; } = [];

        public byte[] ReadingFileBefore { get; private set; } = [];

        public byte[] ReadingFileAfter { get; private set; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenRecordMeetsAV1Day), new SuiteResources
            {
                Topics =
                [
                    new BusEntity(MeteringConsumptionTopology.Topics.IntervalReadingReceived,
                        ConsumptionTopology.Subscriptions.RecordIntervalReadingReceived, RequiresSession: true),
                    new BusEntity(MeteringConsumptionTopology.Topics.ConsumptionRecorded, Suite.TestReader),
                ],
                BlobContainers = [MeteringConsumptionTopology.Containers.ConsumptionLedger],
            }, ct);

            var blobs = new BlobServiceClient(scope.Endpoints.AzuriteConnectionString,
                new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03));
            var ledger = blobs.GetBlobContainerClient(scope.BlobContainer(MeteringConsumptionTopology.Containers.ConsumptionLedger));
            await WriteAsync(ledger, Reading, 1, V1Rows(Reading, 1, 0.25m, "VVAK", "KWT", "v1-reader:343:103"), ct);
            await WriteAsync(ledger, Estimated, 0, V1Rows(Estimated, 0, 0.35m, "P", "KWH", "v1-reader:estimate:6"), ct);
            ReadingFileBefore = await DownloadAsync(ledger, Reading, 1, ct);

            _bus = new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString);
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{EntityNamingOptions.Section}:Prefix"] = $"{scope.Id}-",
            });
            builder.Services.TryAddTimeProvider();
            builder.Services.AddSingleton(_bus);
            builder.Services.AddKeyedSingleton(RecordOptions.LedgerConnectionName, blobs);
            builder.Services.AddBaseApplication();
            builder.Services.AddBaseInfrastructure(builder.Configuration);
            builder.Services.AddRecordApplication();
            builder.Services.AddRecordInfrastructure(builder.Configuration);
            _host = builder.Build();
            await _host.StartAsync(ct);

            await using var sender = _bus.CreateScopedSender(scope, MeteringConsumptionTopology.Topics.IntervalReadingReceived);
            await using var receiver = _bus.CreateScopedReceiver(scope, MeteringConsumptionTopology.Topics.ConsumptionRecorded, Suite.TestReader);
            foreach (var reading in new[]
            {
                new IntervalReadingReceivedBuilder().ForMprn(Reading).Build(),
                new IntervalReadingReceivedBuilder().ForMprn(Estimated).Build(),
            })
            {
                await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(reading, Json))
                {
                    MessageId = reading.ReadingId,
                    SessionId = reading.MPRN,
                    CorrelationId = reading.CorrelationId,
                    ContentType = "application/json",
                }, ct);
            }

            var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
            while (Announced.Count < 2 && DateTimeOffset.UtcNow < deadline)
            {
                if (await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), ct) is { } message)
                {
                    var recorded = message.Body.ToObjectFromJson<ConsumptionRecorded>(Json)!;
                    Announced[recorded.SupplyPointId] = (recorded, message.MessageId);
                    await receiver.CompleteMessageAsync(message, ct);
                }
            }

            await foreach (var blob in ledger.GetBlobsAsync(cancellationToken: ct))
            {
                Files.Add(blob.Name);
            }

            ReadingFileAfter = await DownloadAsync(ledger, Reading, 1, ct);
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

        // One day as the V1 reader writes it: a first entry, one reading leg of 48 half-hours, marked as V1's.
        private static IEnumerable<LedgerRow> V1Rows(string mprn, int rrvn, decimal kwh, string status, string uom, string contentHash) =>
            Enumerable.Range(1, 48).Select(period => new LedgerRow
            {
                LedgerEntryId = $"{contentHash}:{mprn}:2026-06-20:{rrvn}",
                SupplyPointId = mprn,
                MeterSerial = "M6011",
                ReadDate = Day,
                EntryType = LedgerRow.ReadingReceived,
                Rrvn = rrvn,
                Leg = LedgerRow.ReadingLeg,
                PostedAtUtc = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc),
                Period = period,
                Kwh = kwh,
                Status = status,
                MeteringIntervalMinutes = 30,
                UomCode = uom,
                SourceMessageType = "V1",
                MarketTimestamp = new DateTime(2026, 6, 21, 8, 0, 0, DateTimeKind.Utc),
                BlobPath = "",
                ContentHash = contentHash,
                ReceivedFileName = "",
                Fuel = "electricity",
                Market = "roi-esb",
                Producer = "platform.v1-reader",
            });

        private static async Task WriteAsync(BlobContainerClient ledger, string mprn, int rrvn, IEnumerable<LedgerRow> rows,
            CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await LedgerFile.WriteAsync(rows, buffer, ct);
            buffer.Position = 0;
            await ledger.GetBlobClient(LedgerPath.File(mprn, Day, rrvn)).UploadAsync(buffer, overwrite: false, ct);
        }

        private static async Task<byte[]> DownloadAsync(BlobContainerClient ledger, string mprn, int rrvn, CancellationToken ct) =>
            (await ledger.GetBlobClient(LedgerPath.File(mprn, Day, rrvn)).DownloadContentAsync(ct)).Value.Content.ToArray();
    }
}
