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

// Sends one IntervalReadingReceived per MPRN, meter and channel in the file.
public sealed class MM343Strategy(
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<MM343Strategy> logger) : BaseMessageStrategy(publisher), IMessageStrategy
{
    public string Market => Markets.RoiEsb;

    public string MessageType => ESBMessageTypes.MM343;

    public async Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken)
    {
        var parsedMessage = ParseDocument<Message<MIM343_SMARTMeteringIntervalData>>(payload);
        var provenance = CreateProvenance(parsedMessage.MessageHeader!, receipt);

        var events = new List<IEvent>();

        var intervalReadingEvents = CreateIntervalReadingEvents(parsedMessage, receipt, provenance);

        events.AddRange(intervalReadingEvents);

        await PublishAsync(events, cancellationToken);

        logger.LogInformation("Published {Published} events from receipt {ReceiptId}", events.Count, receipt.ReceiptId);
        return new InterpretationOutcome(events.Count);
    }

    private List<IntervalReadingReceived> CreateIntervalReadingEvents(Message<MIM343_SMARTMeteringIntervalData> message, MarketMessageReceived receipt, ReadingProvenance provenance)
    {
        var readings = new List<IntervalReadingReceived>();

        foreach (var mprnLevelInfo in message.MessageBody?.MPRNLevelInfos ?? [])
        {
            var mprn = mprnLevelInfo.MPRN!;
            var readDate = mprnLevelInfo.ReadDate!.Value;
            var rrvn = mprnLevelInfo.ReadingReplacementVersionNumber!.Value;

            foreach (var meter in mprnLevelInfo.MeterIDs ?? [])
            {
                var meterSerial = meter.SerialNumber!;

                foreach (var channel in meter.ChannelInfos ?? [])
                {
                    var meteringIntervalMinutes = channel.MeteringInterval!.Value;
                    var uomCode = channel.UOM_Code!;

                    readings.Add(BuildIntervalReadingEvent(receipt, provenance, mprn, meterSerial, readDate, rrvn, meteringIntervalMinutes, uomCode,
                                                           channel.IntervalInfos, time.GetUtcNow()));
                }
            }
        }

        return readings;
    }
}
