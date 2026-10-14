using System.Reflection;
using BuildingBlocks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Schema;

// The module's tables in the domain's database and the one schema policy every host that runs the
// module applies: Development applies unapplied scripts, everywhere else the pipeline's migrator has
// run and a host that still sees an unapplied script refuses to start.
public static class MarketMessagingSchema
{
    public const string ConnectionName = "metering-consumption";

    private const string ScriptPrefix = "Metr-MarketMessaging";

    private static Assembly Scripts => typeof(MarketMessagingSchema).Assembly;

    /// <summary>Brings a database up to the module's scripts. The migrator host, every test class that
    /// needs these tables, and a Development host all reach the schema through here.</summary>
    public static Task ApplyMarketMessagingSchemaAsync(string connectionString)
    {
        SchemaMigrator.Apply(connectionString, Scripts, ScriptPrefix);
        return Task.CompletedTask;
    }

    public static Task ApplyMarketMessagingMigrationsAsync(this IHost host)
    {
        var connectionString = host.Services.GetRequiredService<IConfiguration>().MarketMessagingConnectionString();
        if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return ApplyMarketMessagingSchemaAsync(connectionString);
        }

        var pending = SchemaMigrator.Pending(connectionString, Scripts, ScriptPrefix);
        return pending.Count == 0
            ? Task.CompletedTask
            : throw new InvalidOperationException(
                $"{pending.Count} unapplied schema script(s) ({string.Join(", ", pending)}); run the MeteringConsumption migrator before deploying this host.");
    }

    internal static string MarketMessagingConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionName)
        ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is not configured.");
}
