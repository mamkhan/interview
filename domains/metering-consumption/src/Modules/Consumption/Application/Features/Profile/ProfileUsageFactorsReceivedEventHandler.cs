using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Profile.Abstraction;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.Application.Features.Profile;

// The session on the MPRN gives this handler one message per supply point at a time, so reading the profile,
// applying the factors and saving them cannot race with other usage factors for the same supply point.
public sealed class ProfileUsageFactorsReceivedEventHandler(
    ISupplyPointProfileRepository repository,
    TimeProvider time,
    ILogger<ProfileUsageFactorsReceivedEventHandler> logger) : IEventHandler<UsageFactorsReceived>
{
    public async Task HandleAsync(UsageFactorsReceived message, CancellationToken cancellationToken)
    {
        var statements = message.UsageFactors
            .Select(factor => UsageFactorStatement.Of(factor.TimeslotCode, factor.EffectiveFromDate,
                factor.EstimatedUsageFactor, factor.ActualUsageFactor, message.Provenance.ContentHash))
            .Where(statement => statement.EffectiveFrom <= DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime))
            .ToArray();

        var profile = await repository.FindAsync(message.MPRN, cancellationToken)
            ?? SupplyPointProfile.New(message.MPRN, message.Provenance.Market);
        var changed = profile.Apply(statements);

        if (changed)
        {
            await repository.SaveUsageFactorsAsync(profile, time.GetUtcNow(), cancellationToken);
        }

        logger.LogInformation("{Count} usage factors for {SupplyPointId} from {Source} {Disposition}",
            statements.Length, message.MPRN, message.Provenance.ContentHash, changed ? "updated" : "unchanged");
    }
}
