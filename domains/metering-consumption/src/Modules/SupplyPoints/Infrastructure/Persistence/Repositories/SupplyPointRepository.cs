using System.Data;
using BuildingBlocks.Persistence;
using BuildingBlocks.Sql;
using Dapper;
using MeteringConsumption.SupplyPoints.Application.Features.Reload.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Store.Abstraction;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Infrastructure.Persistence.Repositories;

// On the unit of work's connection and transaction. Identifiers and codes bind as varchar so the indexes are used.
public sealed class SupplyPointRepository(IUnitOfWork unitOfWork) : ISupplyPointRepository, ISupplyPointReloadRepository
{
    // UPDLOCK with HOLDLOCK also locks the key range when the supply point is new, so a second first message for the
    // same XPRN waits for the first one's insert instead of inserting a duplicate.
    private const string FindSql = """
        SELECT [Market] FROM [supply].[SupplyPoint] WITH (UPDLOCK, HOLDLOCK, FORCESEEK) WHERE [XPRN] = @Xprn;
        """;

    private const string AddSql = "INSERT INTO [supply].[SupplyPoint] ([XPRN], [Market]) VALUES (@Xprn, @Market);";

    // Every statement in a unit of work seeks its own supply point's rows. On tables this small the optimizer would rather
    // scan, and a scan waits on another supply point's uncommitted rows, or deadlocks with that unit of work when it
    // reads this one's in turn.
    private const string SupplyPointIdOf = "(SELECT [SupplyPointId] FROM [supply].[SupplyPoint] WITH (FORCESEEK) WHERE [XPRN] = @Xprn)";

    private const string ReloadDeleteSql = $"""
        DELETE t FROM [supply].[MeterPointHistory] AS t WITH (FORCESEEK) WHERE [SupplyPointId] = {SupplyPointIdOf};
        DELETE t FROM [supply].[MeterPoint] AS t WITH (FORCESEEK) WHERE [SupplyPointId] = {SupplyPointIdOf};
        DELETE t FROM [supply].[UsageFactorHistory] AS t WITH (FORCESEEK) WHERE [SupplyPointId] = {SupplyPointIdOf};
        DELETE t FROM [supply].[UsageFactor] AS t WITH (FORCESEEK) WHERE [SupplyPointId] = {SupplyPointIdOf};
        """;

    internal static readonly Table<MeterPointValues> MeterPoints = new()
    {
        Name = "MeterPoint",
        KeyColumns = [],
        ValueColumns = ["MeterConfigurationCode", "LoadProfileCode", "DuosGroup", "MeterPointStatusCode", "NetworksReferenceNumber"],
        StatedColumns = ["MeterConfigurationCode", "LoadProfileCode", "DuosGroup", "MeterPointStatusCode", "NetworksReferenceNumber"],
        BindKey = (_, _) => { },
        BindValues = (parameters, values) =>
        {
            parameters.Add("MeterConfigurationCode", values.MeterConfigurationCode, DbType.AnsiString, size: 10);
            parameters.Add("LoadProfileCode", values.LoadProfileCode, DbType.AnsiString, size: 3);
            parameters.Add("DuosGroup", values.DuosGroup, DbType.AnsiString, size: 4);
            parameters.Add("MeterPointStatusCode", values.MeterPointStatusCode, DbType.AnsiString, size: 2);
            parameters.Add("NetworksReferenceNumber", values.NetworksReferenceNumber, DbType.AnsiString, size: 35);
        },
        ReadValues = row => new MeterPointValues(
            (string?)row["MeterConfigurationCode"],
            (string?)row["LoadProfileCode"],
            (string?)row["DuosGroup"],
            (string?)row["MeterPointStatusCode"],
            (string?)row["NetworksReferenceNumber"]),
    };

