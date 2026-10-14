using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;
using static MeteringConsumption.Contracts.MeteringConsumptionTopology;

namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;

// Sends MeterConfigurationReceived, then UsageFactorsReceived, then the whole configuration, each meter in place with its
// registers included, as one change for SupplyPoints.
public sealed class MM320Strategy(
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<MM320Strategy> logger) : BaseMessageStrategy(publisher), IMessageStrategy
{
    public string Market => Markets.RoiEsb;

    public string MessageType => ESBMessageTypes.MM320;

    public async Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken)
    {
        var parsedMessage = ParseDocument<Message<MIM320_ValidatedCoSReading>>(payload);
        var provenance = CreateProvenance(parsedMessage.MessageHeader!, receipt);

        var events = new List<IEvent>();

        var meterConfigurationEvent = CreateMeterConfigurationEvent(parsedMessage, receipt, provenance);

        events.Add(meterConfigurationEvent);

        var usageFactorEvent = CreateUsageFactorEvent(parsedMessage, receipt, provenance);

        events.Add(usageFactorEvent);

        var meters = (parsedMessage.MessageBody!.MeterIDs ?? []).Select(meter => ConfiguredMeterFor(meter)).ToList();
        events.Add(MeterConfigurationChangeFor(meterConfigurationEvent, usageFactorEvent, meters));

        await PublishAsync(events, cancellationToken);

        logger.LogInformation("Published {Published} events from receipt {ReceiptId}", events.Count, receipt.ReceiptId);
        return new InterpretationOutcome(events.Count);
    }

    // A 320 has no effective date of its own, so the configuration carries the change-of-supplier reading's ReadDate
    // instead and leaves EffectiveFromDate empty.
    private MeterConfigurationReceived CreateMeterConfigurationEvent(Message<MIM320_ValidatedCoSReading> message, MarketMessageReceived receipt,
                                                                     ReadingProvenance provenance)
    {
        var body = message.MessageBody!;

        return BuildMeterConfigurationEvent(receipt, provenance, body.MPRN!, body.MeterConfigurationCode!, body.LoadProfileCode!, body.DUOS_Group!,
                                            body.MeterPointStatusCode!, null, body.ReadDate!.Value, body.NetworksReferenceNumber, time.GetUtcNow());
    }

    private UsageFactorsReceived CreateUsageFactorEvent(Message<MIM320_ValidatedCoSReading> message, MarketMessageReceived receipt, ReadingProvenance provenance)
    {
        var body = message.MessageBody!;

        return BuildUsageFactorEvent(receipt, provenance, body.MPRN!, body.NetworksReferenceNumber!, body.ReadDate!.Value, body.UsageFactors,
                                     time.GetUtcNow());
    }
}
