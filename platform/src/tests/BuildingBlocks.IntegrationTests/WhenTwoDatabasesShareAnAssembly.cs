using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.IntegrationTests;

// A module that owns two databases embeds both sets of scripts in its one assembly; each database gets the
// platform tables and its own prefix's scripts, never the other's. Frozen literals are the file and table names
// under Scripts/ in this assembly and in BuildingBlocks.Persistence.
public sealed class WhenTwoDatabasesShareAnAssembly
{
    [Fact]
    public async Task AppliesOnlyTheScriptsOfTheDatabaseAskedFor()
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenTwoDatabasesShareAnAssembly),
            new SuiteResources { Migrate = PlatformSchema.CreateSiblingAsync }, TestContext.Current.CancellationToken);

        await using var connection = new SqlConnection(scope.SqlConnectionString);
        (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT ScriptName FROM dbo.SchemaVersions ORDER BY ScriptName;",
            cancellationToken: TestContext.Current.CancellationToken)))
            .Should().Equal("Platform-00001 - Platform tables.sql", "Sibling-00001 - Sibling row.sql");
        (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM sys.tables WHERE name IN ('SiblingRow', 'WrittenRow') ORDER BY name;",
            cancellationToken: TestContext.Current.CancellationToken)))
            .Should().Equal("SiblingRow");
    }
}
