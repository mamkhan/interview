using System.Data;
using Dapper;

namespace BuildingBlocks.Sql;

/// <summary>Explicitly typed temporal parameters. An untyped temporal value binds at the driver's
/// default type and precision (legacy DATETIME rounds to 1/300 s), so every temporal parameter
/// states its type: DATETIMEOFFSET for instants, DATETIME2 for legacy UTC columns, DATE for dates.</summary>
public static class SqlTemporal
{
    public static DynamicParameters AddUtc(this DynamicParameters parameters, string name, DateTimeOffset value)
    {
        parameters.Add(name, value, DbType.DateTimeOffset);
        return parameters;
    }

    public static DynamicParameters AddUtcOrNull(this DynamicParameters parameters, string name,
        DateTimeOffset? value)
    {
        parameters.Add(name, value, DbType.DateTimeOffset);
        return parameters;
    }

    public static DynamicParameters AddUtcDateTime2(this DynamicParameters parameters, string name, DateTime valueUtc)
    {
        parameters.Add(name, valueUtc, DbType.DateTime2);
        return parameters;
    }

    public static DynamicParameters AddDate(this DynamicParameters parameters, string name, DateOnly value)
    {
        parameters.Add(name, value.ToDateTime(TimeOnly.MinValue), DbType.Date);
        return parameters;
    }

    public static DynamicParameters AddDateOrNull(this DynamicParameters parameters, string name, DateOnly? value)
    {
        parameters.Add(name, value?.ToDateTime(TimeOnly.MinValue), DbType.Date);
        return parameters;
    }
}