    internal static readonly Table<UsageFactorValues> UsageFactors = new()
    {
        Name = "UsageFactor",
        KeyColumns = ["TimeslotCode"],
        ValueColumns = ["EstimatedUsageFactor", "ActualUsageFactor"],
        StatedColumns = ["EstimatedUsageFactor", "ActualUsageFactor"],
        BindKey = (parameters, key) => parameters.Add("TimeslotCode", ((UsageFactorKey)key).TimeslotCode, DbType.AnsiString, size: 10),
        BindValues = (parameters, values) =>
        {
            parameters.Add("EstimatedUsageFactor", values.Estimated, DbType.Decimal, precision: 15, scale: 8);
            parameters.Add("ActualUsageFactor", values.Actual, DbType.Decimal, precision: 15, scale: 8);
        },
        ReadValues = row => new UsageFactorValues((decimal?)row["EstimatedUsageFactor"], (decimal?)row["ActualUsageFactor"]),
    };

    private static readonly Table<MeterDetails> Meters = new()
    {
        Name = "Meter",
        KeyColumns = ["SerialNumber"],
        ValueColumns = ["MeterCategoryCode", "MeterLocationCode", "ExchangedMeterReference"],
        StatedColumns = ["MeterCategoryCode", "MeterLocationCode", "ExchangedMeterReference"],
        BindKey = (parameters, key) => parameters.Add("SerialNumber", ((MeterKey)key).SerialNumber, DbType.AnsiString, size: 9),
        BindValues = (parameters, values) =>
        {
            parameters.Add("MeterCategoryCode", values.MeterCategoryCode, DbType.AnsiString, size: 15);
            parameters.Add("MeterLocationCode", values.MeterLocationCode, DbType.AnsiString, size: 3);
            parameters.Add("ExchangedMeterReference", values.ExchangedMeterReference, DbType.AnsiString, size: 18);
        },
        ReadValues = row => new MeterDetails(
            (string?)row["MeterCategoryCode"], (string?)row["MeterLocationCode"], (string?)row["ExchangedMeterReference"]),
    };

    // The read's columns are one value: a statement states the reading value whenever it states a read.
    private static readonly Table<RegisterValues> Registers = new()
    {
        Name = "RegisterLevelInfo",
        KeyColumns = ["SerialNumber", "RegisterKey"],
        ValueColumns =
        [
            "TimeslotCode", "RegisterTypeCode", "UOM_Code", "MeterMultiplier", "MeteringInterval", "PreDecimalDetails",
            "PostDecimalDetails", "ReadingValue", "ReadTypeCode", "ReadReasonCode", "ReadStatusCode", "PreviousReadDate",
        ],
        StatedColumns =
        [
            "TimeslotCode", "RegisterTypeCode", "UOM_Code", "MeterMultiplier", "MeteringInterval", "PreDecimalDetails",
            "PostDecimalDetails", "ReadingValue",
        ],
        BindKey = (parameters, key) =>
        {
            var register = ((RegisterKey)key).Register;
            parameters.Add("SerialNumber", register.SerialNumber, DbType.AnsiString, size: 9);
            parameters.Add("RegisterKey", register.Key, DbType.AnsiString, size: 8);
        },
        BindValues = (parameters, values) =>
        {
            var details = values.Details;
            var read = values.Read;
            parameters.Add("TimeslotCode", details.TimeslotCode, DbType.AnsiString, size: 10);
            parameters.Add("RegisterTypeCode", details.RegisterTypeCode, DbType.AnsiString, size: 2);
            parameters.Add("UOM_Code", details.UomCode, DbType.AnsiString, size: 3);
            parameters.Add("MeterMultiplier", details.MeterMultiplier, DbType.Decimal, precision: 12, scale: 5);
            parameters.Add("MeteringInterval", details.MeteringInterval, DbType.Int32);
            parameters.Add("PreDecimalDetails", details.PreDecimalDetails, DbType.AnsiString, size: 2);
            parameters.Add("PostDecimalDetails", details.PostDecimalDetails, DbType.AnsiString, size: 1);
            parameters.Add("ReadingValue", read?.ReadingValue, DbType.Decimal, precision: 15, scale: 3);
            parameters.Add("ReadTypeCode", read?.ReadTypeCode, DbType.AnsiString, size: 2);
            parameters.Add("ReadReasonCode", read?.ReadReasonCode, DbType.AnsiString, size: 2);
            parameters.Add("ReadStatusCode", read?.ReadStatusCode, DbType.AnsiString, size: 4);
            parameters.AddDateOrNull("PreviousReadDate", read?.PreviousReadDate);
        },
        ReadValues = row => new RegisterValues(
            new RegisterDetails
            {
                TimeslotCode = (string?)row["TimeslotCode"],
                RegisterTypeCode = (string?)row["RegisterTypeCode"],
                UomCode = (string?)row["UOM_Code"],
                MeterMultiplier = (decimal?)row["MeterMultiplier"],
                MeteringInterval = (int?)row["MeteringInterval"],
                PreDecimalDetails = (string?)row["PreDecimalDetails"],
                PostDecimalDetails = (string?)row["PostDecimalDetails"],
            },
            row["ReadingValue"] is decimal reading
                ? new RegisterRead(reading, (string?)row["ReadTypeCode"], (string?)row["ReadReasonCode"], (string?)row["ReadStatusCode"],
                    row["PreviousReadDate"] is DateTime previous ? DateOnly.FromDateTime(previous) : null)
                : null),
    };

