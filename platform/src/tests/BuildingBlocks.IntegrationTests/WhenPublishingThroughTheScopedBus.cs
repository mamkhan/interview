using System.Text.Json;
using AwesomeAssertions;
using BuildingBlocks.IntegrationTests.Scenarios;

namespace BuildingBlocks.IntegrationTests;

// The tier itself: the production publisher sends on this class's scoped topic and the message is read
// back off the production-named subscription of the same scope.
public sealed class WhenPublishingThroughTheScopedBus(PublishedThroughTheScopedBusScenario published)
    : IClassFixture<PublishedThroughTheScopedBusScenario>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DeliversTheMessageToTheScopedSubscription() => published.Received.Should().NotBeNull();

    [Fact]
    public void KeepsTheMessageIdAndCorrelationId()
    {
        published.Received!.MessageId.Should().Be("ping-first");
        published.Received.CorrelationId.Should().Be("corr-first");
    }

    [Fact]
    public void StampsTheEventTypeAsTheSubject() =>
        published.Received!.Subject.Should().Be(typeof(PublishedThroughTheScopedBusScenario.Ping).FullName);

    [Fact]
    public void CarriesTheSerialisedBody() =>
        published.Received!.Body.ToObjectFromJson<PublishedThroughTheScopedBusScenario.Ping>(SerializerOptions)!
            .Name.Should().Be("first");
}
