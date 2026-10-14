using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.UnitTests.Domain;

public class WhenNamingASealedFile
{
    // Derived offline: printf '<MarketMessage type="343"><Reading mprn="10000000001"/></MarketMessage>'
    // | shasum -a 256
    private const string KnownContent = """<MarketMessage type="343"><Reading mprn="10000000001"/></MarketMessage>""";
    private const string KnownContentSha256 = "aeb7ccb0448e1dab8781d5040c8329f4822b202d6c8f3e54b32cd6cbf639405c";

    private static readonly byte[] KnownBytes = System.Text.Encoding.UTF8.GetBytes(KnownContent);

    [Fact]
    public void TheHashIsTheSha256OfTheBytes()
    {
        var sealedFile = SealedFile.Of("roi-esb", KnownBytes);

        sealedFile.Hash.Should().Be(KnownContentSha256);
    }

    [Fact]
    public void ThePathIsTheMarketAndTheHash()
    {
        var sealedFile = SealedFile.Of("roi-esb", KnownBytes);

        sealedFile.Path.Should().Be($"roi-esb/{KnownContentSha256}.xml");
    }

    [Fact]
    public void TheSameContentFromTwoMarketsNamesTwoBlobs()
    {
        var esb = SealedFile.Of("roi-esb", KnownBytes);
        var gni = SealedFile.Of("roi-gni", KnownBytes);

        gni.Path.Should().NotBe(esb.Path);
        gni.Hash.Should().Be(esb.Hash);
    }
}