    private static readonly Dictionary<Type, object> Tables = new()
    {
        [typeof(MeterPointValues)] = MeterPoints,
        [typeof(UsageFactorValues)] = UsageFactors,
        [typeof(MeterDetails)] = Meters,
        [typeof(RegisterValues)] = Registers,
    };

    public async Task<SupplyPoint?> FindAsync(XPRN xprn, CancellationToken cancellationToken)
    {
        var market = await unitOfWork.Connection.QuerySingleOrDefaultAsync<string>(Command(FindSql, XprnParameter(xprn), cancellationToken));
        if (market is null)
        {
            return null;
        }

        var supplyPoint = SupplyPoint.Restore(xprn, market);
        unitOfWork.Track(supplyPoint);
        return supplyPoint;
    }

    public async Task AddAsync(SupplyPoint supplyPoint, CancellationToken cancellationToken)
    {
        var parameters = XprnParameter(supplyPoint.Id);
        parameters.Add("Market", supplyPoint.Market, DbType.AnsiString, size: 16);

        await unitOfWork.Connection.ExecuteAsync(Command(AddSql, parameters, cancellationToken));
        unitOfWork.Track(supplyPoint);
    }

    public async Task<KeyFacts<TValues>> FactsAsync<TValues>(ConfigurationKey<TValues> key, DateOnly effectiveDate,
        CancellationToken cancellationToken)
        where TValues : class, ICarriedValues<TValues>
    {
        var table = TableOf<TValues>();
        var parameters = table.KeyParameters(key);
        parameters.AddDate("EffectiveDate", effectiveDate);

        await using var results = await unitOfWork.Connection.QueryMultipleAsync(Command(table.FactsSql, parameters, cancellationToken));
        var current = table.Rows(await results.ReadAsync());
        var onDate = table.Rows(await results.ReadAsync());
        var onOrBefore = table.Rows(await results.ReadAsync());
        var after = table.Rows(await results.ReadAsync());

        return new KeyFacts<TValues>
        {
            Current = current,
            HistoryOnDate = onDate.SingleOrDefault(),
            StatedOnOrBefore = onOrBefore,
            StatedAfter = after,
        };
    }

    public async Task SaveAsync<TValues>(ConfigurationKey<TValues> key, Recorded<TValues> recorded, CancellationToken cancellationToken)
        where TValues : class, ICarriedValues<TValues>
    {
        var table = TableOf<TValues>();
        if (recorded.History is { } history)
        {
            var sql = recorded.Kind == RecordOutcomeKind.Replaced ? table.HistoryUpdateSql : table.HistoryInsertSql;
            await unitOfWork.Connection.ExecuteAsync(Command(sql, table.RowParameters(key, history), cancellationToken));
        }

        foreach (var write in recorded.Current)
        {
            var sql = write.Kind switch
            {
                CurrentWriteKind.Insert => table.CurrentInsertSql,
                CurrentWriteKind.Update => table.CurrentUpdateSql,
                _ => table.CurrentDeleteSql,
            };
            await unitOfWork.Connection.ExecuteAsync(Command(sql, table.RowParameters(key, write.Row), cancellationToken));
        }
    }

