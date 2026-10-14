using Dapper;
using MeteringConsumption.MarketMessaging;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.AcceptanceTests.SupplyPoints.Scenarios;

// Drops a built MM332 for a new MPRN into the drop container, then reads the file's receipt, the supply point's history
// and current rows in SupplyPoints and the supply point's row in Consumption's Profile, which the same file feeds.
public sealed class EsbSentAnMm332ForANewSupplyPointScenario : IAsyncLifetime
{
    private const string SupplyPointSql = "SELECT XPRN, Market FROM supply.SupplyPoint WHERE XPRN = @Mprn;";

    // The meter point's rows in one of its two tables, MeterPointHistory or MeterPoint.
    public static string MeterPointsSql(string table) => $"""
        SELECT m.EffectiveDate, m.MeterConfigurationCode, m.LoadProfileCode, m.DuosGroup, m.MeterPointStatusCode,
               m.NetworksReferenceNumber
        FROM supply.{table} AS m
        JOIN supply.SupplyPoint AS s ON s.SupplyPointId = m.SupplyPointId
        WHERE s.XPRN = @Mprn
        ORDER BY m.EffectiveDate;
        """;

    // The usage factors' rows in one of their two tables, UsageFactorHistory or UsageFactor.
    public static string UsageFactorsSql(string table) => $"""
        SELECT u.TimeslotCode, u.EffectiveDate, u.EstimatedUsageFactor, u.ActualUsageFactor
        FROM supply.{table} AS u
        JOIN supply.SupplyPoint AS s ON s.SupplyPointId = u.SupplyPointId
        WHERE s.XPRN = @Mprn
        ORDER BY u.TimeslotCode, u.EffectiveDate;
        """;

    private const string ProfileSql = """
        SELECT MeterConfigurationCode, LoadProfileCode, DuosGroup, MeterPointStatusCode, ConfigurationEffectiveFrom,
               ConfigurationSource, Euf00D, Euf00DSource, Auf00D, Euf00N
        FROM dbo.SupplyPointProfile
        WHERE SupplyPointId = @Mprn;
        """;

    private TargetEnvironment _environment = null!;

    public string ReceiptHash { get; private set; } = "";

    public SupplyPointRow SupplyPoint { get; private set; } = null!;

    public IReadOnlyList<MeterPointRow> MeterPointHistory { get; private set; } = [];

    public IReadOnlyList<MeterPointRow> MeterPoints { get; private set; } = [];

    public IReadOnlyList<UsageFactorRow> UsageFactorHistory { get; private set; } = [];

    public IReadOnlyList<UsageFactorRow> UsageFactors { get; private set; } = [];

    public ProfileRow Profile { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _environment = TargetEnvironment.Discover();

        // A new MPRN on every run keeps the history new, so it holds only what this run's file stated.
        var identity = Guid.NewGuid();
        var mprn = $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
        var fileName = $"DSO_MIM_332_{identity:N}_acceptance.xml";
        var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("332").WithMprn(mprn).BuildBytes();

        var drop = _environment.Blobs().GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
        await drop.GetBlobClient(fileName).UploadAsync(BinaryData.FromBytes(payload), overwrite: false, ct);

        ReceiptHash = (await Receipts.WaitForTheOutcomeAsync(_environment, fileName, ct)).Hash;

        // Store writes the meter point and the usage factors from one change in one transaction, so the first read that
        // finds both holds everything the file stated.
        (SupplyPoint, MeterPoints, UsageFactors) = await Polling.UntilAsync(
            token => ReadSupplyPointAsync(mprn, token),
            rows => rows.SupplyPoint is not null && rows.MeterPoints.Count > 0 && rows.UsageFactors.Count > 0,
            rows => $"SupplyPoints holds {rows.MeterPoints.Count} current meter point and {rows.UsageFactors.Count} current " +
                    $"usage factor rows for {mprn} from {fileName}.",
            ct);
        MeterPointHistory = await QueryAsync<MeterPointRow>(MeterPointsSql("MeterPointHistory"), mprn, ct);
        UsageFactorHistory = await QueryAsync<UsageFactorRow>(UsageFactorsSql("UsageFactorHistory"), mprn, ct);

        // The Profile saves the configuration and the usage factors from two events, each in its own transaction.
        Profile = (await Polling.UntilAsync(
            token => QueryAsync<ProfileRow>(ProfileSql, mprn, token),
            rows => rows.SingleOrDefault() is { MeterConfigurationCode: not null, Euf00D: not null },
            _ => $"the Profile row for {mprn} does not hold both the configuration and the usage factors of {fileName}.",
            ct)).Single();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<(SupplyPointRow SupplyPoint, IReadOnlyList<MeterPointRow> MeterPoints, IReadOnlyList<UsageFactorRow> UsageFactors)>
        ReadSupplyPointAsync(string mprn, CancellationToken ct) =>
        ((await QueryAsync<SupplyPointRow>(SupplyPointSql, mprn, ct)).SingleOrDefault()!,
            await QueryAsync<MeterPointRow>(MeterPointsSql("MeterPoint"), mprn, ct),
            await QueryAsync<UsageFactorRow>(UsageFactorsSql("UsageFactor"), mprn, ct));

    private async Task<IReadOnlyList<TRow>> QueryAsync<TRow>(string sql, string mprn, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_environment.SqlConnectionString);
        return [.. await connection.QueryAsync<TRow>(new CommandDefinition(sql, new { Mprn = mprn }, cancellationToken: ct))];
    }

    public sealed record SupplyPointRow
    {
        public required string XPRN { get; init; }

        public required string Market { get; init; }
    }

    public sealed record MeterPointRow
    {
        public DateTime EffectiveDate { get; init; }

        public string? MeterConfigurationCode { get; init; }

        public string? LoadProfileCode { get; init; }

        public string? DuosGroup { get; init; }

        public string? MeterPointStatusCode { get; init; }

        public string? NetworksReferenceNumber { get; init; }
    }

    public sealed record UsageFactorRow
    {
        public required string TimeslotCode { get; init; }

        public DateTime EffectiveDate { get; init; }

        public decimal? EstimatedUsageFactor { get; init; }

        public decimal? ActualUsageFactor { get; init; }
    }

    public sealed record ProfileRow
    {
        public string? MeterConfigurationCode { get; init; }

        public string? LoadProfileCode { get; init; }

        public string? DuosGroup { get; init; }

        public string? MeterPointStatusCode { get; init; }

        public DateTime? ConfigurationEffectiveFrom { get; init; }

        public string? ConfigurationSource { get; init; }

        public decimal? Euf00D { get; init; }

        public string? Euf00DSource { get; init; }

        public decimal? Auf00D { get; init; }

        public decimal? Euf00N { get; init; }
    }
}
