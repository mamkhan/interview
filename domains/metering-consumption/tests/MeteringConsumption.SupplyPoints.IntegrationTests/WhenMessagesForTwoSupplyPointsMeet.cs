using AwesomeAssertions;
using BuildingBlocks.Persistence;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using MeteringConsumption.SupplyPoints.Infrastructure.Persistence.Repositories;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// Store's consumer stores messages for different supply points at once. Ten supply points are stored first, as a young
// database holds. A message for a new supply point then writes rows of every kind and is not yet committed when a
// message for one of the ten is stored: that one reads and writes only its own supply point's rows, so it never waits
// on the first.
public sealed class WhenMessagesForTwoSupplyPointsMeet : IClassFixture<WhenMessagesForTwoSupplyPointsMeet.Database>, IAsyncDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly string _connectionString;
    private readonly ServiceProvider _services;

    public WhenMessagesForTwoSupplyPointsMeet(Database database)
    {
        _connectionString = database.ConnectionString;
        _services = new ServiceCollection().AddUnitOfWork(_connectionString).BuildServiceProvider();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task TheSecondNeverWaitsForTheFirst()
    {
        var token = TestContext.Current.CancellationToken;
        for (var index = 0; index < 10; index++)
        {
            await using var seedScope = _services.CreateAsyncScope();
            await StoreAMessageAsync(seedScope, new XPRN($"1000000{index:D4}"), commit: true, token);
        }

        await using var firstScope = _services.CreateAsyncScope();
        await StoreAMessageAsync(firstScope, new XPRN("10000000500"), commit: false, token);

        var second = StoreAMessageInItsOwnUnitOfWorkAsync(new XPRN("10000000005"), token);
        var waitedOnTheFirst = await BlockedBeforeItFinishesAsync(second, token);

        await firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(token);
        await second;
        waitedOnTheFirst.Should().BeFalse();
    }

    private async Task StoreAMessageInItsOwnUnitOfWorkAsync(XPRN xprn, CancellationToken token)
    {
        await using var scope = _services.CreateAsyncScope();
        await StoreAMessageAsync(scope, xprn, commit: true, token);
    }

    // The meter point, two usage factors, a meter and a register, in the order the handler writes them.
    private static async Task StoreAMessageAsync(AsyncServiceScope scope, XPRN xprn, bool commit, CancellationToken token)
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(token);
        var repository = new SupplyPointRepository(unitOfWork);
        if (await repository.FindAsync(xprn, token) is null)
        {
            await repository.AddAsync(SupplyPoint.New(xprn, "roi-esb"), token);
        }

        async Task RecordAsync<TValues>(ConfigurationKey<TValues> key, Dated<TValues> statement)
            where TValues : class, ICarriedValues<TValues> =>
            await repository.SaveAsync(key, Recording.Decide(statement, Today,
                await repository.FactsAsync(key, statement.EffectiveDate, token)), token);

        var day = new DateOnly(2026, 9, 25).AddDays(commit ? 0 : 1);
        await RecordAsync(new MeterPointKey(xprn), new MeterPointStatementBuilder().StartingOn(day).Build());
        await RecordAsync(new UsageFactorKey(xprn, "24H"), new SupplyPointUsageFactorStatementBuilder().StartingOn(day)
            .WithValues(5033.03824582m, null).Build());
        await RecordAsync(new UsageFactorKey(xprn, "24H"), new SupplyPointUsageFactorStatementBuilder().StartingOn(new DateOnly(2026, 7, 30))
            .WithValues(null, 4276.94863062m).Build());
        await RecordAsync(new MeterKey(xprn, "90003322"), new MeterStatementBuilder().StartingOn(day).Build());
        await RecordAsync(new RegisterKey(xprn, new RegisterId("90003322", "001")), new RegisterStatementBuilder().StartingOn(day).Build());
        if (commit)
        {
            await unitOfWork.CommitAsync(token);
        }
    }

    // The server's own view of the wait, rather than time elapsed: a request on this database blocked by another
    // session, seen while the second unit of work has not finished.
    private async Task<bool> BlockedBeforeItFinishesAsync(Task second, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!second.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            var blocked = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id <> 0 AND database_id = DB_ID();",
                cancellationToken: token));
            if (blocked > 0)
            {
                return !second.IsCompleted;
            }

            await Task.Delay(50, token);
        }

        return false;
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenMessagesForTwoSupplyPointsMeet), SupplyPointSchema.ApplySupplyPointSchemaAsync);
}
