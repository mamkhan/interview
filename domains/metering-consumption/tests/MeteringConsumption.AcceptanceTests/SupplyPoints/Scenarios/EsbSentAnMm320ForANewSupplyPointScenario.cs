using Dapper;
using MeteringConsumption.MarketMessaging;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.AcceptanceTests.SupplyPoints.Scenarios;

// Drops a built MM320 for a new MPRN into the drop container, then reads the file's receipt, the supply point's meter
// history and current rows and its current register rows in the domain database.
public sealed class EsbSentAnMm320ForANewSupplyPointScenario : IAsyncLifetime
{
    // The meters' rows in one of their two tables, MeterHistory or Meter.
    private static string MetersSql(string table) => $"""
        SELECT m.SerialNumber, m.EffectiveDate, m.MeterCategoryCode, m.MeterLocationCode
        FROM supply.{table} AS m
        JOIN supply.SupplyPoint AS s ON s.SupplyPointId = m.SupplyPointId
        WHERE s.XPRN = @Mprn
        ORDER BY m.SerialNumber, m.EffectiveDate;
        """;

    private const string RegistersSql = """
        SELECT r.RegisterKey, r.TimeslotCode, r.EffectiveDate, r.ReadingValue
        FROM supply.RegisterLevelInfo AS r
        JOIN supply.SupplyPoint AS s ON s.SupplyPointId = r.SupplyPointId
        WHERE s.XPRN = @Mprn
        ORDER BY r.RegisterKey, r.EffectiveDate;
        """;

    private TargetEnvironment _environment = null!;

    public string ReceiptHash { get; private set; } = "";

    public IReadOnlyList<MeterRow> MeterHistory { get; private set; } = [];

    public IReadOnlyList<MeterRow> Meters { get; private set; } = [];

    public IReadOnlyList<RegisterRow> Registers { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _environment = TargetEnvironment.Discover();

        // A new MPRN on every run keeps the history new, so it holds only what this run's file stated.
        var identity = Guid.NewGuid();
        var mprn = $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
        var fileName = $"DSO_MIM_320_{identity:N}_acceptance.xml";
        var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("320").WithMprn(mprn).BuildBytes();

        var drop = _environment.Blobs().GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
        await drop.GetBlobClient(fileName).UploadAsync(BinaryData.FromBytes(payload), overwrite: false, ct);

        ReceiptHash = (await Receipts.WaitForTheOutcomeAsync(_environment, fileName, ct)).Hash;

        // The meter and its registers are stored from one change in one transaction, so the first read that finds the
        // meter and both registers holds everything the file stated.
        (Meters, Registers) = await Polling.UntilAsync(
            token => ReadAsync(mprn, token),
            rows => rows.Meters.Count > 0 && rows.Registers.Count >= 2,
            rows => $"SupplyPoints holds {rows.Meters.Count} meter and {rows.Registers.Count} register rows for {mprn} from {fileName}.",
            ct);
        MeterHistory = await QueryAsync<MeterRow>(MetersSql("MeterHistory"), mprn, ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<(IReadOnlyList<MeterRow> Meters, IReadOnlyList<RegisterRow> Registers)> ReadAsync(string mprn,
        CancellationToken ct) =>
        (await QueryAsync<MeterRow>(MetersSql("Meter"), mprn, ct), await QueryAsync<RegisterRow>(RegistersSql, mprn, ct));

    private async Task<IReadOnlyList<TRow>> QueryAsync<TRow>(string sql, string mprn, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_environment.SqlConnectionString);
        return [.. await connection.QueryAsync<TRow>(new CommandDefinition(sql, new { Mprn = mprn }, cancellationToken: ct))];
    }

    public sealed record MeterRow
    {
        public required string SerialNumber { get; init; }

        public DateTime EffectiveDate { get; init; }

        public string? MeterCategoryCode { get; init; }

        public string? MeterLocationCode { get; init; }
    }

    public sealed record RegisterRow
    {
        public required string RegisterKey { get; init; }

        public string? TimeslotCode { get; init; }

        public DateTime EffectiveDate { get; init; }

        public decimal? ReadingValue { get; init; }
    }
}
