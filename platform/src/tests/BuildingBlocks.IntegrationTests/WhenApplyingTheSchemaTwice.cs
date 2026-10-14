using AwesomeAssertions;
using BuildingBlocks.Persistence;
using BuildingBlocks.TestsShared;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.IntegrationTests;

// A host in Development applies the schema on every start, so the second start has to be a no-op: the
// journal decides what runs, and a script that has run does not run again.
public sealed class WhenApplyingTheSchemaTwice(WhenApplyingTheSchemaTwice.Database database)
    : IClassFixture<WhenApplyingTheSchemaTwice.Database>
{
    private readonly string _connectionString = database.ConnectionString;

    [Fact]
    public void ReportsNothingPendingOnceTheScriptsHaveRun()
    {
        SchemaMigrator.Pending(_connectionString, typeof(PlatformSchema).Assembly, PlatformSchema.ScriptPrefix,
            PlatformSchema.SiblingPrefix).Should().BeEmpty();
    }

    // What the deployed boot check reads: a database the platform scripts have reached but this
    // assembly's have not is a host that must refuse to start, not one that quietly runs old code.
    [Fact]
    public async Task NamesTheScriptsADatabaseHasNotSeen()
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, $"{nameof(WhenApplyingTheSchemaTwice)}-partial",
            new SuiteResources { Migrate = PlatformOnlyAsync }, TestContext.Current.CancellationToken);

        SchemaMigrator.Pending(scope.SqlConnectionString!, typeof(PlatformSchema).Assembly, PlatformSchema.ScriptPrefix,
            PlatformSchema.SiblingPrefix)
            .Should().ContainSingle().Which.Should().Be("Suite-00001 - Written row.sql");
    }

    [Fact]
    public async Task RunsNoScriptAgainAndLeavesTheJournalAsItWas()
    {
        var before = await JournalAsync();

        await PlatformSchema.CreateAsync(_connectionString);

        (await JournalAsync()).Should().Equal(before);
    }

    private static Task PlatformOnlyAsync(string connectionString)
    {
        SchemaMigrator.Apply(connectionString, typeof(SchemaMigrator).Assembly, "Platform");
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<string>> JournalAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        return (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT CONCAT(ScriptName, ' @', CONVERT(varchar(30), Applied, 126)) FROM dbo.SchemaVersions ORDER BY ScriptName;",
            cancellationToken: TestContext.Current.CancellationToken))).ToArray();
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenApplyingTheSchemaTwice), PlatformSchema.CreateAsync);
}
