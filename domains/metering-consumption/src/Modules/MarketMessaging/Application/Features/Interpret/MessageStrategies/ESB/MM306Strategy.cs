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

// Sends MeterConfigurationReceived, then UsageFactorsReceived when the file declares any, then the configuration and its
// usage factors as one change for SupplyPoints.
public sealed class MM306Strategy(
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<MM306Strategy> logger) : BaseMessageStrategy(publisher), IMessageStrategy
{
    public string Market => Markets.RoiEsb;

    public string MessageType => ESBMessageTypes.MM306;

    public async Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken)
    {
        var parsedMessage = ParseDocument<Message<MIM306_MeterPointStatusChangeDeEnergisationRead>>(payload);
        var provenance = CreateProvenance(parsedMessage.MessageHeader!, receipt);

        var events = new List<IEvent>();

        var meterConfigurationEvent = CreateMeterConfigurationEvent(parsedMessage, receipt, provenance);

        events.Add(meterConfigurationEvent);

        var usageFactorEvent = CreateUsageFactorEvent(parsedMessage, receipt, provenance);

        if (usageFactorEvent is not null)
        {
            events.Add(usageFactorEvent);
        }

        events.Add(MeterConfigurationChangeFor(meterConfigurationEvent, usageFactorEvent));

        await PublishAsync(events, cancellationToken);

        logger.LogInformation("Published {Published} events from receipt {ReceiptId}", events.Count, receipt.ReceiptId);
        return new InterpretationOutcome(events.Count);
    }

    // A 306 carries no meter configuration code, so the configuration leaves the code empty for the profile to keep
    // the one it has; the de-energisation is a status change, dated by its EffectiveFromDate rather than a reading.
    private MeterConfigurationReceived CreateMeterConfigurationEvent(Message<MIM306_MeterPointStatusChangeDeEnergisationRead> message,
                                                                     MarketMessageReceived receipt, ReadingProvenance provenance)
    {
        var body = message.MessageBody!;

        return BuildMeterConfigurationEvent(receipt, provenance, body.MPRN!, null, body.LoadProfileCode!, body.DUOS_Group!,
                                            body.MeterPointStatusCode!, body.EffectiveFromDate!.Value, null, body.NetworksReferenceNumber,
                                            time.GetUtcNow());
    }

    private UsageFactorsReceived? CreateUsageFactorEvent(Message<MIM306_MeterPointStatusChangeDeEnergisationRead> message,
                                                        MarketMessageReceived receipt, ReadingProvenance provenance)
    {
        var body = message.MessageBody!;

        if (body.UsageFactors is null || body.UsageFactors.Count is 0)
            return null;

        return BuildUsageFactorEvent(receipt, provenance, body.MPRN!, body.NetworksReferenceNumber, body.EffectiveFromDate!.Value, body.UsageFactors,
                                     time.GetUtcNow());
    }
}
