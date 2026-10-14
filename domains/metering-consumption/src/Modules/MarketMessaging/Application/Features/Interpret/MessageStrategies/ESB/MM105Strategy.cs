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

// Sends MeterConfigurationReceived, then the same configuration as one change for SupplyPoints.
public sealed class MM105Strategy(
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<MM105Strategy> logger) : BaseMessageStrategy(publisher), IMessageStrategy
{
    public string Market => Markets.RoiEsb;

    public string MessageType => ESBMessageTypes.MM105;

    public async Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken)
    {
        var parsedMessage = ParseDocument<Message<MIM105_ChangeofSupplierConfirmation>>(payload);
        var provenance = CreateProvenance(parsedMessage.MessageHeader!, receipt);

        var events = new List<IEvent>();

        var meterConfigurationEvent = CreateMeterConfigurationEvent(parsedMessage, receipt, provenance);

        events.Add(meterConfigurationEvent);
        events.Add(MeterConfigurationChangeFor(meterConfigurationEvent));

        await PublishAsync(events, cancellationToken);

        logger.LogInformation("Published {Published} events from receipt {ReceiptId}", events.Count, receipt.ReceiptId);
        return new InterpretationOutcome(events.Count);
    }

    // A 105 carries no load profile, so the profile keeps the one it has; the change of supplier is dated by its
    // EffectiveFromDate. The customer's name, contacts, addresses and special needs stay in the parsed body.
    private MeterConfigurationReceived CreateMeterConfigurationEvent(Message<MIM105_ChangeofSupplierConfirmation> message,
                                                                     MarketMessageReceived receipt, ReadingProvenance provenance)
    {
        var body = message.MessageBody!;

        return BuildMeterConfigurationEvent(receipt, provenance, body.MPRN!, body.MeterConfigurationCode, null, body.DUOS_Group,
                                            body.MeterPointStatusCode, body.EffectiveFromDate!.Value, null, null, time.GetUtcNow());
    }
}
