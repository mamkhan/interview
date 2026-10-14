using Dapper;
using MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;

// Reads only the tariff values and smart meter flag of the Profile's row; the Profile alone writes them.
public sealed class SupplyPointTariffReadRepository(string connectionString) : ISupplyPointTariffReader
{
    public async Task<TariffInEffect?> FindAsync(string supplyPointId, CancellationToken cancellationToken)
    {
        var findSql = $"""
            SELECT IsNsmp, IsTou, TariffCalcType, EffectiveLoadProfileCode, TariffAsOf, TariffSource,
                   SmartMeterFlagAsOf, SmartMeterFlagSource
            FROM dbo.SupplyPointProfile
            WHERE SupplyPointId = '{supplyPointId}';
            """;

        await using var connection = new SqlConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<TariffRow>(
            new CommandDefinition(findSql, cancellationToken: cancellationToken));
        return row is null
            ? null
            : new TariffInEffect(
                new TariffFacts(row.IsNsmp, row.IsTou, row.TariffCalcType, row.EffectiveLoadProfileCode),
                row.TariffAsOf,
                row.TariffSource,
                row.SmartMeterFlagAsOf,
                row.SmartMeterFlagSource);
    }

    private sealed record TariffRow(
        bool? IsNsmp,
        bool? IsTou,
        string? TariffCalcType,
        string? EffectiveLoadProfileCode,
        DateTimeOffset? TariffAsOf,
        string? TariffSource,
        DateTimeOffset? SmartMeterFlagAsOf,
        string? SmartMeterFlagSource);
}
