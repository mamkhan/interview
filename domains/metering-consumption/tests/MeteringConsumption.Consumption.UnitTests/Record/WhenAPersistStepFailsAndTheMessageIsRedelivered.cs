using AwesomeAssertions;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// A failure at either step propagates, so the message is abandoned and redelivered. Whichever step failed, the
// redelivery ends with the version kept once and announced under its one id.
public class WhenAPersistStepFailsAndTheMessageIsRedelivered
{
    private const string LedgerEntryId =
        "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00:10001234567:2026-06-20:1";

    private readonly RecordHandling _handling = new();

    [Fact]
    public async Task AnnouncesNothingWhenTheEntryCannotBeKept()
    {
        _handling.Store.FailNextAppend = true;

        var delivering = () => _handling.DeliverAsync(new IntervalReadingReceivedBuilder().Build());

        await delivering.Should().ThrowAsync<IOException>();
        _handling.Publisher.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task KeepsAndAnnouncesTheVersionOnTheRedeliveryAfterTheLedgerFailed()
    {
        _handling.Store.FailNextAppend = true;
        await Deliver().Should().ThrowAsync<IOException>();

        await Deliver()();

        _handling.Store.Appends.Should().Be(1);
        _handling.Publisher.Published.Should().ContainSingle().Which.Metadata.MessageId.Should().Be(LedgerEntryId);
    }

    [Fact]
    public async Task AnnouncesTheKeptVersionOnTheRedeliveryAfterPublishingFailed()
    {
        _handling.Publisher.FailNextPublish = true;
        await Deliver().Should().ThrowAsync<TimeoutException>();

        await Deliver()();

        _handling.Store.Appends.Should().Be(1);
        _handling.Log.Calls.Should().Equal("append", "publish", "publish");
        _handling.Publisher.Published.Should().ContainSingle().Which.Metadata.MessageId.Should().Be(LedgerEntryId);
    }

    [Fact]
    public async Task AnnouncesTheKeptVersionAtTheTimeItWasFirstPosted()
    {
        _handling.Publisher.FailNextPublish = true;
        await Deliver().Should().ThrowAsync<TimeoutException>();

        await Deliver()();

        _handling.Announced.Single().PostedAtUtc.Should().Be(RecordHandling.Now);
    }

    private Func<Task> Deliver() => () => _handling.DeliverAsync(new IntervalReadingReceivedBuilder().Build());
}
