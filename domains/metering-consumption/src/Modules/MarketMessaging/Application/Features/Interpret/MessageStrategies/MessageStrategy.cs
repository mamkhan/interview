namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;

public sealed class MessageStrategy
{
    private readonly Dictionary<(string Market, string MessageType), IMessageStrategy> _interpreters = [];

    public MessageStrategy(IEnumerable<IMessageStrategy> interpreters)
    {
        foreach (var interpreter in interpreters)
        {
            if (!_interpreters.TryAdd((interpreter.Market, interpreter.MessageType), interpreter))
            {
                throw new InvalidOperationException(
                    $"Two interpreters claim {interpreter.Market}/{interpreter.MessageType}: {_interpreters[(interpreter.Market, interpreter.MessageType)].GetType().Name} and {interpreter.GetType().Name}.");
            }
        }
    }

    public IMessageStrategy? Find(string market, string messageType) =>
        _interpreters.TryGetValue((market, messageType), out var interpreter) ? interpreter : null;
}
