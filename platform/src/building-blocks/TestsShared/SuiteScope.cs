using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.TestsShared;

// One test class's isolation on the shared stack, id `<suite>-<class>`: its own database, its topics
// and queues under that prefix, its blob containers under that prefix, all reset on the way in because
// the containers outlive the run and a crashed run must not poison the next one. Subscriptions keep
// their production names: they are already scoped by the prefixed topic, so the suites exercise the
// names the hosts use. The emulator caps a namespace at 50 topics, fewer than a whole run's classes
// create, so disposing a scope deletes the topics and queues it created.
public sealed class SuiteScope : IAsyncDisposable
{
    private readonly List<string> _topics = [];
    private readonly List<string> _queues = [];

    private SuiteScope(string id, SharedEndpoints endpoints, string? sqlConnectionString)
    {
        Id = id;
        Endpoints = endpoints;
        SqlConnectionString = sqlConnectionString;
    }

    public string Id { get; }

    public SharedEndpoints Endpoints { get; }

    public string? SqlConnectionString { get; }

    public string DatabaseName => $"acme_{Id.Replace('-', '_')}";

    public string Topic(string logicalName) => Prefixed(logicalName);

    public string Queue(string logicalName) => Prefixed(logicalName);

    public static string Subscription(string logicalName) => logicalName;

    public string BlobContainer(string logicalName)
    {
        var name = Prefixed(logicalName);
        return name.Length <= 63
            ? name
            : throw new ArgumentException(
                $"Blob container name '{name}' is {name.Length} characters; storage allows 63. Shorten the suite id or the class name.",
                nameof(logicalName));
    }

    public static async Task<SuiteScope> CreateAsync(string suite, string testClass, SuiteResources resources,
        CancellationToken cancellationToken = default)
    {
        var endpoints = await SharedPlatformContainers.EnsureAsync(
            needsSql: resources.Migrate is not null,
            needsServiceBus: resources.Topics.Count > 0 || resources.Queues.Count > 0,
            needsAzurite: resources.BlobContainers.Count > 0);

        var id = $"{suite}-{testClass.ToLowerInvariant()}";
        var scope = new SuiteScope(id, endpoints, null);
        if (resources.Migrate is not null)
        {
            scope = new SuiteScope(id, endpoints, await scope.ResetSqlAsync(resources.Migrate, cancellationToken));
        }

        await scope.ResetServiceBusAsync(resources.Topics, resources.Queues, cancellationToken);
        await scope.ResetBlobContainersAsync(resources.BlobContainers, cancellationToken);
        return scope;
    }

    public async ValueTask DisposeAsync()
    {
        if (_topics.Count == 0 && _queues.Count == 0)
        {
            return;
        }

        var admin = new ServiceBusAdministrationClient(Endpoints.ServiceBusAdminConnectionString);

        foreach (var topic in _topics)
        {
            if (await admin.TopicExistsAsync(topic))
            {
                await admin.DeleteTopicAsync(topic);
            }
        }

        foreach (var queue in _queues)
        {
            if (await admin.QueueExistsAsync(queue))
            {
                await admin.DeleteQueueAsync(queue);
            }
        }

        _topics.Clear();
        _queues.Clear();
    }

    private string Prefixed(string logicalName) => $"{Id}-{logicalName}";

    // SINGLE_USER WITH ROLLBACK IMMEDIATE because a previous run may have left pooled connections open.
    private async Task<string> ResetSqlAsync(Func<string, Task> migrate, CancellationToken cancellationToken)
    {
        var master = Endpoints.SqlMasterConnectionString!;
        await using (var connection = new SqlConnection(master))
        {
            await connection.OpenAsync(cancellationToken);
            await ExecuteAsync(connection,
                $"""
                 IF DB_ID('{DatabaseName}') IS NOT NULL
                 BEGIN
                     ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                     DROP DATABASE [{DatabaseName}];
                 END
                 """, cancellationToken);
            await ExecuteAsync(connection, $"CREATE DATABASE [{DatabaseName}];", cancellationToken);
        }

        var connectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = DatabaseName }.ConnectionString;
        await migrate(connectionString);
        return connectionString;
    }

    // Deleting an entity takes its subscriptions and in-flight messages with it, so each run starts empty.
    private async Task ResetServiceBusAsync(IReadOnlyList<BusEntity> topics, IReadOnlyList<QueueEntity> queues,
        CancellationToken cancellationToken)
    {
        if (topics.Count == 0 && queues.Count == 0)
        {
            return;
        }

        var admin = new ServiceBusAdministrationClient(Endpoints.ServiceBusAdminConnectionString);

        foreach (var group in topics.GroupBy(entity => entity.Topic))
        {
            var topic = Topic(group.Key);
            if (await admin.TopicExistsAsync(topic, cancellationToken))
            {
                await admin.DeleteTopicAsync(topic, cancellationToken);
            }

            await admin.CreateTopicAsync(new CreateTopicOptions(topic)
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1),
            }, cancellationToken);
            _topics.Add(topic);

            foreach (var entity in group)
            {
                await admin.CreateSubscriptionAsync(new CreateSubscriptionOptions(topic, Subscription(entity.Subscription))
                {
                    DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                    LockDuration = TimeSpan.FromMinutes(1),
                    MaxDeliveryCount = 10,
                    RequiresSession = entity.RequiresSession,
                }, cancellationToken);
            }
        }

        foreach (var entity in queues)
        {
            var queue = Queue(entity.Name);
            if (await admin.QueueExistsAsync(queue, cancellationToken))
            {
                await admin.DeleteQueueAsync(queue, cancellationToken);
            }

            await admin.CreateQueueAsync(new CreateQueueOptions(queue)
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                LockDuration = TimeSpan.FromMinutes(1),
                MaxDeliveryCount = 10,
            }, cancellationToken);
            _queues.Add(queue);
        }
    }

    // Delete-then-create is not instant on Azurite (a deleted name is briefly unusable), so the contents
    // are cleared instead. Azurite rejects the SDK's newest service version, hence the pin.
    private async Task ResetBlobContainersAsync(IReadOnlyList<string> containers, CancellationToken cancellationToken)
    {
        if (containers.Count == 0)
        {
            return;
        }

        var service = new BlobServiceClient(Endpoints.AzuriteConnectionString,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03));

        foreach (var logicalName in containers)
        {
            var container = service.GetBlobContainerClient(BlobContainer(logicalName));
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                await container.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: cancellationToken);
            }
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

// What a test class needs from the stack. A database exists only when a migrator is given, and it is
// built by that migrator; the emulator and Azurite are started only when an entity or container is named.
public sealed record SuiteResources
{
    public Func<string, Task>? Migrate { get; init; }

    public IReadOnlyList<BusEntity> Topics { get; init; } = [];

    public IReadOnlyList<QueueEntity> Queues { get; init; } = [];

    public IReadOnlyList<string> BlobContainers { get; init; } = [];
}

public sealed record BusEntity(string Topic, string Subscription, bool RequiresSession = false);

public sealed record QueueEntity(string Name);
