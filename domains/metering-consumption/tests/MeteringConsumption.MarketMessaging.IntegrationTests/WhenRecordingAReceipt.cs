using MeteringConsumption.MarketMessaging.Domain;
using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using MeteringConsumption.MarketMessaging.Infrastructure.Persistence.Repositories;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.MarketMessaging.IntegrationTests;

// The receipt table on this class's database: one row per arrival keyed by receipt id, the same bytes
// twice as two rows, timestamps kept to the millisecond, and an outcome for a receipt not yet written
// reported as no row rather than invented.
public sealed class WhenRecordingAReceipt(WhenRecordingAReceipt.Database database)
    : IClassFixture<WhenRecordingAReceipt.Database>
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 9, 16, 10, 0, 0, 123, TimeSpan.Zero);

    private readonly string _connectionString = database.ConnectionString;
    private readonly MarketMessageRepository _repository = new(database.ConnectionString);

    [Fact]
    public async Task InsertsTheRow()
    {
        var inserted = await _repository.AddAsync(Receipt("r-1", "hash-1"), TestContext.Current.CancellationToken);

        inserted.Should().BeTrue();
        var row = await RowAsync("r-1");
        row.Market.Should().Be("roi-esb");
        row.FileName.Should().Be("DSO_MIM_343_1782352213415_3805737.xml");
        row.Hash.Should().Be("hash-1");
        row.Outcome.Should().BeNull();
    }

    [Fact]
    public async Task IgnoresARedeliveryOfTheSameReceipt()
    {
        await _repository.AddAsync(Receipt("r-2", "hash-2"), TestContext.Current.CancellationToken);

        var second = await _repository.AddAsync(Receipt("r-2", "hash-2"), TestContext.Current.CancellationToken);

        second.Should().BeFalse();
        (await CountAsync("r-2")).Should().Be(1);
    }

    [Fact]
    public async Task KeepsTwoReceiptsOfTheSameBytesAsTwoRows()
    {
        await _repository.AddAsync(Receipt("r-3a", "same-hash"), TestContext.Current.CancellationToken);
        await _repository.AddAsync(Receipt("r-3b", "same-hash"), TestContext.Current.CancellationToken);

        (await CountByHashAsync("same-hash")).Should().Be(2);
    }

    [Fact]
    public async Task StoresTheReceivedAtToTheMillisecond()
    {
        await _repository.AddAsync(Receipt("r-4", "hash-4"), TestContext.Current.CancellationToken);

        // .123 is a value legacy DATETIME cannot hold; equality proves the parameter was typed.
        (await RowAsync("r-4")).ReceivedAt.Should().Be(ReceivedAt);
    }

    [Fact]
    public async Task ReportsNoRowWhenTheOutcomeArrivesFirst()
    {
        var recorded = await _repository.RecordOutcomeAsync("r-never", new ReceiptOutcome
        {
            MessageType = "343",
            Outcome = "processed",
            Interpreter = "MM343Strategy",
            ReadingsPublished = 2,
            ProcessedAt = ReceivedAt,
        }, TestContext.Current.CancellationToken);

        recorded.Should().BeFalse();
    }

    private static MarketMessage Receipt(string receiptId, string hash) => new()
    {
        ReceiptId = receiptId,
        Market = "roi-esb",
        Fuel = "electricity",
        FileName = "DSO_MIM_343_1782352213415_3805737.xml",
        ReceivedAt = ReceivedAt,
        Hash = hash,
        BlobPath = $"roi-esb/{hash}.xml",
        CorrelationId = receiptId,
    };

    private async Task<MarketMessage> RowAsync(string receiptId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<MarketMessage>(
            "SELECT * FROM dbo.MarketMessage WHERE ReceiptId = @receiptId", new { receiptId });
    }

    private async Task<int> CountAsync(string receiptId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.MarketMessage WHERE ReceiptId = @receiptId", new { receiptId });
    }

    private async Task<int> CountByHashAsync(string hash)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.MarketMessage WHERE Hash = @hash", new { hash });
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenRecordingAReceipt), MarketMessagingSchema.ApplyMarketMessagingSchemaAsync);
}
