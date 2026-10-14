using System.Net;
using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.MarketMessaging.Scenarios;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging;

namespace MeteringConsumption.AcceptanceTests.MarketMessaging;

// Every message type the platform reads goes from the drop container to a processed receipt in the running
// environment: Event Grid delivers it, the receiver seals it, its type's strategy publishes on topics that exist there,
// and the outcome is recorded. A topic missing from the estate shows here as a file with no outcome. The counts are
// what the builder's file of each type publishes, as the strategies' unit tests pin them.
public sealed class WhenEsbSendsEachMessageType(EsbSentEveryMessageTypeScenario sent)
    : IClassFixture<EsbSentEveryMessageTypeScenario>
{
    [Theory]
    [InlineData("101", 2)]
    [InlineData("105", 2)]
    [InlineData("300", 3)]
    [InlineData("300S", 3)]
    [InlineData("301", 2)]
    [InlineData("306", 3)]
    [InlineData("307", 3)]
    [InlineData("310", 3)]
    [InlineData("320", 3)]
    [InlineData("331", 1)]
    [InlineData("332", 3)]
    [InlineData("343", 1)]
    public void ProcessesTheFileAsItsTypeAndCountsWhatItPublished(string messageTypeCode, int published)
    {
        var landed = sent.Files[messageTypeCode];

        landed.Receipt.FileName.Should().Be(landed.FileName);
        landed.Receipt.MessageType.Should().Be(messageTypeCode);
        landed.Receipt.Outcome.Should().Be(MarketMessageOutcomes.Processed);
        landed.Receipt.ReadingsPublished.Should().Be(published);
    }

    [Fact]
    public void KeepsEverySealedPayloadInTheArchiveUnderTheMarketItArrivedFrom() =>
        sent.Files.Values.Should().AllSatisfy(landed =>
        {
            landed.Receipt.BlobPath.Should().StartWith($"{MeteringConsumptionTopology.Markets.RoiEsb}/");
            landed.IsArchived.Should().BeTrue();
        });

    [Fact]
    public void TakesEveryFileOutOfTheDropContainer() =>
        sent.Files.Values.Should().AllSatisfy(landed => landed.IsStillInTheDropContainer.Should().BeFalse());

    [Fact]
    public void StampsTheMarketAndFuelTheEnvironmentServes() =>
        sent.Files.Values.Should().AllSatisfy(landed =>
        {
            landed.Receipt.Market.Should().Be(MeteringConsumptionTopology.Markets.RoiEsb);
            landed.Receipt.Fuel.Should().Be(MarketMessagingTopology.Fuels.Electricity);
        });

    [Fact]
    public void AnswersTheHealthCheckOnTheProcessingHost() => sent.ProcessingHostHealth.Should().Be(HttpStatusCode.OK);

    [Fact]
    public void AnswersTheHealthCheckOnTheReceiverHost() => sent.ReceiverHostHealth.Should().Be(HttpStatusCode.OK);
}
