namespace BuildingBlocks.Messaging;

/// <summary>Which topic carries each event type. Built once at composition from the contracts
/// library's topology constants, so publishers cannot invent entity names.</summary>
public sealed class EventTopicMap
{
    private readonly Dictionary<Type, string> _byType = [];
    private readonly Dictionary<string, string> _byName = [];

    public EventTopicMap Register<TEvent>(string topic) where TEvent : class
    {
        _byType[typeof(TEvent)] = topic;
        _byName[typeof(TEvent).FullName!] = topic;
        return this;
    }

    public string TopicFor(Type eventType) =>
        _byType.TryGetValue(eventType, out var topic)
            ? topic
            : throw new InvalidOperationException($"No topic registered for event type {eventType.FullName}.");

    public string TopicFor(string eventTypeFullName) =>
        _byName.TryGetValue(eventTypeFullName, out var topic)
            ? topic
            : throw new InvalidOperationException($"No topic registered for event type {eventTypeFullName}.");
}
