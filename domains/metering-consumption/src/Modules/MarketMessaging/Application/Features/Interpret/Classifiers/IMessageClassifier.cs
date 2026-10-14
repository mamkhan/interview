namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers;

// One per market; the code is carried as the operator declares it and is never assumed unique across operators.
public interface IMessageClassifier
{
    string Classify(Stream content);
}
