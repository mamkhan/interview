using System.Data;
using BuildingBlocks.Sql;
using Dapper;
using MeteringConsumption.Consumption.Application.Features.Profile.Abstraction;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;

// A connection per call. Each save writes one group of columns (the configuration, the usage factors, the
// tariff or the smart meter flag), so the consumers never overwrite each other's figures. The update runs under a key-range lock and inserts
// when it found no row, in one transaction, so both consumers can reach a new supply point at once and
// neither fails on the primary key. Identifiers bind as varchar so the primary key index is used.
public sealed class SupplyPointProfileRepository(string connectionString) : ISupplyPointProfileRepository
{
    private const string FindSql = "SELECT * FROM dbo.SupplyPointProfile WHERE SupplyPointId = @SupplyPointId;";

    // Euf holds the estimated and Auf the actual factor of each timeslot. The column names come from this
    // fixed list, never from a message, so the statement built from them cannot carry input.
    private static readonly (UsageFactorSlot Slot, string Column)[] FactorColumns =
    [
        .. from kind in new[] { (Kind: UsageFactorKind.Estimated, Prefix: "Euf"), (Kind: UsageFactorKind.Actual, Prefix: "Auf") }
           from timeslot in UsageFactorTimeslots.All
           select (new UsageFactorSlot(timeslot, kind.Kind), kind.Prefix + timeslot),
    ];

    private static readonly string SaveUsageFactorsSql = BuildSaveUsageFactorsSql();

    private const string SaveConfigurationSql = """
        UPDATE dbo.SupplyPointProfile WITH (UPDLOCK, SERIALIZABLE)
        SET Market = @Market, MeterConfigurationCode = @MeterConfigurationCode, LoadProfileCode = @LoadProfileCode,
            DuosGroup = @DuosGroup, MeterPointStatusCode = @MeterPointStatusCode,
            ConfigurationEffectiveFrom = @ConfigurationEffectiveFrom, ConfigurationSource = @ConfigurationSource,
            ConfigurationMessageType = @ConfigurationMessageType, UpdatedAt = @UpdatedAt
        WHERE SupplyPointId = @SupplyPointId;

        IF @@ROWCOUNT = 0
            INSERT INTO dbo.SupplyPointProfile
                (SupplyPointId, Market, MeterConfigurationCode, LoadProfileCode, DuosGroup, MeterPointStatusCode,
                 ConfigurationEffectiveFrom, ConfigurationSource, ConfigurationMessageType, UpdatedAt)
            VALUES
                (@SupplyPointId, @Market, @MeterConfigurationCode, @LoadProfileCode, @DuosGroup, @MeterPointStatusCode,
                 @ConfigurationEffectiveFrom, @ConfigurationSource, @ConfigurationMessageType, @UpdatedAt);
        """;

    private const string SaveTariffSql = """
        UPDATE dbo.SupplyPointProfile WITH (UPDLOCK, SERIALIZABLE)
        SET Market = @Market, IsTou = @IsTou, TariffCalcType = @TariffCalcType,
            EffectiveLoadProfileCode = @EffectiveLoadProfileCode, TariffAsOf = @TariffAsOf, TariffSource = @TariffSource,
            UpdatedAt = @UpdatedAt
        WHERE SupplyPointId = @SupplyPointId;

        IF @@ROWCOUNT = 0
            INSERT INTO dbo.SupplyPointProfile
                (SupplyPointId, Market, IsTou, TariffCalcType, EffectiveLoadProfileCode, TariffAsOf, TariffSource, UpdatedAt)
            VALUES
                (@SupplyPointId, @Market, @IsTou, @TariffCalcType, @EffectiveLoadProfileCode, @TariffAsOf, @TariffSource, @UpdatedAt);
        """;

