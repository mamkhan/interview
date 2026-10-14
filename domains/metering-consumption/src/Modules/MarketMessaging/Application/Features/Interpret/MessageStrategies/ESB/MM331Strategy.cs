using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;
using Microsoft.Extensions.Logging;
using static MeteringConsumption.Contracts.MeteringConsumptionTopology;

namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;

// Sends the whole configuration, each new smart meter with its channels included, as one change for SupplyPoints. No
// meter configuration goes to Profile, which has never read an MM331, so its input does not change. A removed meter is
// the exchange's to record.
public sealed class MM331Strategy(
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<MM331Strategy> logger) : BaseMessageStrategy(publisher), IMessageStrategy
{
    public string Market => Markets.RoiEsb;

    public string MessageType => ESBMessageTypes.MM331;

    public async Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken)
    {
        var parsedMessage = ParseDocument<Message<MIM331_IntervalMeterTechnicalDetails>>(payload);
        var provenance = CreateProvenance(parsedMessage.MessageHeader!, receipt);
        var body = parsedMessage.MessageBody!;
        var meters = (body.NewMeterRegisters ?? []).Select(meter => ConfiguredMeterFor(meter)).ToList();

        List<IEvent> events =
        [
            BuildMeterConfigurationChangeEvent(receipt.CorrelationId, provenance, body.MPRN!, body.NetworksReferenceNumber,
                body.EffectiveFromDate, null,
                new MeterConfiguration
                {
                    MeterConfigurationCode = body.MeterConfigurationCode,
                    DuosGroup = body.DUOS_Group,
                    MeterPointStatusCode = body.MeterPointStatusCode,
                    UsageFactors = [],
                    Meters = meters,
                },
                time.GetUtcNow()),
        ];

        await PublishAsync(events, cancellationToken);

        logger.LogInformation("Published {Published} events from receipt {ReceiptId}", events.Count, receipt.ReceiptId);
        return new InterpretationOutcome(events.Count);
    }
}
