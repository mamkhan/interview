using System.Reflection;
using BuildingBlocks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.SupplyPoints.Infrastructure.Schema;

// The module's tables in the domain's database, in their own `supply` schema, and the one schema policy every host
// that runs the module applies: Development applies unapplied scripts, everywhere else the pipeline's migrator has
// run and a host that still sees an unapplied script refuses to start.
public static class SupplyPointSchema
{
    public const string ConnectionName = "metering-consumption";

    private const string ScriptPrefix = "Metr-SupplyPoint";

    private static Assembly Scripts => typeof(SupplyPointSchema).Assembly;

    // The migrator host, every test class that needs these tables, and a Development host all reach the
    // schema through here.
    public static Task ApplySupplyPointSchemaAsync(string connectionString)
    {
        SchemaMigrator.Apply(connectionString, Scripts, ScriptPrefix);
        return Task.CompletedTask;
    }

    public static Task ApplySupplyPointMigrationsAsync(this IHost host)
    {
        var connectionString = host.Services.GetRequiredService<IConfiguration>().SupplyPointConnectionString();
        if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return ApplySupplyPointSchemaAsync(connectionString);
        }

        var pending = SchemaMigrator.Pending(connectionString, Scripts, ScriptPrefix);
        return pending.Count == 0
            ? Task.CompletedTask
            : throw new InvalidOperationException(
                $"{pending.Count} unapplied schema script(s) ({string.Join(", ", pending)}); run the MeteringConsumption migrator before deploying this host.");
    }

    internal static string SupplyPointConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionName)
        ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is not configured.");
}
