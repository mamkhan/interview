using System.Reflection;
using BuildingBlocks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.Consumption.Infrastructure.Schema;

// The market reference data Forecast reads, in a database of its own that another process fills. Its scripts sit
// beside the module's under their own prefix, and the same policy applies: Development applies unapplied scripts,
// everywhere else the pipeline's migrator has run and a host that still sees an unapplied script refuses to start.
public static class ReferenceDataSchema
{
    public const string ConnectionName = "reference-data";

    internal const string ScriptPrefix = "Metr-ReferenceData";

    private static Assembly Scripts => typeof(ReferenceDataSchema).Assembly;

    public static Task ApplyReferenceDataSchemaAsync(string connectionString)
    {
        SchemaMigrator.Apply(connectionString, Scripts, ScriptPrefix, ConsumptionSchema.ScriptPrefix);
        return Task.CompletedTask;
    }

    public static Task ApplyReferenceDataMigrationsAsync(this IHost host)
    {
        var connectionString = host.Services.GetRequiredService<IConfiguration>().ReferenceDataConnectionString();
        if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return ApplyReferenceDataSchemaAsync(connectionString);
        }

        var pending = SchemaMigrator.Pending(connectionString, Scripts, ScriptPrefix, ConsumptionSchema.ScriptPrefix);
        return pending.Count == 0
            ? Task.CompletedTask
            : throw new InvalidOperationException(
                $"{pending.Count} unapplied schema script(s) ({string.Join(", ", pending)}); run the MeteringConsumption migrator before deploying this host.");
    }

    internal static string ReferenceDataConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionName)
        ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is not configured.");
}
