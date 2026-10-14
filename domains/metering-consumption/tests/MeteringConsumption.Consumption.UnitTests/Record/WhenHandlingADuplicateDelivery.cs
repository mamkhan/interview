using AwesomeAssertions;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// The same reading is delivered twice, an hour apart: it is kept once, and announced twice under the same message
// id with the same values, so the broker or the consumer discards the second. The body is not byte-identical in
// production: a re-announcement is read back from Parquet, where kWh carries six decimal places.
public sealed class WhenHandlingADuplicateDelivery : IAsyncLifetime
{
    private readonly RecordHandling _handling = new();

    public async ValueTask InitializeAsync()
    {
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().Build());
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().Build());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void KeepsTheEntryOnce() => _handling.Store.Appends.Should().Be(1);

    [Fact]
    public void AnnouncesItAgainUnderTheSameMessageId() =>
        _handling.Publisher.Published.Select(published => published.Metadata.MessageId).Should().Equal(
            "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00:10001234567:2026-06-20:1",
            "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00:10001234567:2026-06-20:1");

    [Fact]
    public void AnnouncesTheSameValuesBothTimes()
    {
        var announced = _handling.Announced.ToArray();

        announced[1].Should().BeEquivalentTo(announced[0]);
    }

    [Fact]
    public async Task AnnouncesItAgainWhenAnotherFileRepeatsTheVersion()
    {
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().FromFile("another-file").Build());

        _handling.Store.Appends.Should().Be(1);
        _handling.Publisher.Published.Last().Metadata.MessageId
            .Should().Be("c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00:10001234567:2026-06-20:1");
    }
}