    private const string SaveSmartMeterFlagSql = """
        UPDATE dbo.SupplyPointProfile WITH (UPDLOCK, SERIALIZABLE)
        SET Market = @Market, IsNsmp = @IsNsmp, SmartMeterFlagAsOf = @SmartMeterFlagAsOf,
            SmartMeterFlagSource = @SmartMeterFlagSource, UpdatedAt = @UpdatedAt
        WHERE SupplyPointId = @SupplyPointId;

        IF @@ROWCOUNT = 0
            INSERT INTO dbo.SupplyPointProfile
                (SupplyPointId, Market, IsNsmp, SmartMeterFlagAsOf, SmartMeterFlagSource, UpdatedAt)
            VALUES
                (@SupplyPointId, @Market, @IsNsmp, @SmartMeterFlagAsOf, @SmartMeterFlagSource, @UpdatedAt);
        """;

    public async Task<SupplyPointProfile?> FindAsync(string supplyPointId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SupplyPointId", supplyPointId, DbType.AnsiString, size: 16);

        await using var connection = new SqlConnection(connectionString);
        var row = (IDictionary<string, object?>?)await connection.QuerySingleOrDefaultAsync(
            new CommandDefinition(FindSql, parameters, cancellationToken: cancellationToken));
        return row is null
            ? null
            : SupplyPointProfile.Restore(supplyPointId, (string)row["Market"]!, ConfigurationOf(row), UsageFactorsOf(row),
                TariffOf(row), SmartMeterFlagOf(row));
    }

    public async Task SaveConfigurationAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var configuration = profile.Configuration
            ?? throw new InvalidOperationException($"The profile of {profile.SupplyPointId} has no configuration to save.");
        var parameters = KeyParameters(profile, updatedAt);
        parameters.Add("MeterConfigurationCode", configuration.MeterConfigurationCode, DbType.AnsiString, size: 16);
        parameters.Add("LoadProfileCode", configuration.LoadProfileCode, DbType.AnsiString, size: 8);
        parameters.Add("DuosGroup", configuration.DuosGroup, DbType.AnsiString, size: 8);
        parameters.Add("MeterPointStatusCode", configuration.MeterPointStatusCode, DbType.AnsiString, size: 4);
        parameters.AddDate("ConfigurationEffectiveFrom", configuration.EffectiveFrom);
        parameters.Add("ConfigurationSource", configuration.Source, DbType.AnsiString, size: 64);
        parameters.Add("ConfigurationMessageType", configuration.MessageType, DbType.AnsiString, size: 16);

