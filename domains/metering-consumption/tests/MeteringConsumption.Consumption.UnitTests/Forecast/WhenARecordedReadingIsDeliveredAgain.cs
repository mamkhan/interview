using AwesomeAssertions;

namespace MeteringConsumption.Consumption.UnitTests.Forecast;

// Service Bus delivers at least once. A reading whose forecast is stored is never recomputed: while that forecast is
// current it is announced again under the same id, which repairs a crash between storing and announcing; once a later
// reading has superseded it, it was announced before that reading was handled, so nothing is sent.
public sealed class WhenARecordedReadingIsDeliveredAgain
{
    private readonly ForecastHandling _handling = new();

    [Fact]
    public async Task AnnouncesTheStoredForecastAgainAfterAFailedAnnouncement()
    {
        _handling.Publisher.FailNextPublish = true;
        var first = () => _handling.DeliverAsync(ForecastHandling.Recorded());
        await first.Should().ThrowAsync<TimeoutException>();

        await _handling.DeliverAsync(ForecastHandling.Recorded());

        _handling.Store.Versions.Should().ContainSingle();
        _handling.Publisher.Published.Single().Metadata.MessageId.Should().Be("10001234567:1");
    }

    [Fact]
    public async Task AnnouncesTheCurrentForecastAgainUnderTheSameId()
    {
        await _handling.DeliverAsync(ForecastHandling.Recorded());
        await _handling.DeliverAsync(ForecastHandling.Recorded());

        _handling.Store.Versions.Should().ContainSingle();
        _handling.Publisher.Published.Select(published => published.Metadata.MessageId)
            .Should().Equal("10001234567:1", "10001234567:1");
    }

    [Fact]
    public async Task SendsNothingForAReadingWhoseForecastWasSuperseded()
    {
        await _handling.DeliverAsync(ForecastHandling.Recorded());
        await _handling.DeliverAsync(ForecastHandling.Recorded("hash:10001234567:2026-04-30:2"));

        await _handling.DeliverAsync(ForecastHandling.Recorded());

        _handling.Store.Versions.Should().HaveCount(2);
        _handling.Publisher.Published.Select(published => published.Metadata.MessageId)
            .Should().Equal("10001234567:1", "10001234567:2");
    }
}
