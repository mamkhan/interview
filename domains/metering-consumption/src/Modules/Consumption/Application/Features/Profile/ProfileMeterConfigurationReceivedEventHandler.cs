using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Profile.Abstraction;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.Application.Features.Profile;

// The session on the MPRN gives this handler one message per supply point at a time, so reading the profile,
// applying the statement and saving it cannot race with another configuration for the same supply point.
public sealed class ProfileMeterConfigurationReceivedEventHandler(
    ISupplyPointProfileRepository repository,
    TimeProvider time,
    ILogger<ProfileMeterConfigurationReceivedEventHandler> logger) : IEventHandler<MeterConfigurationReceived>
{
    public async Task HandleAsync(MeterConfigurationReceived message, CancellationToken cancellationToken)
    {
        // A message that leaves the smart meter flag empty is handled as every market message always was. The V1
        // reader states the flag with no code and no date, so a message stating the flag applies its configuration
        // only when it also states a code. Both statements are built before anything is saved.
        var configuration = message.IsNsmp is null || StatesACode(message) ? ConfigurationOf(message) : null;
        var smartMeterFlag = SmartMeterFlagOf(message);

        var profile = await repository.FindAsync(message.MPRN, cancellationToken)
            ?? SupplyPointProfile.New(message.MPRN, message.Provenance.Market);

        if (configuration is not null)
        {
            var changed = profile.Apply(configuration);
            if (changed)
            {
                await repository.SaveConfigurationAsync(profile, time.GetUtcNow(), cancellationToken);
            }

            logger.LogInformation("Meter configuration for {SupplyPointId} from {Source} {Disposition}",
                message.MPRN, message.Provenance.ContentHash, changed ? "updated" : "unchanged");
        }

        if (smartMeterFlag is not null)
        {
            var changed = profile.Apply(smartMeterFlag);
            if (changed)
            {
                await repository.SaveSmartMeterFlagAsync(profile, time.GetUtcNow(), cancellationToken);
            }

            logger.LogInformation("Smart meter flag for {SupplyPointId} {Disposition}",
                message.MPRN, changed ? "updated" : "unchanged");
        }
    }

    private static bool StatesACode(MeterConfigurationReceived message) =>
        !string.IsNullOrWhiteSpace(message.MeterConfigurationCode)
        || !string.IsNullOrWhiteSpace(message.LoadProfileCode)
        || !string.IsNullOrWhiteSpace(message.DuosGroup)
        || !string.IsNullOrWhiteSpace(message.MeterPointStatusCode);

    private static MeterConfigurationStatement ConfigurationOf(MeterConfigurationReceived message) => new()
    {
        MeterConfigurationCode = message.MeterConfigurationCode,
        LoadProfileCode = message.LoadProfileCode,
        DuosGroup = message.DuosGroup,
        MeterPointStatusCode = message.MeterPointStatusCode,
        EffectiveFrom = message.EffectiveFromDate ?? message.ReadDate
            ?? throw new ProfileStatementRejectedException($"The meter configuration for {message.MPRN} has no effective or read date."),
        Source = message.Provenance.ContentHash,
        MessageType = message.Provenance.SourceMessageType,
    };

    // The flag is ordered by the moment it was read, which the publisher stamps as the envelope's AsOf.
    private static SmartMeterFlagStatement? SmartMeterFlagOf(MeterConfigurationReceived message) =>
        message.IsNsmp is { } isNsmp
            ? SmartMeterFlagStatement.Of(
                isNsmp,
                message.AsOf ?? throw new ProfileStatementRejectedException($"The smart meter flag for {message.MPRN} has no AsOf."),
                message.Provenance.ContentHash)
            : null;
}
