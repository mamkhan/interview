using AwesomeAssertions;
using BuildingBlocks.IntegrationTests.Scenarios;

namespace BuildingBlocks.IntegrationTests;

// Runs in parallel with WhenPublishingThroughTheScopedBus on the same logical topic and subscription
// names: each class sees only what it published because the scope prefix makes the entities distinct.
public sealed class WhenAnotherClassPublishesOnTheSameLogicalTopic(
    AnotherClassPublishedOnTheSameLogicalTopicScenario published)
    : IClassFixture<AnotherClassPublishedOnTheSameLogicalTopicScenario>
{
    [Fact]
    public void ReceivesItsOwnMessage() => published.Received!.MessageId.Should().Be("ping-second");

    [Fact]
    public void ReceivesNothingFromTheOtherScope() => published.NextAfterOwn!.MessageId.Should().Be("ping-sentinel");
}
