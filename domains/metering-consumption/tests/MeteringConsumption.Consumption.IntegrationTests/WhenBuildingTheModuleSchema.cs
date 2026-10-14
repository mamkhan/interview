using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.IntegrationTests;

// Every table a host running this module writes to, on a database built the way the migrator builds one.
public sealed class WhenBuildingTheModuleSchema(WhenBuildingTheModuleSchema.Database database)
    : IClassFixture<WhenBuildingTheModuleSchema.Database>
{
    private readonly string _connectionString = database.ConnectionString;

    [Fact]
    public async Task HasTheProfileTableAndEveryTableItsUnitOfWorkNeeds()
    {
        var tables = await QueryAsync("SELECT name FROM sys.tables WHERE name <> 'SchemaVersions' ORDER BY name;");

        tables.Should().Equal(
            "ClientRequest",
            "ConsumptionForecast",
            "OutboxMessage",
            "ProcessedMessage",
            "SupplyPointProfile");
    }

    [Fact]
    public async Task KeysAForecastOnItsSupplyPointAndVersion()
    {
        var key = await QueryAsync("""
            SELECT c.name FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.is_primary_key = 1 AND ic.object_id = OBJECT_ID('dbo.ConsumptionForecast')
            ORDER BY ic.key_ordinal;
            """);

        key.Should().Equal("SupplyPointId", "ForecastVersion");
    }

    [Fact]
    public async Task AllowsOneCurrentForecastAndOneVersionPerRecordedReading()
    {
        var unique = await QueryAsync("""
            SELECT CONCAT(i.name, ':', ISNULL(i.filter_definition, '')) FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID('dbo.ConsumptionForecast') AND i.is_unique = 1 AND i.is_primary_key = 0
            ORDER BY i.name;
            """);

        unique.Should().Equal(
            "UQ_ConsumptionForecast_SupplyPointId_Current:([IsCurrent]=(1))",
            "UQ_ConsumptionForecast_SupplyPointId_TriggerLedgerEntryId:");
    }

    [Fact]
    public async Task LeavesTheReferenceDataTablesToTheirOwnDatabase()
    {
        var tables = await QueryAsync(
            "SELECT name FROM sys.tables WHERE name IN ('LoadProfileHourlyValue', 'AnnualAverageUsage');");

        tables.Should().BeEmpty();
    }

    [Fact]
    public async Task KeysTheProfileOnTheSupplyPoint()
    {
        var key = await QueryAsync("""
            SELECT c.name FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.is_primary_key = 1 AND ic.object_id = OBJECT_ID('dbo.SupplyPointProfile');
            """);

        key.Should().Equal("SupplyPointId");
    }

    [Fact]
    public async Task StoresUsageFactorsWithTenDecimals()
    {
        var types = await QueryAsync("""
            SELECT DISTINCT CONCAT(TYPE_NAME(system_type_id), '(', precision, ',', scale, ')')
            FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo.SupplyPointProfile') AND name LIKE '_uf___' ;
            """);

        types.Should().Equal("decimal(20,10)");
    }

    private async Task<IReadOnlyList<string>> QueryAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        return (await connection.QueryAsync<string>(new CommandDefinition(sql,
            cancellationToken: TestContext.Current.CancellationToken))).ToArray();
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenBuildingTheModuleSchema), ConsumptionSchema.ApplyConsumptionSchemaAsync);
}
