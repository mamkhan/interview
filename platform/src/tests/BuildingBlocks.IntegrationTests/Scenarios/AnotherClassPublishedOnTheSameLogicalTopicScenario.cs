using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;

namespace BuildingBlocks.IntegrationTests.Scenarios;

public sealed class AnotherClassPublishedOnTheSameLogicalTopicScenario : IAsyncLifetime
{
    private SuiteScope? _scope;
    private ServiceBusClient? _client;

    public ServiceBusReceivedMessage? Received { get; private set; }

    public ServiceBusReceivedMessage? NextAfterOwn { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenAnotherClassPublishesOnTheSameLogicalTopic),
            new SuiteResources { Topics = [new BusEntity(Suite.EventsTopic, Suite.TestReader)] },
            cancellationToken);

        _client = new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString);
        var publisher = new ServiceBusEventPublisher(_client,
            new EventTopicMap().Register<Ping>(scope.Topic(Suite.EventsTopic)), Suite.UnprefixedNames);
        await publisher.PublishAsync(new Ping("second"),
            new EventMetadata { MessageId = "ping-second", CorrelationId = "corr-second" }, cancellationToken);
        await publisher.PublishAsync(new Ping("sentinel"),
            new EventMetadata { MessageId = "ping-sentinel", CorrelationId = "corr-sentinel" }, cancellationToken);

        // The sentinel follows this class's own message, so whatever the subscription holds between the two came
        // from another scope; nothing has to be waited out.
        await using var receiver = _client.CreateScopedReceiver(scope, Suite.EventsTopic, Suite.TestReader);
        Received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken);
        NextAfterOwn = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        if (_scope is not null)
        {
            await _scope.DisposeAsync();
        }
    }

    public sealed record Ping(string Name);
}
