using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using BuildingBlocks.IntegrationTests.Scenarios;

namespace BuildingBlocks.IntegrationTests;

// The consumer over a queue rather than a topic and subscription: a message enqueued is delivered to
// the handler and completed, so it is not seen again.
[SuppressMessage("Naming", "CA1711", Justification = "A scenario name, not a collection type.")]
public sealed class WhenConsumingFromAQueue(ConsumedFromAQueueScenario consumed)
    : IClassFixture<ConsumedFromAQueueScenario>
{
    [Fact]
    public void DeliversTheMessageToTheHandler() => consumed.Delivered.Should().Equal("only");

    [Fact]
    public void DoesNotRedeliverACompletedMessage() => consumed.LeftOnTheQueue.Should().BeNull();
}
