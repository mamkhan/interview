using MeteringConsumption.MarketMessaging.Domain;
using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using MeteringConsumption.MarketMessaging.Infrastructure.Persistence.Repositories;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.IntegrationTests;

// The two outcome handlers over the real repository: the Interpret feature's outcome lands in the receipt row's
// columns. An outcome that overtakes its receipt is the recorder's rule, a unit test.
public sealed class WhenRecordingAnOutcome : IClassFixture<WhenRecordingAnOutcome.Database>
{
    private static readonly DateTimeOffset ProcessedAt = new(2026, 9, 16, 10, 0, 5, 456, TimeSpan.Zero);

    private readonly string _connectionString;
    private readonly MarketMessageRepository _repository;
    private readonly AuditMarketMessageProcessedEventHandler _processed;
    private readonly AuditMarketMessageFailedEventHandler _failed;

    public WhenRecordingAnOutcome(Database database)
    {
        _connectionString = database.ConnectionString;
        _repository = new MarketMessageRepository(_connectionString);
        var recorder = new ReceiptOutcomeRecorder(_repository, new FixedTimeProvider(ProcessedAt), NullLogger<ReceiptOutcomeRecorder>.Instance);
        _processed = new AuditMarketMessageProcessedEventHandler(recorder);
        _failed = new AuditMarketMessageFailedEventHandler(recorder);
    }

    [Fact]
    public async Task UpdatesTheReceiptWithTypeOutcomeAndProcessedAt()
    {
        await _repository.AddAsync(Receipt("r-1"), TestContext.Current.CancellationToken);

        await _processed.HandleAsync(Processed("r-1") with { Interpreter = "MM343Strategy", ReadingsPublished = 2 }, TestContext.Current.CancellationToken);

        var row = await RowAsync("r-1");
        row.MessageType.Should().Be("343");
        row.Outcome.Should().Be("processed");
        row.Interpreter.Should().Be("MM343Strategy");
        row.ReadingsPublished.Should().Be(2);
        row.ProcessedAt.Should().Be(ProcessedAt);
    }

    [Fact]
    public async Task RecordsTheFailureReason()
    {
        await _repository.AddAsync(Receipt("r-3"), TestContext.Current.CancellationToken);

        await _failed.HandleAsync(new MarketMessageFailed
        {
            EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            PublishingContext = "metering-consumption.market-messaging",
            OccurredAtUtc = ProcessedAt,
            CorrelationId = "r-3",
            ReceiptId = "r-3",
            Reason = "The file has no MessageHeader element.",
        }, TestContext.Current.CancellationToken);

        var row = await RowAsync("r-3");
        row.Outcome.Should().Be("failed");
        row.MessageType.Should().BeNull();
        row.FailureReason.Should().Be("The file has no MessageHeader element.");
        row.ProcessedAt.Should().Be(ProcessedAt);
    }

    private static MarketMessage Receipt(string receiptId) => new()
    {
        ReceiptId = receiptId,
        Market = "roi-esb",
        Fuel = "electricity",
        FileName = "DSO_MIM_343_1782352213415_3805737.xml",
        ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        Hash = "hash",
        BlobPath = "roi-esb/hash.xml",
        CorrelationId = receiptId,
    };

    private static MarketMessageProcessed Processed(string receiptId) => new()
    {
        EventId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = ProcessedAt,
        CorrelationId = receiptId,
        ReceiptId = receiptId,
        MessageType = "343",
        Outcome = MarketMessageOutcomes.Processed,
    };

    private async Task<MarketMessage> RowAsync(string receiptId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<MarketMessage>(
            "SELECT * FROM dbo.MarketMessage WHERE ReceiptId = @receiptId", new { receiptId });
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenRecordingAnOutcome), MarketMessagingSchema.ApplyMarketMessagingSchemaAsync);
}