        await ExecuteInTransactionAsync(SaveConfigurationSql, parameters, cancellationToken);
    }

    public async Task SaveUsageFactorsAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var parameters = KeyParameters(profile, updatedAt);
        foreach (var (slot, column) in FactorColumns)
        {
            var factor = profile.UsageFactors.GetValueOrDefault(slot);
            parameters.Add(column, factor?.Value, DbType.Decimal, precision: 20, scale: 10);
            parameters.AddDateOrNull($"{column}EffectiveFrom", factor?.EffectiveFrom);
            parameters.Add($"{column}Source", factor?.Source, DbType.AnsiString, size: 64);
        }

        await ExecuteInTransactionAsync(SaveUsageFactorsSql, parameters, cancellationToken);
    }

    public async Task SaveTariffAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var tariff = profile.Tariff
            ?? throw new InvalidOperationException($"The profile of {profile.SupplyPointId} has no tariff to save.");
        var parameters = KeyParameters(profile, updatedAt);
        parameters.Add("IsTou", tariff.IsTou, DbType.Boolean);
        parameters.Add("TariffCalcType", tariff.CalcType, DbType.AnsiString, size: 16);
        parameters.Add("EffectiveLoadProfileCode", tariff.EffectiveLoadProfileCode, DbType.AnsiString, size: 8);
        parameters.AddUtc("TariffAsOf", tariff.AsOf);
        parameters.Add("TariffSource", tariff.Source, DbType.AnsiString, size: 64);

        await ExecuteInTransactionAsync(SaveTariffSql, parameters, cancellationToken);
    }

    public async Task SaveSmartMeterFlagAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var flag = profile.SmartMeterFlag
            ?? throw new InvalidOperationException($"The profile of {profile.SupplyPointId} has no smart meter flag to save.");
        var parameters = KeyParameters(profile, updatedAt);
        parameters.Add("IsNsmp", flag.IsNsmp, DbType.Boolean);
        parameters.AddUtc("SmartMeterFlagAsOf", flag.AsOf);
        parameters.Add("SmartMeterFlagSource", flag.Source, DbType.AnsiString, size: 64);

        await ExecuteInTransactionAsync(SaveSmartMeterFlagSql, parameters, cancellationToken);
    }

    private async Task ExecuteInTransactionAsync(string sql, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    private static DynamicParameters KeyParameters(SupplyPointProfile profile, DateTimeOffset updatedAt)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SupplyPointId", profile.SupplyPointId, DbType.AnsiString, size: 16);
        parameters.Add("Market", profile.Market, DbType.AnsiString, size: 16);
        parameters.AddUtc("UpdatedAt", updatedAt);
        return parameters;
    }

    private static MeterConfigurationStatement? ConfigurationOf(IDictionary<string, object?> row) =>
        row["ConfigurationEffectiveFrom"] is DateTime effectiveFrom
            ? new MeterConfigurationStatement
            {
                MeterConfigurationCode = (string?)row["MeterConfigurationCode"],
                LoadProfileCode = (string?)row["LoadProfileCode"],
                DuosGroup = (string?)row["DuosGroup"],
                MeterPointStatusCode = (string?)row["MeterPointStatusCode"],
                EffectiveFrom = DateOnly.FromDateTime(effectiveFrom),
                Source = (string)row["ConfigurationSource"]!,
                MessageType = (string)row["ConfigurationMessageType"]!,
            }
            : null;

    private static TariffStatement? TariffOf(IDictionary<string, object?> row) =>
        row["TariffAsOf"] is DateTimeOffset asOf
            ? TariffStatement.Of(
                (bool?)row["IsTou"],
                (string?)row["TariffCalcType"],
                (string?)row["EffectiveLoadProfileCode"],
                asOf,
                (string)row["TariffSource"]!)
            : null;

    private static SmartMeterFlagStatement? SmartMeterFlagOf(IDictionary<string, object?> row) =>
        row["SmartMeterFlagAsOf"] is DateTimeOffset asOf
            ? new SmartMeterFlagStatement((bool)row["IsNsmp"]!, asOf, (string)row["SmartMeterFlagSource"]!)
            : null;

    private static IEnumerable<UsageFactorStatement> UsageFactorsOf(IDictionary<string, object?> row) =>
        from factor in FactorColumns
        where row[factor.Column] is decimal
        select new UsageFactorStatement(
            factor.Slot,
            DateOnly.FromDateTime((DateTime)row[$"{factor.Column}EffectiveFrom"]!),
            (decimal)row[factor.Column]!,
            (string)row[$"{factor.Column}Source"]!);

    private static string BuildSaveUsageFactorsSql()
    {
        var columns = FactorColumns
            .SelectMany(factor => new[] { factor.Column, $"{factor.Column}EffectiveFrom", $"{factor.Column}Source" })
            .ToArray();
        return $"""
            UPDATE dbo.SupplyPointProfile WITH (UPDLOCK, SERIALIZABLE)
            SET Market = @Market, {string.Join(", ", columns.Select(column => $"{column} = @{column}"))}, UpdatedAt = @UpdatedAt
            WHERE SupplyPointId = @SupplyPointId;

            IF @@ROWCOUNT = 0
                INSERT INTO dbo.SupplyPointProfile (SupplyPointId, Market, {string.Join(", ", columns)}, UpdatedAt)
                VALUES (@SupplyPointId, @Market, {string.Join(", ", columns.Select(column => $"@{column}"))}, @UpdatedAt);
            """;
    }
}
