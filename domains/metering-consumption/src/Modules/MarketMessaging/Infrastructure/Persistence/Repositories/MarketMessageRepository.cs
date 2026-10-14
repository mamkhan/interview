using MeteringConsumption.MarketMessaging.Domain;
using System.Data;
using BuildingBlocks.Sql;
using Dapper;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Persistence.Repositories;

// A connection per call and no transaction: each write is one idempotent statement. Identifiers bind as
// varchar so the primary key index is used.
public sealed class MarketMessageRepository(string connectionString) : IMarketMessageRepository
{
    private const string InsertSql = """
        INSERT INTO dbo.MarketMessage
            (ReceiptId, Market, Fuel, FileName, ReceivedAt, Hash, BlobPath, CorrelationId)
        SELECT @ReceiptId, @Market, @Fuel, @FileName, @ReceivedAt, @Hash, @BlobPath, @CorrelationId
        WHERE NOT EXISTS (SELECT 1 FROM dbo.MarketMessage WHERE ReceiptId = @ReceiptId);
        """;

    private const string OutcomeSql = """
        UPDATE dbo.MarketMessage
        SET MessageType = @MessageType, Outcome = @Outcome, Interpreter = @Interpreter,
            ReadingsPublished = @ReadingsPublished, FailureReason = @FailureReason, ProcessedAt = @ProcessedAt
        WHERE ReceiptId = @ReceiptId;
        """;

    public async Task<bool> AddAsync(MarketMessage receipt, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ReceiptId", receipt.ReceiptId, DbType.AnsiString, size: 64);
        parameters.Add("Market", receipt.Market, DbType.AnsiString, size: 16);
        parameters.Add("Fuel", receipt.Fuel, DbType.AnsiString, size: 16);
        parameters.Add("FileName", receipt.FileName, DbType.String, size: 260);
        parameters.AddUtc("ReceivedAt", receipt.ReceivedAt);
        parameters.Add("Hash", receipt.Hash, DbType.AnsiString, size: 64);
        parameters.Add("BlobPath", receipt.BlobPath, DbType.AnsiString, size: 400);
        parameters.Add("CorrelationId", receipt.CorrelationId, DbType.AnsiString, size: 100);

        await using var connection = new SqlConnection(connectionString);
        var rows = await connection.ExecuteAsync(new CommandDefinition(InsertSql, parameters, cancellationToken: cancellationToken));
        return rows == 1;
    }

    public async Task<bool> RecordOutcomeAsync(string receiptId, ReceiptOutcome outcome, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ReceiptId", receiptId, DbType.AnsiString, size: 64);
        parameters.Add("MessageType", outcome.MessageType, DbType.AnsiString, size: 16);
        parameters.Add("Outcome", outcome.Outcome, DbType.AnsiString, size: 16);
        parameters.Add("Interpreter", outcome.Interpreter, DbType.AnsiString, size: 200);
        parameters.Add("ReadingsPublished", outcome.ReadingsPublished, DbType.Int32);
        parameters.Add("FailureReason", outcome.FailureReason, DbType.String, size: 1000);
        parameters.AddUtc("ProcessedAt", outcome.ProcessedAt);

        await using var connection = new SqlConnection(connectionString);
        var rows = await connection.ExecuteAsync(new CommandDefinition(OutcomeSql, parameters, cancellationToken: cancellationToken));
        return rows == 1;
    }
}
