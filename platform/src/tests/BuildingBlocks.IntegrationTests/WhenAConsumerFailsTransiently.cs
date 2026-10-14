using AwesomeAssertions;
using BuildingBlocks.IntegrationTests.Scenarios;

namespace BuildingBlocks.IntegrationTests;

// A handler that fails with anything but a domain rule or a validation: the consumer abandons the message, Service Bus
// delivers it again at once rather than when the minute's lock runs out, and the second delivery succeeds and
// completes it. Every module consumer inherits this.
public sealed class WhenAConsumerFailsTransiently(FailedOnceOnAQueueScenario failed)
    : IClassFixture<FailedOnceOnAQueueScenario>
{
    [Fact]
    public void DeliversItAgainAfterTheFailure() => failed.Attempts.Should().Equal("flaky", "flaky");

    [Fact]
    public void DeliversItAgainWithoutWaitingForTheLockToExpire() =>
        failed.UntilHandled.Should().BeLessThan(TimeSpan.FromSeconds(30));

    [Fact]
    public void CompletesItOnceTheHandlerSucceeds()
    {
        failed.LeftOnTheQueue.Should().BeNull();
        failed.DeadLettered.Should().BeNull();
    }
}
