using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Extensions.Configuration;

// Brings the domain's database up to every module's scripts, as the deploying principal, before the hosts
// that refuse to start on an unapplied script are rolled. It reads the connection string from the key the
// hosts read, so the pipeline configures one name for both. The reference data Forecast reads has a database of its own.
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();

var connectionString = configuration.GetConnectionString(MarketMessagingSchema.ConnectionName);
var referenceDataConnectionString = configuration.GetConnectionString(ReferenceDataSchema.ConnectionName);
if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(referenceDataConnectionString))
{
    await Console.Error.WriteLineAsync(
        $"Connection strings '{MarketMessagingSchema.ConnectionName}' and '{ReferenceDataSchema.ConnectionName}' must both be configured; " +
        $"set ConnectionStrings__{MarketMessagingSchema.ConnectionName} and ConnectionStrings__{ReferenceDataSchema.ConnectionName}.");
    return 1;
}

await MarketMessagingSchema.ApplyMarketMessagingSchemaAsync(connectionString);
await ConsumptionSchema.ApplyConsumptionSchemaAsync(connectionString);
await SupplyPointSchema.ApplySupplyPointSchemaAsync(connectionString);
await ReferenceDataSchema.ApplyReferenceDataSchemaAsync(referenceDataConnectionString);
Console.WriteLine("Schema is up to date.");
return 0;
