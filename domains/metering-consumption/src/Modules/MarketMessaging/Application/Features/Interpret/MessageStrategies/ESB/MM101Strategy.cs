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

// Sends MeterConfigurationReceived, then the same configuration as one change for SupplyPoints, when the file states the
// date it takes effect.
public sealed class MM101Strategy(
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<MM101Strategy> logger) : BaseMessageStrategy(publisher), IMessageStrategy
{
    public string Market => Markets.RoiEsb;

    public string MessageType => ESBMessageTypes.MM101;

    public async Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken)
    {
        var parsedMessage = ParseDocument<Message<MIM101_NewConnectionRegistrationAcceptance>>(payload);
        var provenance = CreateProvenance(parsedMessage.MessageHeader!, receipt);

        var events = new List<IEvent>();

        var meterConfigurationEvent = CreateMeterConfigurationEvent(parsedMessage, receipt, provenance);

        if (meterConfigurationEvent is not null)
        {
            events.Add(meterConfigurationEvent);
            events.Add(MeterConfigurationChangeFor(meterConfigurationEvent));
        }

        await PublishAsync(events, cancellationToken);

        logger.LogInformation("Published {Published} events from receipt {ReceiptId}", events.Count, receipt.ReceiptId);
        return new InterpretationOutcome(events.Count);
    }

    // EffectiveFromDate is optional on a 101, and RegistrationReceiptDate is when ESB took the registration, not when
    // the configuration applies, so a 101 without one sends no configuration rather than one with an invented date.
    // The customer's name, contacts, addresses and special needs stay in the parsed body.
    private MeterConfigurationReceived? CreateMeterConfigurationEvent(Message<MIM101_NewConnectionRegistrationAcceptance> message,
                                                                      MarketMessageReceived receipt, ReadingProvenance provenance)
    {
        var body = message.MessageBody!;

        if (body.EffectiveFromDate is null)
            return null;

        return BuildMeterConfigurationEvent(receipt, provenance, body.MPRN!, body.MeterConfigurationCode, body.LoadProfileCode, body.DUOS_Group,
                                            body.MeterPointStatusCode, body.EffectiveFromDate.Value, null, null, time.GetUtcNow());
    }
}
