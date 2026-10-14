using BuildingBlocks.Messaging;
using Growth.Contracts.Events;
using MeteringConsumption.Consumption.Application.Features.Profile.Abstraction;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.Application.Features.Profile;

// The session on the MPRN gives this handler one message per supply point at a time, so reading the profile,
// applying the values and saving them cannot race with another tariff statement for the same supply point.
public sealed class ProfileTariffResolvedEventHandler(
    ISupplyPointProfileRepository repository,
    TimeProvider time,
    ILogger<ProfileTariffResolvedEventHandler> logger) : IEventHandler<TariffResolved>
{
    public async Task HandleAsync(TariffResolved message, CancellationToken cancellationToken)
    {
        var asOf = message.AsOf
            ?? throw new ProfileStatementRejectedException($"The tariff values for {message.MPRN} have no AsOf.");

        try
        {
            var profile = await repository.FindAsync(message.MPRN, cancellationToken)
                ?? SupplyPointProfile.New(message.MPRN, message.Market);

            var changed = profile.Apply(TariffStatement.Of(
                message.IsTou, message.TariffCalcType, message.EffectiveLoadProfileCode, asOf, message.Source));

            if (changed)
            {
                await repository.SaveTariffAsync(profile, time.GetUtcNow(), cancellationToken);
            }

            logger.LogInformation("Tariff values for {SupplyPointId} {Disposition}", message.MPRN, changed ? "updated" : "unchanged");
        }
        catch (Exception exception) when (exception is not ProfileStatementRejectedException)
        {
            logger.LogError(exception, "Saving the tariff values for {SupplyPointId} failed", message.MPRN);
        }
    }
}
