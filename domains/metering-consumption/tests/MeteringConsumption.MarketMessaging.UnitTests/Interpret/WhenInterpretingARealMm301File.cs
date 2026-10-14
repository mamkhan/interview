using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM301 interpreter over a real ESB change of meter point characteristics, its MPRN, transaction reference and
// premises address replaced by fake ones and every other value as ESB sent it. The file states a configuration code
// and DUoS group but no load profile and no status. Expected values read offline from the file's text, sharing no
// code with the parser: MCC12, DUOS group DG1, effective 2026-10-01, header MarketTimestamp 2026-10-01T15:14:05.066Z;
// sha256 of the file 46167dc43b4fe650fe56586aa236af526a01330d159ac30e0f483680458770f3 (sha256sum).
public class WhenInterpretingARealMm301File
{
    private const string FileName = "ESB_MM301_10900001301_0000000790001301.xml";
    private const string Mprn = "10900001301";
    private const string Hash = "46167dc43b4fe650fe56586aa236af526a01330d159ac30e0f483680458770f3";
    private const string ReceiptId = "6b2e8c4f-0a7d-4e39-b1c5-9d6e3a5b7c28";

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm301File()
    {
        var interpreter = new MM301Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 15, 30, 3, TimeSpan.Zero)),
                                            NullLogger<MM301Strategy>.Instance);
        using var payload = File.OpenRead($"samples/{FileName}");
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    private MeterConfigurationReceived MeterConfiguration => _publisher.Events.OfType<MeterConfigurationReceived>().Single();

    [Fact]
    public void PublishesOnlyTheMeterConfigurationAndSupplyPointsChange() =>
        _publisher.Events.Select(e => e.GetType()).Should().Equal(typeof(MeterConfigurationReceived), typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheCodesTheFileStates()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.MeterConfigurationCode.Should().Be("MCC12");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
    }

    [Fact]
    public void LeavesTheCodesTheFileDoesNotStateEmpty()
    {
        MeterConfiguration.LoadProfileCode.Should().BeNull();
        MeterConfiguration.MeterPointStatusCode.Should().BeNull();
        MeterConfiguration.NetworksReferenceNumber.Should().BeNull();
    }

    [Fact]
    public void DatesTheConfigurationByItsEffectiveDate()
    {
        MeterConfiguration.EffectiveFromDate.Should().Be(new DateOnly(2026, 10, 1));
        MeterConfiguration.ReadDate.Should().BeNull();
    }

    [Fact]
    public void ReturnsBothAsPublished() => _outcome.ReadingsPublished.Should().Be(2);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 10, 1, 15, 30, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 10, 1, 15, 30, 0, TimeSpan.Zero),
        Hash = Hash,
        BlobPath = $"roi-esb/{Hash}.xml",
    };

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<IEvent> Events { get; } = [];

        public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
            where TEvent : class => PublishAsync(message, typeof(TEvent), metadata, cancellationToken);

        public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken)
        {
            Events.Add((IEvent)message);
            return Task.CompletedTask;
        }

        public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
