using MeteringConsumption.MarketMessaging.Domain;
using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.MarketMessaging.IntegrationTests;

// Every table a host running this module writes to, on a database built the way the migrator builds
// one. An earlier schema description created the outbox, the inbox and the client-request log and then
// dropped them, and no suite noticed because the suites described those tables themselves.
public sealed class WhenBuildingTheModuleSchema(WhenBuildingTheModuleSchema.Database database)
    : IClassFixture<WhenBuildingTheModuleSchema.Database>
{
    private readonly string _connectionString = database.ConnectionString;

    [Fact]
    public async Task HasTheModuleTableAndEveryTableItsUnitOfWorkNeeds()
    {
        var tables = await QueryAsync(
            "SELECT name FROM sys.tables WHERE name <> 'SchemaVersions' ORDER BY name;");

        tables.Should().Equal("ClientRequest", "MarketMessage", "OutboxMessage", "ProcessedMessage");
    }

    [Fact]
    public async Task IndexesTheOutboxByStateSoTheRelayReadsPendingRowsCheaply()
    {
        var indexes = await QueryAsync(
            "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('OutboxMessage') AND name IS NOT NULL ORDER BY name;");

        indexes.Should().Contain("IX_OutboxMessage_State");
    }

    private async Task<IReadOnlyList<string>> QueryAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        return (await connection.QueryAsync<string>(new CommandDefinition(sql,
            cancellationToken: TestContext.Current.CancellationToken))).ToArray();
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenBuildingTheModuleSchema), MarketMessagingSchema.ApplyMarketMessagingSchemaAsync);
}
