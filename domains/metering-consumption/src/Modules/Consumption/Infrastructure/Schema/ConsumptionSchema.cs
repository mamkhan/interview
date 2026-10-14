using System.Reflection;
using BuildingBlocks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.Consumption.Infrastructure.Schema;

// The module's tables in the domain's database and the one schema policy every host that runs the
// module applies: Development applies unapplied scripts, everywhere else the pipeline's migrator has
// run and a host that still sees an unapplied script refuses to start.
public static class ConsumptionSchema
{
    public const string ConnectionName = "metering-consumption";

    internal const string ScriptPrefix = "Metr-Consumption";

    private static Assembly Scripts => typeof(ConsumptionSchema).Assembly;

    // The migrator host, every test class that needs these tables, and a Development host all reach the
    // schema through here.
    public static Task ApplyConsumptionSchemaAsync(string connectionString)
    {
        SchemaMigrator.Apply(connectionString, Scripts, ScriptPrefix, ReferenceDataSchema.ScriptPrefix);
        return Task.CompletedTask;
    }

    public static Task ApplyConsumptionMigrationsAsync(this IHost host)
    {
        var connectionString = host.Services.GetRequiredService<IConfiguration>().ConsumptionConnectionString();
        if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return ApplyConsumptionSchemaAsync(connectionString);
        }

        var pending = SchemaMigrator.Pending(connectionString, Scripts, ScriptPrefix, ReferenceDataSchema.ScriptPrefix);
        return pending.Count == 0
            ? Task.CompletedTask
            : throw new InvalidOperationException(
                $"{pending.Count} unapplied schema script(s) ({string.Join(", ", pending)}); run the MeteringConsumption migrator before deploying this host.");
    }

    internal static string ConsumptionConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionName)
        ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is not configured.");
}
