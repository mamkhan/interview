using Azure.Storage.Blobs;
using MeteringConsumption.MarketMessaging.Application.Extensions;
using MeteringConsumption.MarketMessaging.Infrastructure.Extensions;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.TryAddTimeProvider();

builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs", configureClientBuilder: clientBuilder =>
{
    var version = builder.Configuration[BlobServiceVersion.ConfigurationKey];
    if (!string.IsNullOrWhiteSpace(version))
    {
        clientBuilder.WithVersion(Enum.Parse<BlobClientOptions.ServiceVersion>(version));
    }
});

builder.Services.AddBaseApplication();
builder.Services.AddBaseInfrastructure(builder.Configuration);

builder.Services.AddReceiveApplication();
builder.Services.AddReceiveInfrastructure(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
