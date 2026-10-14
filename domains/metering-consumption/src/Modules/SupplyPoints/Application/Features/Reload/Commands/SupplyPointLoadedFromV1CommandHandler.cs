using BuildingBlocks.Commands;
using MeteringConsumption.SupplyPoints.Application.Features.Reload.Abstraction;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.Application.Features.Reload.Commands;

// V1's values replace what the supply point holds for its meter point and usage factors, in one unit of work. V1 states
// no meter point status and no networks reference number, so the reloaded rows have neither.
public sealed class SupplyPointLoadedFromV1CommandHandler(ISupplyPointReloadRepository repository, TimeProvider clock)
    : ICommandHandler<SupplyPointLoadedFromV1, ReloadOutcome>
{
    public async Task<ReloadOutcome> HandleAsync(SupplyPointLoadedFromV1 command, CancellationToken cancellationToken)
    {
        var xprn = new XPRN(command.MPRN);
        if (await repository.FindAsync(xprn, cancellationToken) is null)
        {
            await repository.AddAsync(SupplyPoint.New(xprn, command.Market), cancellationToken);
        }

        var outcome = SupplyPoint.Reload(
            new Dated<MeterPointValues>(command.CodesFrom, new MeterPointValues(
                Stated(command.MeterConfigurationCode), Stated(command.LoadProfileCode), Stated(command.DuosGroup), null, null)),
            command.UsageFactors.Select(factor => (factor.TimeslotCode, new Dated<UsageFactorValues>(factor.EffectiveFromDate,
                new UsageFactorValues(factor.EstimatedUsageFactor, factor.ActualUsageFactor)))),
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        await repository.SaveReloadAsync(xprn, outcome, cancellationToken);

        return outcome;
    }

    private static string? Stated(string? code) => string.IsNullOrWhiteSpace(code) ? null : code;
}
