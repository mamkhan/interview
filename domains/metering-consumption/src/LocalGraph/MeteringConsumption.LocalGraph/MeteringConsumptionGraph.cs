using Aspire.Hosting.Azure;
using Growth.LocalGraph;
using MeteringConsumption.Consumption;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.Contracts;
using MeteringConsumption.MarketMessaging;
using MeteringConsumption.SupplyPoints;
using Platform.LocalGraph;

namespace MeteringConsumption.LocalGraph;

// What this domain adds to the shared fixtures locally: its topics, subscriptions, queue,
// containers and database, named from the contracts topology so the AppHost and the hosts agree.
public sealed record MeteringConsumptionFixtures(
    PlatformFixtures Platform,
    IResourceBuilder<AzureServiceBusResource> ServiceBus,
    IResourceBuilder<AzureStorageResource> Storage,
    IResourceBuilder<AzureBlobStorageResource> Blobs,
    IResourceBuilder<AzureSqlDatabaseResource> Database,
    IResourceBuilder<AzureSqlDatabaseResource> ReferenceData);

// The domain's local graph as extension methods, so the domain AppHost and the platform
// AppHost compose the identical graph and neither can drift from the other.
public static class MeteringConsumptionGraph
{
    // Local only: nothing in the domain consumes ConsumptionRecorded yet, and a topic with no subscription
    // discards what is published, so a local run reads the announcements here.
    public const string ObserveConsumptionRecordedSubscription = "metr.observe-consumption-recorded";

    // Local only, for the same reason: Billing & Pay's subscription is its own, so a run reads forecasts here.
    public const string ObserveConsumptionForecastUpdatedSubscription = "metr.observe-consumption-forecast-updated";

    public const int ReceiverHostPort = 5330;
    public const int MeteringConsumptionHostPort = 5320;

    public static MeteringConsumptionFixtures AddMeteringConsumptionFixtures(
        this IDistributedApplicationBuilder builder, PlatformFixtures platform, GrowthFixtures growth)
    {
        var bus = platform.ServiceBus;

        bus.AddServiceBusQueue("receive-blob-created", MarketMessagingTopology.Queues.ReceiveBlobCreated);

        var received = bus.AddServiceBusTopic("market-message-received", MarketMessagingTopology.Topics.MarketMessageReceived);
        received.AddServiceBusSubscription("audit-market-message-received", MarketMessagingTopology.Subscriptions.AuditMarketMessageReceived);
        received.AddServiceBusSubscription("interpret-market-message-received", MarketMessagingTopology.Subscriptions.InterpretMarketMessageReceived);

        bus.AddServiceBusTopic("market-message-processed", MarketMessagingTopology.Topics.MarketMessageProcessed)
            .AddServiceBusSubscription("audit-market-message-processed", MarketMessagingTopology.Subscriptions.AuditMarketMessageProcessed);
        bus.AddServiceBusTopic("market-message-failed", MarketMessagingTopology.Topics.MarketMessageFailed)
            .AddServiceBusSubscription("audit-market-message-failed", MarketMessagingTopology.Subscriptions.AuditMarketMessageFailed);

        bus.AddServiceBusTopic("interval-reading-received", MeteringConsumptionTopology.Topics.IntervalReadingReceived)
            .AddServiceBusSubscription("record-interval-reading-received",
                ConsumptionTopology.Subscriptions.RecordIntervalReadingReceived)
            .WithProperties(subscription => subscription.RequiresSession = true);
        var recordedTopic = bus.AddServiceBusTopic("consumption-recorded", MeteringConsumptionTopology.Topics.ConsumptionRecorded);
        recordedTopic.AddServiceBusSubscription("observe-consumption-recorded", ObserveConsumptionRecordedSubscription);
        recordedTopic.AddServiceBusSubscription("forecast-consumption-recorded",
                ConsumptionTopology.Subscriptions.ForecastConsumptionRecorded)
            .WithProperties(subscription => subscription.RequiresSession = true);
        bus.AddServiceBusTopic("consumption-forecast-updated", MeteringConsumptionTopology.Topics.ConsumptionForecastUpdated)
            .AddServiceBusSubscription("observe-consumption-forecast-updated", ObserveConsumptionForecastUpdatedSubscription);

        bus.AddServiceBusTopic("meter-configuration-received", MeteringConsumptionTopology.Topics.MeterConfigurationReceived)
            .AddServiceBusSubscription("profile-meter-configuration-received",
                ConsumptionTopology.Subscriptions.ProfileMeterConfigurationReceived)
            .WithProperties(subscription => subscription.RequiresSession = true);
        bus.AddServiceBusTopic("usage-factors-received", MeteringConsumptionTopology.Topics.UsageFactorsReceived)
            .AddServiceBusSubscription("profile-usage-factors-received",
                ConsumptionTopology.Subscriptions.ProfileUsageFactorsReceived)
            .WithProperties(subscription => subscription.RequiresSession = true);

        // The subscription carries its topic's name; its resource label names the module because Aspire labels must be
        // unique across the graph.
        bus.AddServiceBusTopic("meter-configuration-change-received", MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived)
            .AddServiceBusSubscription("supplypoints-meter-configuration-change-received",
                SupplyPointTopology.Subscriptions.MeterConfigurationChangeReceived)
            .WithProperties(subscription => subscription.RequiresSession = true);
        platform.SupplyPointLoadedFromV1
            .AddServiceBusSubscription("supplypoints-supply-point-loaded-from-v1", SupplyPointTopology.Subscriptions.SupplyPointLoadedFromV1);
        growth.TariffResolved
            .AddServiceBusSubscription("profile-tariff-resolved", ConsumptionTopology.Subscriptions.ProfileTariffResolved)
            .WithProperties(subscription => subscription.RequiresSession = true);

        var blobs = platform.Storage.AddBlobs("blobs");
        platform.Storage.AddBlobContainer("esb-drop", MarketMessagingTopology.Containers.EsbDrop);
        platform.Storage.AddBlobContainer("market-message-archive", MarketMessagingTopology.Containers.MarketMessageArchive);
        platform.Storage.AddBlobContainer("consumption-ledger", MeteringConsumptionTopology.Containers.ConsumptionLedger);

        var database = platform.Sql.AddDatabase("metering-consumption");
        var referenceData = platform.Sql.AddDatabase(ReferenceDataSchema.ConnectionName);

        return new MeteringConsumptionFixtures(platform, bus, platform.Storage, blobs, database, referenceData);
    }

