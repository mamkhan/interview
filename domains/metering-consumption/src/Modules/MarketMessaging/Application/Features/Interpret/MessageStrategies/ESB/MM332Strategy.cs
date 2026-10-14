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

// Sends MeterConfigurationReceived, then UsageFactorsReceived when the file declares any, then the whole configuration,
// each new and retained meter with its registers included, as one change for SupplyPoints. A removed meter is the
// exchange's to record.
public sealed class MM332Strategy(
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<MM332Strategy> logger) : BaseMessageStrategy(publisher), IMessageStrategy
{
    public string Market => Markets.RoiEsb;

    public string MessageType => ESBMessageTypes.MM332;

    public async Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken)
    {
        var parsedMessage = ParseDocument<Message<MIM332_NonIntervalTechnicalMeterDetails>>(payload);
        var provenance = CreateProvenance(parsedMessage.MessageHeader!, receipt);

        var events = new List<IEvent>();

        var meterConfigurationEvent = CreateMeterConfigurationEvent(parsedMessage, receipt, provenance);

        events.Add(meterConfigurationEvent);

        var usageFactorEvent = CreateUsageFactorEvent(parsedMessage, receipt, provenance);

        if (usageFactorEvent is not null)
        {
            events.Add(usageFactorEvent);
        }

        var body = parsedMessage.MessageBody!;
        List<ConfiguredMeter> meters =
        [
            .. (body.NewMeterRegisters ?? []).Select(meter => ConfiguredMeterFor(meter)),
            .. (body.RetainedMeterRegisters ?? []).Select(meter => ConfiguredMeterFor(meter)),
        ];
        events.Add(MeterConfigurationChangeFor(meterConfigurationEvent, usageFactorEvent, meters));

        await PublishAsync(events, cancellationToken);

        logger.LogInformation("Published {Published} events from receipt {ReceiptId}", events.Count, receipt.ReceiptId);
        return new InterpretationOutcome(events.Count);
    }

    private MeterConfigurationReceived CreateMeterConfigurationEvent(Message<MIM332_NonIntervalTechnicalMeterDetails> message, MarketMessageReceived receipt,
                                                                     ReadingProvenance provenance)
    {
        var body = message.MessageBody!;

        return BuildMeterConfigurationEvent(receipt, provenance, body.MPRN!, body.MeterConfigurationCode!, body.LoadProfileCode!, body.DUOS_Group!,
                                            body.MeterPointStatusCode!, body.EffectiveFromDate!.Value, null, body.NetworksReferenceNumber, time.GetUtcNow());
    }

    private UsageFactorsReceived? CreateUsageFactorEvent(Message<MIM332_NonIntervalTechnicalMeterDetails> message, MarketMessageReceived receipt,
                                                        ReadingProvenance provenance)
    {
        var body = message.MessageBody!;

        if (body.UsageFactors is null || body.UsageFactors.Count is 0)
            return null;

        return BuildUsageFactorEvent(receipt, provenance, body.MPRN!, body.NetworksReferenceNumber, body.EffectiveFromDate!.Value, body.UsageFactors,
                                     time.GetUtcNow());
    }
}
