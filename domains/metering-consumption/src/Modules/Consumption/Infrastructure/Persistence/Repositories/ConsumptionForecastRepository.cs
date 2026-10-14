using System.Data;
using System.Globalization;
using System.Text.Json;
using BuildingBlocks.Sql;
using Dapper;
using MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;

// A save takes the supply point's next version under a key-range lock, retires the current one and inserts the new
// one as current, in one transaction. A retired version keeps its daily series and drops its inputs: thirty days of
// versions with their inputs would fill most of the production database. Each day is stored to six decimals and the total is the sum of the stored days,
// so the event built from the row adds up. The JSON is ASCII, so varchar holds it as written.
public sealed class ConsumptionForecastRepository(string connectionString) : IConsumptionForecastStore
{
    private const int KwhDecimals = 6;

    private const string FindByTriggerSql = """
        SELECT SupplyPointId, ForecastVersion, ForecastId, ComputedAtUtc, AnchorDate, HorizonDays, TotalKwh,
               UsageFactorSource, TriggerLedgerEntryId, IsCurrent, Daily, Inputs
        FROM dbo.ConsumptionForecast
        WHERE SupplyPointId = @SupplyPointId AND TriggerLedgerEntryId = @TriggerLedgerEntryId;
        """;

    private const string SaveSql = """
        DECLARE @ForecastVersion bigint = (
            SELECT ISNULL(MAX(ForecastVersion), 0) + 1
            FROM dbo.ConsumptionForecast WITH (UPDLOCK, HOLDLOCK)
            WHERE SupplyPointId = @SupplyPointId);

        UPDATE dbo.ConsumptionForecast SET IsCurrent = 0, Inputs = '' WHERE SupplyPointId = @SupplyPointId AND IsCurrent = 1;

        INSERT INTO dbo.ConsumptionForecast
            (SupplyPointId, ForecastVersion, ForecastId, ComputedAtUtc, AnchorDate, HorizonDays, TotalKwh,
             UsageFactorSource, TriggerLedgerEntryId, IsCurrent, Daily, Inputs)
        VALUES
            (@SupplyPointId, @ForecastVersion, CONCAT(@SupplyPointId, ':', @ForecastVersion), @ComputedAtUtc, @AnchorDate,
             @HorizonDays, @TotalKwh, @UsageFactorSource, @TriggerLedgerEntryId, 1, @Daily, @Inputs);

        SELECT @ForecastVersion;
        """;

    private const string DeleteSupersededSql = """
        DELETE TOP (@BatchSize) FROM dbo.ConsumptionForecast WHERE IsCurrent = 0 AND ComputedAtUtc < @ComputedBefore;
        SELECT @@ROWCOUNT;
        """;

    public async Task<StoredForecast?> FindByTriggerAsync(
        string supplyPointId, string triggerLedgerEntryId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SupplyPointId", supplyPointId, DbType.AnsiString, size: 16);
        parameters.Add("TriggerLedgerEntryId", triggerLedgerEntryId, DbType.AnsiString, size: 200);

        await using var connection = new SqlConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<ForecastRow>(
            new CommandDefinition(FindByTriggerSql, parameters, cancellationToken: cancellationToken));
        return row?.ToStored();
    }

    public async Task<StoredForecast> SaveAsync(NewForecast forecast, CancellationToken cancellationToken)
    {
        var daily = forecast.Daily
            .Select(day => day with { Kwh = decimal.Round(day.Kwh, KwhDecimals, MidpointRounding.AwayFromZero) })
            .ToArray();
        var totalKwh = daily.Sum(day => day.Kwh);
        var parameters = new DynamicParameters();
        parameters.Add("SupplyPointId", forecast.SupplyPointId, DbType.AnsiString, size: 16);
        parameters.AddUtc("ComputedAtUtc", forecast.ComputedAtUtc);
        parameters.AddDate("AnchorDate", forecast.AnchorDate);
        parameters.Add("HorizonDays", (short)daily.Length, DbType.Int16);
        parameters.Add("TotalKwh", totalKwh, DbType.Decimal, precision: 18, scale: KwhDecimals);
        parameters.Add("UsageFactorSource", forecast.UsageFactorSource, DbType.AnsiString, size: 8);
        parameters.Add("TriggerLedgerEntryId", forecast.TriggerLedgerEntryId, DbType.AnsiString, size: 200);
        parameters.Add("Daily", DailyJson.Write(daily), DbType.AnsiString, size: -1);
        parameters.Add("Inputs", forecast.Inputs, DbType.AnsiString, size: -1);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var version = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(SaveSql, parameters, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);

        return new StoredForecast
        {
            SupplyPointId = forecast.SupplyPointId,
            ForecastVersion = version,
            ForecastId = string.Create(CultureInfo.InvariantCulture, $"{forecast.SupplyPointId}:{version}"),
            ComputedAtUtc = forecast.ComputedAtUtc,
            AnchorDate = forecast.AnchorDate,
            HorizonDays = daily.Length,
            TotalKwh = totalKwh,
            UsageFactorSource = forecast.UsageFactorSource,
            TriggerLedgerEntryId = forecast.TriggerLedgerEntryId,
            IsCurrent = true,
            Daily = daily,
            Inputs = forecast.Inputs,
        };
    }

    public async Task<int> DeleteSupersededAsync(DateTimeOffset computedBefore, int batchSize, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.AddUtc("ComputedBefore", computedBefore);
        parameters.Add("BatchSize", batchSize, DbType.Int32);

        await using var connection = new SqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(DeleteSupersededSql, parameters, cancellationToken: cancellationToken));
    }

    private sealed record ForecastRow(
        string SupplyPointId,
        long ForecastVersion,
        string ForecastId,
        DateTimeOffset ComputedAtUtc,
        DateTime AnchorDate,
        short HorizonDays,
        decimal TotalKwh,
        string UsageFactorSource,
        string TriggerLedgerEntryId,
        bool IsCurrent,
        string Daily,
        string Inputs)
    {
        public StoredForecast ToStored() => new()
        {
            SupplyPointId = SupplyPointId,
            ForecastVersion = ForecastVersion,
            ForecastId = ForecastId,
            ComputedAtUtc = ComputedAtUtc,
            AnchorDate = DateOnly.FromDateTime(AnchorDate),
            HorizonDays = HorizonDays,
            TotalKwh = TotalKwh,
            UsageFactorSource = UsageFactorSource,
            TriggerLedgerEntryId = TriggerLedgerEntryId,
            IsCurrent = IsCurrent,
            Daily = DailyJson.Read(DateOnly.FromDateTime(AnchorDate), Daily),
            Inputs = Inputs,
        };
    }

    // [8.725931,8.698215]: the kWh of each day from the anchor date, which the row holds, because the series is
    // repeated on every version kept.
    private static class DailyJson
    {
        public static string Write(IEnumerable<ForecastedDay> days) => JsonSerializer.Serialize(days.Select(day => day.Kwh));

        public static ForecastedDay[] Read(DateOnly anchorDate, string json) =>
            [.. JsonSerializer.Deserialize<decimal[]>(json)!.Select((kwh, day) => new ForecastedDay(anchorDate.AddDays(day), kwh))];
    }
}
