using BuildingBlocks.Seedwork;

namespace MeteringConsumption.MarketMessaging.Domain;

// The file declares no message type the market's classifier can read: deterministic, recorded as failed.
public sealed class MessageClassificationException(string message) : DomainException(message);
