namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers;

// One classifier per market: the operator's own header is the only thing that names a file's type.
public sealed class MessageClassifiers : Dictionary<string, IMessageClassifier>
{
    public IMessageClassifier? For(string market) => TryGetValue(market, out var classifier) ? classifier : null;
}