    public async Task SaveReloadAsync(XPRN xprn, ReloadOutcome outcome, CancellationToken cancellationToken)
    {
        await unitOfWork.Connection.ExecuteAsync(Command(ReloadDeleteSql, XprnParameter(xprn), cancellationToken));
        await InsertAsync(MeterPoints, new MeterPointKey(xprn), outcome.MeterPoint, cancellationToken);
        foreach (var (timeslotCode, rows) in outcome.UsageFactors)
        {
            await InsertAsync(UsageFactors, new UsageFactorKey(xprn, timeslotCode), rows, cancellationToken);
        }
    }

    private async Task InsertAsync<TValues>(Table<TValues> table, ConfigurationKey<TValues> key, KeyRows<TValues> rows,
        CancellationToken cancellationToken)
        where TValues : class, ICarriedValues<TValues>
    {
        foreach (var row in rows.History)
        {
            await unitOfWork.Connection.ExecuteAsync(Command(table.HistoryInsertSql, table.RowParameters(key, row), cancellationToken));
        }

        foreach (var row in rows.Current)
        {
            await unitOfWork.Connection.ExecuteAsync(Command(table.CurrentInsertSql, table.RowParameters(key, row), cancellationToken));
        }
    }

    private CommandDefinition Command(string sql, DynamicParameters parameters, CancellationToken cancellationToken) =>
        new(sql, parameters, unitOfWork.Transaction, cancellationToken: cancellationToken);

    private static Table<TValues> TableOf<TValues>()
        where TValues : class, ICarriedValues<TValues> =>
        (Table<TValues>)Tables[typeof(TValues)];

