using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;

namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;

public interface IMessageStrategy
{
    string Market { get; }

    string MessageType { get; }

    Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken);
}
