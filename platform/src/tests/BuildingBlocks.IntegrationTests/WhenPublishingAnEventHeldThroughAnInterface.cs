using System.Text.Json;
using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;

namespace BuildingBlocks.IntegrationTests;

// A caller holding its events through a shared interface publishes each by its runtime type: the topic, the subject
// and the body are the concrete type's, so nothing is resolved or serialised as the interface.
public sealed class WhenPublishingAnEventHeldThroughAnInterface(WhenPublishingAnEventHeldThroughAnInterface.Published published)
    : IClassFixture<WhenPublishingAnEventHeldThroughAnInterface.Published>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DeliversItOnTheTopicRegisteredForTheRuntimeType() => published.Received.Should().NotBeNull();

    [Fact]
    public void StampsTheRuntimeTypeAsTheSubject() =>
        published.Received!.Subject.Should().Be(typeof(Circle).FullName);

    [Fact]
    public void SerialisesTheRuntimeTypesPropertiesNotOnlyTheInterfaces()
    {
        var body = published.Received!.Body.ToObjectFromJson<Circle>(SerializerOptions)!;

        body.Name.Should().Be("wheel");
        body.Radius.Should().Be(2.5m);
    }

    public interface IShape
    {
        string Name { get; }
    }

    public sealed record Circle(string Name, decimal Radius) : IShape;

    public sealed class Published : IAsyncLifetime
    {
        private SuiteScope? _scope;
        private ServiceBusClient? _client;

        public ServiceBusReceivedMessage? Received { get; private set; }

        public async ValueTask InitializeAsync()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenPublishingAnEventHeldThroughAnInterface),
                new SuiteResources { Topics = [new BusEntity(Suite.EventsTopic, Suite.TestReader)] },
                cancellationToken);

            _client = new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString);
            var publisher = new ServiceBusEventPublisher(_client,
                new EventTopicMap().Register<Circle>(scope.Topic(Suite.EventsTopic)), Suite.UnprefixedNames);

            IShape shape = new Circle("wheel", 2.5m);
            await publisher.PublishAsync(shape, shape.GetType(),
                new EventMetadata { MessageId = "circle-wheel", CorrelationId = "corr-wheel" }, cancellationToken);

            await using var receiver = _client.CreateScopedReceiver(scope, Suite.EventsTopic, Suite.TestReader);
            Received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken);
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
    }
}
