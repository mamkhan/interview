using Dapper;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.AcceptanceTests;

// The receipt Market Messaging keeps for each file put in the drop container, read back by the file's name.
public static class Receipts
{
    private const string ReceiptSql = """
        SELECT ReceiptId, FileName, Market, Fuel, Hash, BlobPath, MessageType, Outcome, ReadingsPublished, FailureReason
        FROM dbo.MarketMessage
        WHERE FileName = @FileName;
        """;

    // A file crosses Event Grid and two hosts before its outcome is recorded, so a missing row and a row without an
    // outcome name different broken hops.
    public static Task<ReceiptRow> WaitForTheOutcomeAsync(TargetEnvironment environment, string fileName,
        CancellationToken ct) =>
        Polling.UntilAsync<ReceiptRow?>(
            token => FindAsync(environment, fileName, token),
            row => row?.Outcome is not null,
            row => row is null
                ? $"no receipt for {fileName}, so the file was never sealed or never announced."
                : $"the receipt for {fileName} has no outcome, so the file was sealed but never processed.",
            ct)!;

    public static async Task<ReceiptRow?> FindAsync(TargetEnvironment environment, string fileName, CancellationToken ct)
    {
        await using var connection = new SqlConnection(environment.SqlConnectionString);
        return await connection.QuerySingleOrDefaultAsync<ReceiptRow>(
            new CommandDefinition(ReceiptSql, new { FileName = fileName }, cancellationToken: ct));
    }

    public sealed record ReceiptRow
    {
        public required string ReceiptId { get; init; }

        public required string FileName { get; init; }

        public required string Market { get; init; }

        public required string Fuel { get; init; }

        public required string Hash { get; init; }

        public required string BlobPath { get; init; }

        public string? MessageType { get; init; }

        public string? Outcome { get; init; }

        public int? ReadingsPublished { get; init; }

        public string? FailureReason { get; init; }
    }
}
