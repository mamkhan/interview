using BuildingBlocks.Commands;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Application.Features.Store;

// Records everything one message states about a supply point's configuration, once per message: the inbox marker, keyed
// on the subscription, commits with the rows.
public sealed class MeterConfigurationChangeReceivedEventHandler(CommandRunner runner) : IEventHandler<MeterConfigurationChangeReceived>
{
    public async Task HandleAsync(MeterConfigurationChangeReceived message, CancellationToken cancellationToken) =>
        await runner.RunOnceAsync<MeterConfigurationChangeReceived, IReadOnlyList<RecordOutcomeKind>>(
            message,
            new MessageKey(SupplyPointTopology.Subscriptions.MeterConfigurationChangeReceived, message.EventId.ToString()),
            cancellationToken);
}
