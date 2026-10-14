using System.Xml;
using AwesomeAssertions;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Domain;
using MeteringConsumption.MarketMessaging.UnitTests.Interpret.Scenarios;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

public class WhenAMarketMessageIsReceived
{
    private readonly ReceivedMessageScenario _scenario = new();

    public WhenAMarketMessageIsReceived()
    {
        _scenario.Handler().HandleAsync(ReceivedMessageScenario.Receipt(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void ClassifiesFromTheOperatorsHeader() => _scenario.Processed.MessageType.Should().Be("343");

    [Fact]
    public void DispatchesToTheInterpreterForTheMarketAndType() =>
        _scenario.Mm343.Interpreted.Should().ContainSingle().Which.ReceiptId.Should().Be(ReceivedMessageScenario.ReceiptId);

    [Fact]
    public void PublishesProcessedWithTheTypeInterpreterAndCount()
    {
        _scenario.Processed.Outcome.Should().Be(MarketMessageOutcomes.Processed);
        _scenario.Processed.MessageType.Should().Be("343");
        _scenario.Processed.Interpreter.Should().Be("RecordingInterpreter");
        _scenario.Processed.ReadingsPublished.Should().Be(2);
        _scenario.Processed.ReceiptId.Should().Be(ReceivedMessageScenario.ReceiptId);
        _scenario.Processed.OccurredAtUtc.Should().Be(ReceivedMessageScenario.Now);
    }

    [Fact]
    public void PublishesTheOutcomeAfterTheInterpreterFinishes() =>
        _scenario.Log.Calls.Should().Equal("open", "interpret", "publish:MarketMessageProcessed");

    [Fact]
    public void UsesTheReceiptIdAsTheOutcomeMessageId()
    {
        var metadata = _scenario.Publisher.Published.Single().Metadata;

        metadata.MessageId.Should().Be(ReceivedMessageScenario.ReceiptId);
        metadata.CorrelationId.Should().Be(ReceivedMessageScenario.ReceiptId);
    }
}

public class WhenAReceivedMessageHasNoInterpreter
{
    private readonly ReceivedMessageScenario _scenario = new() { Classifier = new StubClassifier("010") };

    public WhenAReceivedMessageHasNoInterpreter()
    {
        _scenario.Handler().HandleAsync(ReceivedMessageScenario.Receipt(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void PublishesNoInterpreterWithTheType()
    {
        _scenario.Processed.Outcome.Should().Be(MarketMessageOutcomes.NoInterpreter);
        _scenario.Processed.MessageType.Should().Be("010");
        _scenario.Processed.Interpreter.Should().BeNull();
    }

    [Fact]
    public void PublishesNoReadings()
    {
        _scenario.Mm343.Interpreted.Should().BeEmpty();
        _scenario.Processed.ReadingsPublished.Should().Be(0);
    }
}

public class WhenAMessageCannotBeClassified
{
    private readonly ReceivedMessageScenario _scenario = new() { Classifier = new RefusingClassifier("The file has no MessageHeader element.") };
    private readonly Exception? _thrown;

    public WhenAMessageCannotBeClassified()
    {
        try
        {
            _scenario.Handler().HandleAsync(ReceivedMessageScenario.Receipt(), CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _thrown = exception;
        }
    }

    [Fact]
    public void PublishesFailedWithTheReason()
    {
        _scenario.Failed.Reason.Should().Be("The file has no MessageHeader element.");
        _scenario.Failed.MessageType.Should().BeNull();
        _scenario.Failed.ReceiptId.Should().Be(ReceivedMessageScenario.ReceiptId);
        _scenario.Publisher.Published.Single().Metadata.MessageId.Should().Be(ReceivedMessageScenario.ReceiptId);
    }

    [Fact]
    public void PublishesNoReadings() => _scenario.Mm343.Interpreted.Should().BeEmpty();

    [Fact]
    public void CompletesRatherThanThrowing() => _thrown.Should().BeNull();
}

public class WhenAnInterpreterRejectsThePayload
{
    private readonly ReceivedMessageScenario _scenario = new();
    private readonly Exception? _thrown;

    public WhenAnInterpreterRejectsThePayload()
    {
        _scenario.Mm343.Throws = new MarketMessageRejectedException("MPRNLevelInfo is missing the required attribute ReadDate.");
        try
        {
            _scenario.Handler().HandleAsync(ReceivedMessageScenario.Receipt(), CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _thrown = exception;
        }
    }

    [Fact]
    public void PublishesFailedWithTheReason()
    {
        _scenario.Failed.Reason.Should().Be("MPRNLevelInfo is missing the required attribute ReadDate.");
        _scenario.Failed.MessageType.Should().Be("343");
    }

    [Fact]
    public void PublishesNoReadings() => _scenario.Publisher.Published.Should().ContainSingle();

    [Fact]
    public void CompletesRatherThanThrowing() => _thrown.Should().BeNull();

    [Fact]
    public async Task TreatsMalformedXmlTheSameWay()
    {
        var malformed = new ReceivedMessageScenario();
        malformed.Mm343.Throws = new XmlException("Unexpected end of file.");

        await malformed.Handler().HandleAsync(ReceivedMessageScenario.Receipt(), CancellationToken.None);

        malformed.Failed.Reason.Should().Be("Unexpected end of file.");
    }
}

public class WhenTheArchiveIsUnreachable
{
    [Fact]
    public async Task PropagatesSoTheMessageIsRetried()
    {
        var scenario = new ReceivedMessageScenario();
        scenario.Archive.Throws = new TimeoutException("storage unavailable");

        var act = () => scenario.Handler().HandleAsync(ReceivedMessageScenario.Receipt(), CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        scenario.Publisher.Published.Should().BeEmpty();
    }
}