    internal static DynamicParameters XprnParameter(XPRN xprn)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Xprn", xprn.Value, DbType.AnsiString, size: 11);
        return parameters;
    }

    // One kind of key's two tables, [supply].[{Name}History] and [supply].[{Name}], keyed by the supply point, the key
    // columns and the effective date, and how a row becomes values and back. A parameter is named for its column.
    internal sealed class Table<TValues>
        where TValues : class, ICarriedValues<TValues>
    {
        public required string Name { get; init; }

        public required string[] KeyColumns { get; init; }

        public required string[] ValueColumns { get; init; }

        // One column per value, set whenever a statement states that value.
        public required string[] StatedColumns { get; init; }

        public required Action<DynamicParameters, ConfigurationKey<TValues>> BindKey { get; init; }

        public required Action<DynamicParameters, TValues> BindValues { get; init; }

        public required Func<IDictionary<string, object>, TValues> ReadValues { get; init; }

        private string Current => $"[supply].[{Name}]";

        private string History => $"[supply].[{Name}History]";

        private string KeyMatch => $"[SupplyPointId] = {SupplyPointIdOf}" + string.Concat(KeyColumns.Select(column => $" AND [{column}] = @{column}"));

        private string Selected => string.Join(", ", ValueColumns.Prepend("EffectiveDate").Select(column => $"[{column}]"));

        // The key's current rows, its history row on the date, and the history rows that give each value its latest
        // statement on or before the date and its first after it: one seek per value, however long the history.
        public string FactsSql => $"""
            SELECT {Selected} FROM {Current} WITH (FORCESEEK) WHERE {KeyMatch} ORDER BY [EffectiveDate];
            SELECT {Selected} FROM {History} WITH (FORCESEEK) WHERE {KeyMatch} AND [EffectiveDate] = @EffectiveDate;
            SELECT {Selected} FROM {History} WITH (FORCESEEK) WHERE {KeyMatch} AND [EffectiveDate] IN ({Each("MAX", "<=")}) ORDER BY [EffectiveDate];
            SELECT {Selected} FROM {History} WITH (FORCESEEK) WHERE {KeyMatch} AND [EffectiveDate] IN ({Each("MIN", ">")}) ORDER BY [EffectiveDate];
            """;

        // For every key of this kind at the supply point: the latest current row on or before @Day, and the history rows
        // that give each value its latest statement on or before it.
        public string OnDaySql
        {
            get
            {
                var partition = string.Join(", ", KeyColumns.Prepend("SupplyPointId").Select(column => $"[{column}]"));
                var keys = string.Concat(KeyColumns.Select(column => $"[{column}], "));
                var latest = string.Join(", ", StatedColumns.Select((column, index) =>
                    $"MAX(CASE WHEN [{column}] IS NOT NULL THEN [EffectiveDate] END) OVER (PARTITION BY {partition}) AS [Latest{index}]"));
                return $"""
                    SELECT {keys}{Selected} FROM (
                        SELECT *, ROW_NUMBER() OVER (PARTITION BY {partition} ORDER BY [EffectiveDate] DESC) AS [Latest]
                        FROM {Current} WHERE [SupplyPointId] = {SupplyPointIdOf} AND [EffectiveDate] <= @Day) AS [Row]
                    WHERE [Latest] = 1;
                    SELECT {keys}{Selected} FROM (
                        SELECT *, {latest}
                        FROM {History} WHERE [SupplyPointId] = {SupplyPointIdOf} AND [EffectiveDate] <= @Day) AS [Row]
                    WHERE [EffectiveDate] IN ({string.Join(", ", StatedColumns.Select((_, index) => $"[Latest{index}]"))});
                    """;
            }
        }

        public string HistoryInsertSql => InsertInto(History);

        public string HistoryUpdateSql =>
            $"UPDATE t SET {Assignments} FROM {History} AS t WITH (FORCESEEK) WHERE {KeyMatch} AND [EffectiveDate] = @EffectiveDate;";

        public string CurrentInsertSql => InsertInto(Current);

        public string CurrentUpdateSql =>
            $"UPDATE t SET {Assignments}, [UpdatedAt] = SYSUTCDATETIME() FROM {Current} AS t WITH (FORCESEEK) WHERE {KeyMatch} AND [EffectiveDate] = @EffectiveDate;";

        public string CurrentDeleteSql =>
            $"DELETE t FROM {Current} AS t WITH (FORCESEEK) WHERE {KeyMatch} AND [EffectiveDate] = @EffectiveDate;";

        private string Assignments => string.Join(", ", ValueColumns.Select(column => $"t.[{column}] = @{column}"));

        public DynamicParameters KeyParameters(ConfigurationKey<TValues> key)
        {
            var parameters = XprnParameter(key.Xprn);
            BindKey(parameters, key);
            return parameters;
        }

        public DynamicParameters RowParameters(ConfigurationKey<TValues> key, Dated<TValues> row)
        {
            var parameters = KeyParameters(key);
            parameters.AddDate("EffectiveDate", row.EffectiveDate);
            BindValues(parameters, row.Values);
            return parameters;
        }

        public IReadOnlyList<Dated<TValues>> Rows(IEnumerable<dynamic> rows) =>
            [.. rows.Select(row => Row((IDictionary<string, object>)row))];

        public Dated<TValues> Row(IDictionary<string, object> row) =>
            new(DateOnly.FromDateTime((DateTime)row["EffectiveDate"]), ReadValues(row));

        private string InsertInto(string table)
        {
            var columns = KeyColumns.Append("EffectiveDate").Concat(ValueColumns).ToArray();
            return $"""
                INSERT INTO {table} ([SupplyPointId], {string.Join(", ", columns.Select(column => $"[{column}]"))})
                VALUES ({SupplyPointIdOf}, {string.Join(", ", columns.Select(column => $"@{column}"))});
                """;
        }

        private string Each(string aggregate, string comparison) => string.Join(" UNION ALL ", StatedColumns.Select(column =>
            $"SELECT {aggregate}([EffectiveDate]) FROM {History} WITH (FORCESEEK) WHERE {KeyMatch} AND [EffectiveDate] {comparison} @EffectiveDate AND [{column}] IS NOT NULL"));
    }
}
