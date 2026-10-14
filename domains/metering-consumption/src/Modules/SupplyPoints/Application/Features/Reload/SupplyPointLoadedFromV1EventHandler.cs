using BuildingBlocks.Commands;
using BuildingBlocks.Messaging;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.Application.Features.Reload;

// Reloads a supply point once per event: the inbox marker, keyed on the subscription, commits with the rows. Each run of
// the V1 reader names its events afresh, so a later run reloads again.
public sealed class SupplyPointLoadedFromV1EventHandler(CommandRunner runner) : IEventHandler<SupplyPointLoadedFromV1>
{
    public async Task HandleAsync(SupplyPointLoadedFromV1 message, CancellationToken cancellationToken) =>
        await runner.RunOnceAsync<SupplyPointLoadedFromV1, ReloadOutcome>(
            message,
            new MessageKey(SupplyPointTopology.Subscriptions.SupplyPointLoadedFromV1, message.EventId.ToString()),
            cancellationToken);
}
