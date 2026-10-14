using BuildingBlocks.Messaging;
using BuildingBlocks.Seedwork;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;

public abstract class BaseMessageStrategy(IEventPublisher publisher)
{
    protected static T ParseDocument<T>(Stream content) where T : class =>
        ESBMessageParser.ParseDocument<T>(content);

    protected static ReadingProvenance CreateProvenance(MessageHeader header, MarketMessageReceived receipt) =>
        new(receipt.Market,
            header.MessageTypeCode!,
            header.MarketTimestamp!.Value,
            receipt.BlobPath,
            receipt.Hash,
            receipt.FileName);


    protected async Task PublishAsync(IReadOnlyList<IEvent> events, CancellationToken cancellationToken)
    {
        foreach (var @event in events)
        {
            await publisher.PublishAsync(@event, @event.GetType(), new EventMetadata
            {
                MessageId = @event.ReadingId,
                CorrelationId = @event.CorrelationId,
                SessionId = @event.MPRN,
                ApplicationProperties = new Dictionary<string, string>
                {
                    [MeteringConsumptionTopology.MessageProperties.Market] = @event.Provenance.Market,
                },
            }, cancellationToken);
        }
    }

    protected static IntervalReadingReceived BuildIntervalReadingEvent(MarketMessageReceived receipt, ReadingProvenance provenance, string mprn, string meterSerial,
                                                                       DateOnly readDate, int rrvn, int meteringIntervalMinutes, string uomCode,
                                                                       IReadOnlyList<IntervalInfo>? intervals, DateTimeOffset occurredAtUtc) =>
        new()
        {
            EventId = DeterministicGuid.From($"{receipt.Hash}:{mprn}:{readDate:yyyy-MM-dd}:{rrvn}"),
            PublishingContext = MarketMessagingTopology.PublishingContext,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = receipt.CorrelationId,
            MPRN = mprn,
            MeterSerial = meterSerial,
            ReadDate = readDate,
            RRVN = rrvn,
            MeteringIntervalMinutes = meteringIntervalMinutes,
            UOMCode = uomCode,
            Periods = CreatePeriods(intervals),
            Provenance = provenance,
        };

    protected static UsageFactorsReceived BuildUsageFactorEvent(MarketMessageReceived receipt, ReadingProvenance provenance, string mprn,
                                                               string? networksReferenceNumber, DateOnly readDate,
                                                               IReadOnlyList<UsageFactors>? usageFactors, DateTimeOffset occurredAtUtc) =>
        new()
        {
            EventId = DeterministicGuid.From($"{receipt.Hash}:{mprn}:{readDate:yyyy-MM-dd}:usage-factors"),
            PublishingContext = MarketMessagingTopology.PublishingContext,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = receipt.CorrelationId,
            MPRN = mprn,
            NetworksReferenceNumber = networksReferenceNumber,
            ReadDate = readDate,
            UsageFactors = CreateUsageFactors(usageFactors),
            Provenance = provenance,
        };

    protected static MeterConfigurationReceived BuildMeterConfigurationEvent(MarketMessageReceived receipt, ReadingProvenance provenance, string mprn,
                                                                             string? meterConfigurationCode, string? loadProfileCode, string? duosGroup,
                                                                             string? meterPointStatusCode, DateOnly? effectiveFromDate, DateOnly? readDate,
                                                                             string? networksReferenceNumber, DateTimeOffset occurredAtUtc) =>
        new()
        {
            EventId = DeterministicGuid.From($"{receipt.Hash}:{mprn}:{effectiveFromDate ?? readDate:yyyy-MM-dd}"),
            PublishingContext = MarketMessagingTopology.PublishingContext,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = receipt.CorrelationId,
            MPRN = mprn,
            MeterConfigurationCode = meterConfigurationCode,
            LoadProfileCode = loadProfileCode,
            DuosGroup = duosGroup,
            MeterPointStatusCode = meterPointStatusCode,
            EffectiveFromDate = effectiveFromDate,
            ReadDate = readDate,
            NetworksReferenceNumber = networksReferenceNumber,
            Provenance = provenance,
        };

    // SupplyPoints reads everything one message states about a supply point's configuration as one event, built from the
    // values the meter configuration and usage factors events carry, so Profile's input and SupplyPoints' can never differ.
    protected static MeterConfigurationChangeReceived MeterConfigurationChangeFor(MeterConfigurationReceived configuration,
        UsageFactorsReceived? usageFactors = null, IReadOnlyList<ConfiguredMeter>? meters = null) =>
        BuildMeterConfigurationChangeEvent(configuration.CorrelationId, configuration.Provenance, configuration.MPRN,
            configuration.NetworksReferenceNumber, configuration.EffectiveFromDate, configuration.ReadDate,
            new MeterConfiguration
            {
                MeterConfigurationCode = configuration.MeterConfigurationCode,
                LoadProfileCode = configuration.LoadProfileCode,
                DuosGroup = configuration.DuosGroup,
                MeterPointStatusCode = configuration.MeterPointStatusCode,
                UsageFactors = usageFactors?.UsageFactors ?? [],
                Meters = meters ?? [],
            },
            configuration.OccurredAtUtc);

