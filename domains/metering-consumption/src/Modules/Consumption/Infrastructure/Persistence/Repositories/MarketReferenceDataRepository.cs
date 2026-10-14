using System.Data;
using BuildingBlocks.Sql;
using Dapper;
using MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;

// Reads the reference-data database, which another process writes; this module never writes to it.
public sealed class MarketReferenceDataRepository(string connectionString) : IMarketReferenceData
{
    private const string CurveSql = """
        SELECT ValueDate, Hour, H24, D00, N00
        FROM dbo.LoadProfileHourlyValue
        WHERE LoadProfileCode = @LoadProfileCode AND ValueDate BETWEEN @First AND @Last;
        """;

    private const string AnnualAverageSql = """
        SELECT Aau24H, Aau00D, Aau00N
        FROM dbo.AnnualAverageUsage
        WHERE DuosGroup = @DuosGroup AND [Year] = @Year;
        """;

    public async Task<LoadProfileCurve> CurveAsync(
        string loadProfileCode, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("LoadProfileCode", loadProfileCode, DbType.AnsiString, size: 8);
        parameters.AddDate("First", first);
        parameters.AddDate("Last", last);

        await using var connection = new SqlConnection(connectionString);
        var rows = await connection.QueryAsync<HourRow>(
            new CommandDefinition(CurveSql, parameters, cancellationToken: cancellationToken));
        return new LoadProfileCurve(loadProfileCode, rows.Select(row =>
            new LoadProfileHour(DateOnly.FromDateTime(row.ValueDate), row.Hour, row.H24, row.D00, row.N00)));
    }

    public async Task<AnnualAverageUsage?> AnnualAverageAsync(string duosGroup, int year, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("DuosGroup", duosGroup, DbType.AnsiString, size: 8);
        parameters.Add("Year", (short)year, DbType.Int16);

        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<AnnualAverageUsage>(
            new CommandDefinition(AnnualAverageSql, parameters, cancellationToken: cancellationToken));
    }

    private sealed record HourRow(DateTime ValueDate, byte Hour, decimal? H24, decimal? D00, decimal? N00);
}
