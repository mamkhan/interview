using AwesomeAssertions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Domain;

public class WhenNamingAMarketIdentity
{
    [Fact]
    public void AcceptsALowercaseHyphenatedTag()
    {
        var identity = new MarketIdentity("roi-esb", "electricity");

        identity.Market.Should().Be("roi-esb");
        identity.Fuel.Should().Be("electricity");
    }

    [Theory]
    [InlineData("ROI-ESB")]
    [InlineData("roi esb")]
    [InlineData("roi_esb")]
    [InlineData("")]
    public void RefusesATagThatIsNotLowercaseHyphenated(string market)
    {
        // These values reach blob paths and subscription filters, where a case difference is a
        // subscription that silently receives nothing.
        var build = () => new MarketIdentity(market, "electricity");

        build.Should().Throw<ArgumentException>();
    }
}
