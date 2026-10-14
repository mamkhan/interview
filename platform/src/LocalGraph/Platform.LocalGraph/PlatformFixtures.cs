using Aspire.Hosting.Azure;
using Platform.Contracts;

namespace Platform.LocalGraph;

/// <summary>The local stand-ins for the platform layer's shared fixtures: one bus, one storage emulator,
/// one SQL server, and the topics platform services publish to. Domains add their own topics, containers and
/// databases to these, and their subscriptions to the platform's topics.</summary>
public sealed record PlatformFixtures(
    IResourceBuilder<AzureServiceBusResource> ServiceBus,
    IResourceBuilder<AzureStorageResource> Storage,
    IResourceBuilder<AzureSqlServerResource> Sql,
    IResourceBuilder<AzureServiceBusTopicResource> SupplyPointLoadedFromV1);

// The host ports are pinned so a SQL client or the acceptance suite finds the same address on every run,
// and none is a standard port, so the metering rail's dev stack on this machine runs beside it.
public static class PlatformGraph
{
    public const int SqlPort = 14330;
    public const string SqlPassword = "LocalDev!Passw0rd";
    public const int BlobPort = 10010;
    public const int QueuePort = 10011;
    public const int TablePort = 10012;
    public const int ServiceBusPort = 5673;

    public static PlatformFixtures AddPlatformFixtures(this IDistributedApplicationBuilder builder)
    {
        var serviceBus = builder.AddAzureServiceBus("messaging")
            .RunAsEmulator(emulator => emulator.WithHostPort(ServiceBusPort));
        var storage = builder.AddAzureStorage("storage")
            .RunAsEmulator(emulator => emulator.WithBlobPort(BlobPort).WithQueuePort(QueuePort).WithTablePort(TablePort));

        var sqlPassword = builder.AddParameter("sql-password", SqlPassword, secret: true);
        var sql = builder.AddAzureSqlServer("sql")
            .RunAsContainer(container => container.WithPassword(sqlPassword).WithHostPort(SqlPort));

        var supplyPointLoadedFromV1 = serviceBus.AddServiceBusTopic("supply-point-loaded-from-v1",
            PlatformTopology.Topics.SupplyPointLoadedFromV1);

        return new PlatformFixtures(serviceBus, storage, sql, supplyPointLoadedFromV1);
    }

    /// <summary>The local stand-in for an Event Grid BlobCreated subscription: a poller that announces each
    /// new blob in one container on one queue. A domain names the container and queue it watches.</summary>
    public static IResourceBuilder<ProjectResource> AddLocalBlobEvents<TProject>(
        this IDistributedApplicationBuilder builder,
        string name,
        PlatformFixtures platform,
        IResourceBuilder<AzureBlobStorageResource> blobs,
        string container,
        string queue)
        where TProject : IProjectMetadata, new() =>
        builder.AddProject<TProject>(name)
            .WithReference(platform.ServiceBus)
            .WithReference(blobs)
            .WaitFor(platform.ServiceBus)
            .WaitFor(platform.Storage)
            .WithEnvironment("BlobEvents__Container", container)
            .WithEnvironment("BlobEvents__Queue", queue);
}
