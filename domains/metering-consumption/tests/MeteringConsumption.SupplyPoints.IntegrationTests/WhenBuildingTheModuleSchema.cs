using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// Every table a host running this module writes to, on a database built the way the migrator builds one.
public sealed class WhenBuildingTheModuleSchema(WhenBuildingTheModuleSchema.Database database)
    : IClassFixture<WhenBuildingTheModuleSchema.Database>
{
    private readonly string _connectionString = database.ConnectionString;

    [Fact]
    public async Task HasAHistoryAndACurrentTablePerKindAndEveryTableItsUnitOfWorkNeeds()
    {
        var tables = await QueryAsync("""
            SELECT SCHEMA_NAME(schema_id) + '.' + name FROM sys.tables WHERE name <> 'SchemaVersions'
            ORDER BY SCHEMA_NAME(schema_id), name;
            """);

        tables.Should().Equal(
            "dbo.ClientRequest",
            "dbo.OutboxMessage",
            "dbo.ProcessedMessage",
            "supply.Meter",
            "supply.MeterHistory",
            "supply.MeterPoint",
            "supply.MeterPointHistory",
            "supply.RegisterLevelInfo",
            "supply.RegisterLevelInfoHistory",
            "supply.SupplyPoint",
            "supply.UsageFactor",
            "supply.UsageFactorHistory");
    }

    // Of the message a row came from, only the meter point keeps anything: its networks reference number.
    [Fact]
    public async Task KeepsOnlyTheMeterPointsNetworksReferenceNumberOfTheMessage()
    {
        var columns = await QueryAsync("""
            SELECT TABLE_NAME + '.' + COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'supply'
              AND COLUMN_NAME IN ('NetworksReferenceNumber', 'SourceMessageType', 'MarketTimestamp', 'ContentHash')
            ORDER BY TABLE_NAME;
            """);

        columns.Should().Equal("MeterPoint.NetworksReferenceNumber", "MeterPointHistory.NetworksReferenceNumber");
    }

    // Each test uses its own XPRN: the class shares one database.
    [Fact]
    public async Task KeepsOneSupplyPointPerXprn()
    {
        await InsertSupplyPointAsync("10000000002");

        var second = () => InsertSupplyPointAsync("10000000002");

        (await second.Should().ThrowAsync<SqlException>()).Which.Number.Should().BeOneOf(2601, 2627);
    }

    // The same key on another date, and another key on the same date, are no conflict; only the key and date repeated is.
    [Theory]
    [InlineData("10000000011", "MeterPoint", "", "")]
    [InlineData("10000000012", "MeterPointHistory", "", "")]
    [InlineData("10000000013", "UsageFactor", "[TimeslotCode], ", "'24H', ")]
    [InlineData("10000000014", "UsageFactorHistory", "[TimeslotCode], ", "'24H', ")]
    [InlineData("10000000015", "Meter", "[SerialNumber], ", "'90003311', ")]
    [InlineData("10000000016", "MeterHistory", "[SerialNumber], ", "'90003311', ")]
    [InlineData("10000000017", "RegisterLevelInfo", "[SerialNumber], [RegisterKey], [RegisterTypeCode], [UOM_Code], ",
        "'90000320', '001', '78', 'KWH', ")]
    [InlineData("10000000018", "RegisterLevelInfoHistory", "[SerialNumber], [RegisterKey], [RegisterTypeCode], [UOM_Code], ",
        "'90000320', '001', '78', 'KWH', ")]
    public async Task KeepsOneRowPerKeyAndEffectiveDate(string xprn, string table, string keyColumns, string keyValues)
    {
        await InsertSupplyPointAsync(xprn);
        await InsertAsync(xprn, table, keyColumns, keyValues, "2026-03-01");
        await InsertAsync(xprn, table, keyColumns, keyValues, "2026-06-15");

        var second = () => InsertAsync(xprn, table, keyColumns, keyValues, "2026-03-01");

        (await second.Should().ThrowAsync<SqlException>()).Which.Number.Should().BeOneOf(2601, 2627);
    }

    // ESB states usage factors with up to eight decimals (XSD 14.00.00); none may be rounded away in either table.
    [Theory]
    [InlineData("10000000021", "UsageFactor")]
    [InlineData("10000000022", "UsageFactorHistory")]
    public async Task StoresUsageFactorsWithEightDecimals(string xprn, string table)
    {
        await InsertSupplyPointAsync(xprn);
        await ExecuteAsync($"""
            INSERT INTO [supply].[{table}] ([SupplyPointId], [TimeslotCode], [EffectiveDate], [EstimatedUsageFactor])
            SELECT [SupplyPointId], '24H', '2026-03-01', 4127.12345678 FROM [supply].[SupplyPoint] WHERE [XPRN] = '{xprn}';
            """);

        var stored = await QueryAsync($"""
            SELECT CONVERT(varchar(32), [EstimatedUsageFactor]) FROM [supply].[{table}]
            WHERE [SupplyPointId] = (SELECT [SupplyPointId] FROM [supply].[SupplyPoint] WHERE [XPRN] = '{xprn}');
            """);

        stored.Should().Equal("4127.12345678");
    }

    private Task InsertAsync(string xprn, string table, string keyColumns, string keyValues, string effectiveDate) => ExecuteAsync($"""
        INSERT INTO [supply].[{table}] ([SupplyPointId], {keyColumns}[EffectiveDate])
        SELECT [SupplyPointId], {keyValues}'{effectiveDate}' FROM [supply].[SupplyPoint] WHERE [XPRN] = '{xprn}';
        """);

    private Task InsertSupplyPointAsync(string xprn) =>
        ExecuteAsync($"INSERT INTO [supply].[SupplyPoint] ([XPRN], [Market]) VALUES ('{xprn}', 'roi-esb');");

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: TestContext.Current.CancellationToken));
    }

    private async Task<IReadOnlyList<string>> QueryAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        return (await connection.QueryAsync<string>(new CommandDefinition(sql,
            cancellationToken: TestContext.Current.CancellationToken))).ToArray();
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenBuildingTheModuleSchema), SupplyPointSchema.ApplySupplyPointSchemaAsync);
}
