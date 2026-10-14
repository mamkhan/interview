using Azure.Storage.Blobs;
using LocalBlobEvents;
using Microsoft.Extensions.Azure;

// Stands in for the Event Grid system topic under `aspire run`: watches one Azurite container and enqueues
// one CloudEvents BlobCreated per blob it has not announced. Local only; never deployed.
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs", configureClientBuilder: clientBuilder =>
{
    var version = builder.Configuration[BlobServiceVersion.ConfigurationKey];
    if (!string.IsNullOrWhiteSpace(version))
    {
        clientBuilder.WithVersion(Enum.Parse<BlobClientOptions.ServiceVersion>(version));
    }
});

builder.Services.AddOptions<BlobEventsOptions>()
    .BindConfiguration(BlobEventsOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHostedService<BlobEventPoller>();

builder.Build().Run();