    public static IResourceBuilder<ProjectResource> AddReceiverHost<TProject>(
        this IDistributedApplicationBuilder builder, MeteringConsumptionFixtures fixtures)
        where TProject : IProjectMetadata, new() =>
        builder.AddProject<TProject>("market-messaging-receiver")
            .WithReference(fixtures.ServiceBus)
            .WithReference(fixtures.Blobs)
            .WaitFor(fixtures.ServiceBus)
            .WaitFor(fixtures.Storage)
            .WithEnvironment("Drop__Container", MarketMessagingTopology.Containers.EsbDrop)
            .WithEnvironment("Drop__Market", MeteringConsumptionTopology.Markets.RoiEsb)
            .WithEnvironment("Drop__Fuel", MarketMessagingTopology.Fuels.Electricity)
            .WithEndpoint("http", endpoint => endpoint.Port = ReceiverHostPort)
            .WithHttpHealthCheck("/health");

    public static IResourceBuilder<ProjectResource> AddMeteringConsumptionHost<TProject>(
        this IDistributedApplicationBuilder builder, MeteringConsumptionFixtures fixtures)
        where TProject : IProjectMetadata, new() =>
        builder.AddProject<TProject>("metering-consumption-host")
            .WithReference(fixtures.ServiceBus)
            .WithReference(fixtures.Blobs)
            .WithReference(fixtures.Blobs, RecordOptions.LedgerConnectionName)
            .WithReference(fixtures.Database)
            .WithReference(fixtures.ReferenceData)
            .WaitFor(fixtures.ServiceBus)
            .WaitFor(fixtures.Storage)
            .WaitFor(fixtures.Database)
            .WaitFor(fixtures.ReferenceData)
            .WithEndpoint("http", endpoint => endpoint.Port = MeteringConsumptionHostPort)
            .WithHttpHealthCheck("/health");

    // Under `aspire run` nothing raises BlobCreated for Azurite, so this poller stands in for
    // the Event Grid subscription on the drop container.
    public static IResourceBuilder<ProjectResource> AddEsbDropBlobEvents<TProject>(
        this IDistributedApplicationBuilder builder, MeteringConsumptionFixtures fixtures)
        where TProject : IProjectMetadata, new() =>
        builder.AddLocalBlobEvents<TProject>(
            "esb-drop-blob-events",
            fixtures.Platform,
            fixtures.Blobs,
            MarketMessagingTopology.Containers.EsbDrop,
            MarketMessagingTopology.Queues.ReceiveBlobCreated);
}
