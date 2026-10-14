using AwesomeAssertions;
using BuildingBlocks.Persistence;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using MeteringConsumption.SupplyPoints.Infrastructure.Persistence.Repositories;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// Two units of work can look up one new supply point at once, as when a redelivery overlaps a run whose session lock
// expired. The first unit of work to look the supply point up holds it; the second waits until the first commits and
// then finds the supply point the first one added, instead of adding a second.
public sealed class WhenTwoMessagesForOneSupplyPointMeet
    : IClassFixture<WhenTwoMessagesForOneSupplyPointMeet.Database>, IAsyncDisposable
{
    private static readonly XPRN NewSupplyPoint = new("10000000201");

    private readonly string _connectionString;
    private readonly ServiceProvider _services;

    public WhenTwoMessagesForOneSupplyPointMeet(Database database)
    {
        _connectionString = database.ConnectionString;
        _services = new ServiceCollection().AddUnitOfWork(_connectionString).BuildServiceProvider();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task TheSecondWaitsForTheFirstAndFindsTheSupplyPointItAdded()
    {
        var token = TestContext.Current.CancellationToken;
        await using var firstScope = _services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await first.BeginAsync(token);
        (await new SupplyPointRepository(first).FindAsync(NewSupplyPoint, token)).Should().BeNull();

        var second = FindInItsOwnUnitOfWorkAsync(token);
        var waitedOnTheFirst = await BlockedBeforeItFinishesAsync(second, token);

        await new SupplyPointRepository(first).AddAsync(SupplyPoint.New(NewSupplyPoint, "roi-esb"), token);
        await first.CommitAsync(token);

        waitedOnTheFirst.Should().BeTrue();
        (await second).Should().NotBeNull();
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

    private async Task<SupplyPoint?> FindInItsOwnUnitOfWorkAsync(CancellationToken token)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(token);
        var found = await new SupplyPointRepository(unitOfWork).FindAsync(NewSupplyPoint, token);
        await unitOfWork.CommitAsync(token);
        return found;
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenTwoMessagesForOneSupplyPointMeet), SupplyPointSchema.ApplySupplyPointSchemaAsync);
}
