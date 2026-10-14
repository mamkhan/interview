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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The Record role booted through the module's own registration on this class's scope, reading its production
// subscription. The persona's RRVN 1 (0.5 KWT, 0.25 kWh a half-hour) is published twice, the way a redelivery
// reaches it, then RRVN 2 (0.4 KWT, 0.20 kWh) from another file with its own receipt: two ledger files, and three
// announcements, the repeat under the same id, each carrying the correlation id of the reading it answers.
public sealed class WhenAReadingArrivesOnTheBus(WhenAReadingArrivesOnTheBus.Arrived arrived)
    : IClassFixture<WhenAReadingArrivesOnTheBus.Arrived>
{
    private const string FirstId =
        "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00:10001234567:2026-06-20:1";

    private const string FirstCorrelationId = "5c2a4f1e-7d3b-4e8a-9f61-0b2c3d4e5f60";
    private const string RevisionCorrelationId = "8d1e6b2a-4c3f-4a5e-9b7d-2f1a0c9e8d7b";

    [Fact]
    public void KeepsOneFilePerVersion() =>
        arrived.Files.Should().BeEquivalentTo("10001234567/2026-06-20_1.parquet", "10001234567/2026-06-20_2.parquet");

    [Fact]
    public void AnnouncesTheRepeatUnderTheFirstId() =>
        arrived.Announced.Select(announced => announced.MessageId).Should().Equal(
            FirstId, FirstId, "revision:10001234567:2026-06-20:2");

    [Fact]
    public void AnnouncesTheRevisionWithRrvnOneCancelled()
    {
        var revision = arrived.Announced[2].Recorded;

        revision.SupersedesRRVN.Should().Be(1);
        revision.Reversal!.Periods.Should().HaveCount(48).And.OnlyContain(period => period.Kwh == -0.25m);
        revision.Reading.Periods.Should().HaveCount(48).And.OnlyContain(period => period.Kwh == 0.20m);
    }

    [Fact]
    public void AnnouncesUnderTheCorrelationIdOfTheReadingItAnswers() =>
        arrived.Announced.Select(announced => announced.CorrelationId).Should().Equal(
            FirstCorrelationId, FirstCorrelationId, RevisionCorrelationId);

    [Fact]
    public void AnnouncesOnTheSupplyPointsSession() =>
        arrived.Announced.Should().OnlyContain(announced => announced.SessionId == "10001234567");

    public sealed class Arrived : IAsyncLifetime
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private SuiteScope? _scope;
        private IHost? _host;
        private ServiceBusClient? _bus;

        public List<(ConsumptionRecorded Recorded, string MessageId, string SessionId, string CorrelationId)> Announced { get; } = [];

        public List<string> Files { get; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenAReadingArrivesOnTheBus), new SuiteResources
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
                new IntervalReadingReceivedBuilder().Build(),
                new IntervalReadingReceivedBuilder().Build(),
                new IntervalReadingReceivedBuilder().FromFile("revision").WithRrvn(2).WithUniformValue(0.40m)
                    .WithCorrelationId(RevisionCorrelationId).Build(),
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
            while (Announced.Count < 3 && DateTimeOffset.UtcNow < deadline)
            {
                if (await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), ct) is { } message)
                {
                    Announced.Add((message.Body.ToObjectFromJson<ConsumptionRecorded>(Json)!, message.MessageId, message.SessionId,
                        message.CorrelationId));
                    await receiver.CompleteMessageAsync(message, ct);
                }
            }

            var ledger = blobs.GetBlobContainerClient(scope.BlobContainer(MeteringConsumptionTopology.Containers.ConsumptionLedger));
            await foreach (var blob in ledger.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.None,
                Azure.Storage.Blobs.Models.BlobStates.None, null, ct))
            {
                Files.Add(blob.Name);
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
    }
}
