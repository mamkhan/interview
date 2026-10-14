using BuildingBlocks.Commands;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.SupplyPoints.Application.Features.Store.Abstraction;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Exceptions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using DomainRegisterRead = MeteringConsumption.SupplyPoints.Domain.ValueObjects.RegisterRead;

namespace MeteringConsumption.SupplyPoints.Application.Features.Store.Commands;

// Records the meter point, then each usage factor in the order the message lists them, then each meter, then each
// register, into one supply point in one unit of work, so a message is stored whole or not at all.
public sealed class MeterConfigurationChangeCommandHandler(ISupplyPointRepository repository, TimeProvider clock)
    : ICommandHandler<MeterConfigurationChangeReceived, IReadOnlyList<RecordOutcomeKind>>
{
    public async Task<IReadOnlyList<RecordOutcomeKind>> HandleAsync(MeterConfigurationChangeReceived command, CancellationToken cancellationToken)
    {
        // The meter point and the meters take effect on the date the message states, or on its read date when it states
        // none. Without either there is nowhere to put them, so none of the message is stored.
        var date = command.EffectiveFromDate ?? command.ReadDate
            ?? throw new SupplyPointRejectedException($"The meter configuration for {command.MPRN} has no effective or read date.");
        var configuration = command.Configuration;
        var xprn = new XPRN(command.MPRN);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        if (await repository.FindAsync(xprn, cancellationToken) is null)
        {
            await repository.AddAsync(SupplyPoint.New(xprn, command.Provenance.Market), cancellationToken);
        }

        var outcomes = new List<RecordOutcomeKind>
        {
            await RecordAsync(new MeterPointKey(xprn), new Dated<MeterPointValues>(date, new MeterPointValues(
                Stated(configuration.MeterConfigurationCode),
                Stated(configuration.LoadProfileCode),
                Stated(configuration.DuosGroup),
                Stated(configuration.MeterPointStatusCode),
                Stated(command.NetworksReferenceNumber))), today, cancellationToken),
        };

        foreach (var factor in configuration.UsageFactors)
        {
            outcomes.Add(await RecordAsync(new UsageFactorKey(xprn, factor.TimeslotCode), new Dated<UsageFactorValues>(
                factor.EffectiveFromDate, new UsageFactorValues(factor.EstimatedUsageFactor, factor.ActualUsageFactor)), today, cancellationToken));
        }

        foreach (var meter in configuration.Meters)
        {
            outcomes.Add(await RecordAsync(new MeterKey(xprn, meter.SerialNumber), new Dated<MeterDetails>(
                date, new MeterDetails(meter.MeterCategoryCode, meter.MeterLocationCode, meter.ExchangedMeterReference)), today, cancellationToken));
        }

        foreach (var meter in configuration.Meters)
        {
            foreach (var register in meter.Registers)
            {
                outcomes.Add(await RecordAsync(new RegisterKey(xprn, RegisterIdOf(meter, register)),
                    new Dated<RegisterValues>(date, RegisterValuesOf(register)), today, cancellationToken));
            }
        }

        return outcomes;
    }

    private async Task<RecordOutcomeKind> RecordAsync<TValues>(ConfigurationKey<TValues> key, Dated<TValues> statement, DateOnly today,
        CancellationToken cancellationToken)
        where TValues : class, ICarriedValues<TValues>
    {
        var recorded = Recording.Decide(statement, today, await repository.FactsAsync(key, statement.EffectiveDate, cancellationToken));
        await repository.SaveAsync(key, recorded, cancellationToken);
        return recorded.Kind;
    }

    // ESB leaves a code it does not state blank as often as it leaves it out.
    private static string? Stated(string? code) => string.IsNullOrWhiteSpace(code) ? null : code;

    // A smart meter's channel has no sequence number; its register type and unit are what tell it from the meter's other
    // channels.
    private static RegisterId RegisterIdOf(ConfiguredMeter meter, ConfiguredRegister register) =>
        new(meter.SerialNumber, register.MeterRegisterSequence ?? $"{register.RegisterTypeCode}:{register.UOMCode}");

    private static RegisterValues RegisterValuesOf(ConfiguredRegister register) => new(
        new RegisterDetails
        {
            TimeslotCode = register.TimeslotCode,
            RegisterTypeCode = register.RegisterTypeCode,
            UomCode = register.UOMCode,
            MeterMultiplier = register.MeterMultiplier,
            MeteringInterval = register.MeteringInterval,
            PreDecimalDetails = register.PreDecimalDetails,
            PostDecimalDetails = register.PostDecimalDetails,
        },
        register.Read is { } read
            ? new DomainRegisterRead(read.ReadingValue, read.ReadTypeCode, read.ReadReasonCode, read.ReadStatusCode, read.PreviousReadDate)
            : null);
}
