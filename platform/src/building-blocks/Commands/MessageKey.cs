namespace BuildingBlocks.Commands;

/// <summary>What the inbox records for a message-triggered command: which consumer handled which
/// message. The consumer name is the handler type, so two slices consuming the same event keep separate
/// markers and one being redelivered never skips the other.</summary>
public readonly record struct MessageKey(string Consumer, string MessageId)
{
    public static MessageKey For<THandler>(string messageId) => new(typeof(THandler).Name, messageId);
}