    protected static MeterConfigurationChangeReceived BuildMeterConfigurationChangeEvent(string correlationId, ReadingProvenance provenance,
                                                                                         string mprn, string? networksReferenceNumber,
                                                                                         DateOnly? effectiveFromDate, DateOnly? readDate,
                                                                                         MeterConfiguration configuration,
                                                                                         DateTimeOffset occurredAtUtc)
    {
        var change = new MeterConfigurationChangeReceived
        {
            EventId = Guid.Empty,
            PublishingContext = MarketMessagingTopology.PublishingContext,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            MPRN = mprn,
            NetworksReferenceNumber = networksReferenceNumber,
            EffectiveFromDate = effectiveFromDate,
            ReadDate = readDate,
            Configuration = configuration,
            Provenance = provenance,
        };
        return change with { EventId = DeterministicGuid.From(change.ReadingId) };
    }

    protected static ConfiguredMeter ConfiguredMeterFor(MeterID meter) =>
        ConfiguredMeterFor(MeterChanges.InPlace, meter.SerialNumber!, meter.MeterCategoryCode, meter.MeterLocationCode, null,
            meter.RegisterLevelInfos, null);

    protected static ConfiguredMeter ConfiguredMeterFor(NewMeterRegisters meter) =>
        ConfiguredMeterFor(MeterChanges.New, meter.SerialNumber!, meter.MeterCategoryCode, meter.MeterLocationCode,
            meter.ExchangedMeterReference, meter.RegisterLevelInfos, meter.RegisterChannelInformations);

    protected static ConfiguredMeter ConfiguredMeterFor(RetainedMeterRegisters meter) =>
        ConfiguredMeterFor(MeterChanges.Retained, meter.SerialNumber!, meter.MeterCategoryCode, null, null, meter.RegisterLevelInfos, null);

    // A meter's registers come first, then a smart meter's channels, which have no sequence number, timeslot or read.
    private static ConfiguredMeter ConfiguredMeterFor(string change, string serialNumber, string? meterCategoryCode,
        string? meterLocationCode, string? exchangedMeterReference, IReadOnlyList<RegisterLevelInfo>? registers,
        IReadOnlyList<RegisterChannelInformation>? channels) =>
        new()
        {
            SerialNumber = serialNumber,
            MeterCategoryCode = meterCategoryCode,
            MeterLocationCode = meterLocationCode,
            ExchangedMeterReference = exchangedMeterReference,
            Change = change,
            Registers =
            [
                .. (registers ?? []).Select(register => new ConfiguredRegister
                {
                    MeterRegisterSequence = register.MeterRegisterSequence,
                    TimeslotCode = register.TimeslotCode,
                    RegisterTypeCode = register.RegisterTypeCode!,
                    UOMCode = register.UOM_Code!,
                    MeterMultiplier = register.MeterMultiplier,
                    PreDecimalDetails = register.PreDecimalDetails,
                    PostDecimalDetails = register.PostDecimalDetails,
                    Read = register.ReadingValue is { } reading
                        ? new RegisterRead(reading, register.ReadTypeCode, register.ReadReasonCode, register.ReadStatusCode, register.PreviousReadDate)
                        : null,
                }),
                .. (channels ?? []).Select(channel => new ConfiguredRegister
                {
                    RegisterTypeCode = channel.RegisterTypeCode!,
                    UOMCode = channel.UOM_Code!,
                    MeteringInterval = channel.MeteringInterval,
                }),
            ],
        };

    private static IReadOnlyList<IntervalPeriod> CreatePeriods(IReadOnlyList<IntervalInfo>? intervals)
    {
        if (intervals is null || intervals.Count is 0)
            return Array.Empty<IntervalPeriod>();

        var periods = new List<IntervalPeriod>();

        foreach (var interval in intervals.OrderBy(interval => interval.IntervalPeriodTimestamp))
        {
            periods.Add(new IntervalPeriod(interval.IntervalPeriodTimestamp!.Value, interval.IntervalValue!.Value, interval.IntervalStatusCode!));
        }

        return periods;
    }

    private static IReadOnlyList<UsageFactor> CreateUsageFactors(IReadOnlyList<UsageFactors>? usageFactors)
    {
        if (usageFactors is null || usageFactors.Count is 0)
            return Array.Empty<UsageFactor>();

        var factors = new List<UsageFactor>();

        foreach (var usageFactor in usageFactors)
        {
            factors.Add(new UsageFactor(usageFactor.TimeslotCode!, usageFactor.EffectiveFromDate!.Value, usageFactor.ActualUsageFactor, usageFactor.EstimatedUsageFactor));
        }

        return factors;
    }
}
