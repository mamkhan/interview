namespace BuildingBlocks.Messaging;

/// <summary>The scoped clock a delivery runs on: the message's AsOf when the envelope carries one, the
/// system clock otherwise. Handlers inject <see cref="TimeProvider"/> and never see this type.</summary>
public sealed class MessageClock : TimeProvider
{
    public DateTimeOffset? AsOf { get; set; }

    public override DateTimeOffset GetUtcNow() => AsOf ?? System.GetUtcNow();
}
