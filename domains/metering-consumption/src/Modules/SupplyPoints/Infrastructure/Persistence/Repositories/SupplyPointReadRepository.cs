using BuildingBlocks.Sql;
using Dapper;
using MeteringConsumption.SupplyPoints.Application.Features.Provide.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Provide.DTO;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.SupplyPoints.Infrastructure.Persistence.Repositories;

// Its own connection per read and no lock on the supply point's row, so a read never waits on Store or holds it up.
public sealed class SupplyPointReadRepository(string connectionString) : ISupplyPointReader
{
    private static readonly string ReadSql =
        "SELECT COUNT(*) FROM [supply].[SupplyPoint] WHERE [XPRN] = @Xprn;\n" +
        SupplyPointRepository.MeterPoints.OnDaySql + "\n" +
        SupplyPointRepository.UsageFactors.OnDaySql;

    public async Task<SupplyPointOnDay?> ReadAsync(XPRN xprn, DateOnly day, CancellationToken cancellationToken)
    {
        var parameters = SupplyPointRepository.XprnParameter(xprn);
        parameters.AddDate("Day", day);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(ReadSql, parameters,
            cancellationToken: cancellationToken));
        if (await results.ReadSingleAsync<int>() == 0)
        {
            return null;
        }

        var meterPoints = SupplyPointRepository.MeterPoints;
        var meterPoint = new KeyOnDay<MeterPointValues>(
            meterPoints.Rows(await results.ReadAsync()), meterPoints.Rows(await results.ReadAsync()));

        var usageFactors = SupplyPointRepository.UsageFactors;
        var current = ByTimeslot(await results.ReadAsync());
        var history = ByTimeslot(await results.ReadAsync());
        return new SupplyPointOnDay(meterPoint, current.Keys.Union(history.Keys).ToDictionary(
            timeslotCode => timeslotCode,
            timeslotCode => new KeyOnDay<UsageFactorValues>(
                [.. current.GetValueOrDefault(timeslotCode, []).Select(usageFactors.Row)],
                [.. history.GetValueOrDefault(timeslotCode, []).Select(usageFactors.Row)])));
    }

    private static Dictionary<string, List<IDictionary<string, object>>> ByTimeslot(IEnumerable<dynamic> rows) =>
        rows.Cast<IDictionary<string, object>>()
            .GroupBy(row => (string)row["TimeslotCode"])
            .ToDictionary(timeslot => timeslot.Key, timeslot => timeslot.ToList());
}
