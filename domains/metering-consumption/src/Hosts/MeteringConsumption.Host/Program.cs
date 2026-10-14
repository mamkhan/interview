using Azure.Core.Extensions;
using Azure.Storage.Blobs;
using MeteringConsumption.Consumption.Application.Extensions;
using MeteringConsumption.Consumption.Application.Features.Record;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Consumption.Infrastructure.Extensions;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.MarketMessaging.Application.Extensions;
using MeteringConsumption.MarketMessaging.Infrastructure.Extensions;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Extensions.Azure;
using ConsumptionApplication = MeteringConsumption.Consumption.Application.Extensions.ApplicationRegistration;
using ConsumptionInfrastructure = MeteringConsumption.Consumption.Infrastructure.Extensions.InfrastructureRegistration;
using MarketMessagingApplication = MeteringConsumption.MarketMessaging.Application.Extensions.ApplicationRegistration;
using MarketMessagingInfrastructure = MeteringConsumption.MarketMessaging.Infrastructure.Extensions.InfrastructureRegistration;
using SupplyPointsApplication = MeteringConsumption.SupplyPoints.Application.Extensions.ApplicationRegistration;
using SupplyPointsInfrastructure = MeteringConsumption.SupplyPoints.Infrastructure.Extensions.InfrastructureRegistration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.TryAddTimeProvider();

builder.AddAzureServiceBusClient("messaging");
// Aspire drops the unkeyed blob client once a keyed one of the same type is added, so both accounts are keyed
// and the archive's is also served unkeyed for the Market Messaging stores that ask for it that way.
builder.AddKeyedAzureBlobServiceClient("blobs", configureClientBuilder: PinBlobServiceVersion);
builder.AddKeyedAzureBlobServiceClient(RecordOptions.LedgerConnectionName, configureClientBuilder: PinBlobServiceVersion);
builder.Services.AddSingleton(services => services.GetRequiredKeyedService<BlobServiceClient>("blobs"));
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(RecordTelemetry.MeterName));

// Every module names its base methods alike, so a host that runs two modules calls each through its class.
MarketMessagingApplication.AddBaseApplication(builder.Services);
MarketMessagingInfrastructure.AddBaseInfrastructure(builder.Services, builder.Configuration);
ConsumptionApplication.AddBaseApplication(builder.Services);
ConsumptionInfrastructure.AddBaseInfrastructure(builder.Services, builder.Configuration);
SupplyPointsApplication.AddBaseApplication(builder.Services);
SupplyPointsInfrastructure.AddBaseInfrastructure(builder.Services, builder.Configuration);

builder.Services.AddInterpretApplication();
builder.Services.AddInterpretInfrastructure(builder.Configuration);

builder.Services.AddAuditApplication();
builder.Services.AddAuditInfrastructure(builder.Configuration);

builder.Services.AddRecordApplication();
builder.Services.AddRecordInfrastructure(builder.Configuration);

builder.Services.AddForecastApplication();
builder.Services.AddForecastInfrastructure(builder.Configuration);

builder.Services.AddProfileApplication();
builder.Services.AddProfileInfrastructure(builder.Configuration);

// Store and Consumption's Record share a slice name, so Store is called through its class.
SupplyPointsApplication.AddStoreApplication(builder.Services);
SupplyPointsInfrastructure.AddStoreInfrastructure(builder.Services, builder.Configuration);

SupplyPointsApplication.AddReloadApplication(builder.Services);
SupplyPointsInfrastructure.AddReloadInfrastructure(builder.Services, builder.Configuration);

SupplyPointsApplication.AddProvideApplication(builder.Services);
SupplyPointsInfrastructure.AddProvideInfrastructure(builder.Services, builder.Configuration);

var app = builder.Build();

await app.ApplyMarketMessagingMigrationsAsync();
await app.ApplyConsumptionMigrationsAsync();
await app.ApplyReferenceDataMigrationsAsync();
await app.ApplySupplyPointMigrationsAsync();

app.MapDefaultEndpoints();
app.Run();

void PinBlobServiceVersion(IAzureClientBuilder<BlobServiceClient, BlobClientOptions> clientBuilder)
{
    var version = builder.Configuration[BlobServiceVersion.ConfigurationKey];
    if (!string.IsNullOrWhiteSpace(version))
    {
        clientBuilder.WithVersion(Enum.Parse<BlobClientOptions.ServiceVersion>(version));
    }
}
