using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.MarketMessaging.Scenarios;
using MeteringConsumption.Contracts.Events;

namespace MeteringConsumption.AcceptanceTests.MarketMessaging;

// A file the platform cannot turn into events still ends with an outcome on its receipt, so an operator can find it,
// and its payload is kept: a message type no strategy reads is recorded as having no interpreter, and a file that is
// not XML as failed, with the reason the classifier gave.
public sealed class WhenEsbSendsFilesNoStrategyReads(EsbSentFilesNoStrategyReadsScenario sent)
    : IClassFixture<EsbSentFilesNoStrategyReadsScenario>
{
    [Fact]
    public void RecordsAMessageTypeNoStrategyReadsAsHavingNoInterpreter()
    {
        sent.UnreadType.Outcome.Should().Be(MarketMessageOutcomes.NoInterpreter);
        sent.UnreadType.MessageType.Should().Be("017");
    }

    [Fact]
    public void RecordsAFileThatIsNotXmlAsFailedWithTheReason()
    {
        sent.Unreadable.Outcome.Should().Be(MarketMessageOutcomes.Failed);
        sent.Unreadable.MessageType.Should().BeNull();
        sent.Unreadable.FailureReason.Should().StartWith("The file is not readable XML");
    }

    [Fact]
    public void KeepsBothPayloadsInTheArchive()
    {
        sent.UnreadTypeIsArchived.Should().BeTrue();
        sent.UnreadableIsArchived.Should().BeTrue();
    }
}
