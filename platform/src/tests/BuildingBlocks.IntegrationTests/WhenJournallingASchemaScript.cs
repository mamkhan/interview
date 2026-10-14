using AwesomeAssertions;
using BuildingBlocks.Persistence;
using BuildingBlocks.TestsShared;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.IntegrationTests;

// DbUp decides a script has run by its journal name alone, so the name is the file name without the
// folder it sits in: a script moved to another folder is still the script that ran. Frozen literals are
// the file names under Scripts/ in this assembly and in BuildingBlocks.Persistence.
public sealed class WhenJournallingASchemaScript
{
    [Fact]
    public async Task RecordsEachScriptUnderItsFileNameAlone()
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, $"{nameof(WhenJournallingASchemaScript)}-names",
            new SuiteResources { Migrate = PlatformSchema.CreateAsync }, TestContext.Current.CancellationToken);

        (await JournalAsync(scope.SqlConnectionString!))
            .Should().Equal("Platform-00001 - Platform tables.sql", "Suite-00001 - Written row.sql");
    }

    [Fact]
    public async Task DoesNotRunAScriptTheJournalAlreadyHoldsUnderItsFileName()
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, $"{nameof(WhenJournallingASchemaScript)}-moved",
            new SuiteResources { Migrate = PlatformOnlyAsync }, TestContext.Current.CancellationToken);
        await using (var connection = new SqlConnection(scope.SqlConnectionString))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.SchemaVersions (ScriptName, Applied) VALUES ('Suite-00001 - Written row.sql', SYSUTCDATETIME());",
                cancellationToken: TestContext.Current.CancellationToken));
        }

        await PlatformSchema.CreateAsync(scope.SqlConnectionString!);

        await using var check = new SqlConnection(scope.SqlConnectionString);
        (await check.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'WrittenRow';",
            cancellationToken: TestContext.Current.CancellationToken))).Should().Be(0);
    }

    private static Task PlatformOnlyAsync(string connectionString)
    {
        SchemaMigrator.Apply(connectionString, typeof(SchemaMigrator).Assembly, "Platform");
        return Task.CompletedTask;
    }

    private static async Task<IReadOnlyList<string>> JournalAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        return (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT ScriptName FROM dbo.SchemaVersions ORDER BY ScriptName;",
            cancellationToken: TestContext.Current.CancellationToken))).ToArray();
    }
}
