using AwesomeAssertions;
using BuildingBlocks.IntegrationTests.Scenarios;

namespace BuildingBlocks.IntegrationTests;

// A consumer's clock is the message's AsOf when the envelope carries one and the system clock
// otherwise; the handler sees it through the TimeProvider the host registers, nothing else.
public sealed class WhenConsumingAMessageThatCarriesAnAsOf(MessageWithAnAsOfConsumedScenario consumed)
    : IClassFixture<MessageWithAnAsOfConsumedScenario>
{
    private static readonly DateTimeOffset ReplayedAt = new(2026, 3, 1, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ReadsTheAsOfAsNow() => consumed.Observed["replayed"].Should().Be(ReplayedAt);

    [Fact]
    public void ReadsTheSystemClockWhenThereIsNoAsOf() =>
        consumed.Observed["live"].Should().BeOnOrAfter(consumed.Before).And.BeOnOrBefore(consumed.After);
}
