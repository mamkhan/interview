using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The reference data a forecast reads, on a database built the way the migrator builds the reference-data
// database: its two tables and the platform's, none of the module's own.
public sealed class WhenBuildingTheReferenceDataSchema : IAsyncLifetime
{
    private string _connectionString = null!;

    public async ValueTask InitializeAsync()
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenBuildingTheReferenceDataSchema),
            new SuiteResources { Migrate = ReferenceDataSchema.ApplyReferenceDataSchemaAsync },
            TestContext.Current.CancellationToken);
        _connectionString = scope.SqlConnectionString!;
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task HasTheLoadProfileAndAnnualAverageTablesAndNoneOfTheModules()
    {
        var tables = await QueryAsync("SELECT name FROM sys.tables WHERE name <> 'SchemaVersions' ORDER BY name;");

        tables.Should().Equal(
            "AnnualAverageUsage",
            "ClientRequest",
            "LoadProfileHourlyValue",
            "OutboxMessage",
            "ProcessedMessage");
    }

    [Fact]
    public async Task KeysAnHourlyValueOnItsProfileDateAndHour()
    {
        var key = await QueryAsync("""
            SELECT c.name FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.is_primary_key = 1 AND ic.object_id = OBJECT_ID('dbo.LoadProfileHourlyValue')
            ORDER BY ic.key_ordinal;
            """);

        key.Should().Equal("LoadProfileCode", "ValueDate", "Hour");
    }

    [Fact]
    public async Task StoresHourlyValuesWithTenDecimals()
    {
        var types = await QueryAsync("""
            SELECT DISTINCT CONCAT(TYPE_NAME(system_type_id), '(', precision, ',', scale, ')')
            FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo.LoadProfileHourlyValue') AND name IN ('H24', 'D00', 'N00');
            """);

        types.Should().Equal("decimal(12,10)");
    }

    private async Task<IReadOnlyList<string>> QueryAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        return (await connection.QueryAsync<string>(new CommandDefinition(sql,
            cancellationToken: TestContext.Current.CancellationToken))).ToArray();
    }
}
