using AwesomeAssertions;
using BuildingBlocks.IntegrationTests.Scenarios;

namespace BuildingBlocks.IntegrationTests;

// A handler that throws a domain rule the same bytes will break again: redelivery cannot help, so the consumer
// dead-letters the message on its first delivery, named for the rule, and never runs the handler again. Every module
// consumer inherits this; its own tests prove only that its rule rejects.
public sealed class WhenAConsumerRejectsAMessage(RejectedOnAQueueScenario rejected)
    : IClassFixture<RejectedOnAQueueScenario>
{
    [Fact]
    public void DeadLettersItOnItsFirstDelivery()
    {
        rejected.DeadLettered!.MessageId.Should().Be("undated");
        rejected.DeadLettered.DeliveryCount.Should().Be(1);
    }

    [Fact]
    public void NamesTheRuleItBroke()
    {
        rejected.DeadLettered!.DeadLetterReason.Should().Be(nameof(RejectedOnAQueueScenario.NoPlaceForIt));
        rejected.DeadLettered.DeadLetterErrorDescription.Should().Be("the statement carries no date to place it on");
    }

    [Fact]
    public void RunsTheHandlerOnce() => rejected.Attempts.Should().Equal("undated");
}
