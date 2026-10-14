using BuildingBlocks.Seedwork;

namespace MeteringConsumption.MarketMessaging.Domain;

// A well-formed file its interpreter refuses: deterministic, so the receipt is recorded as failed with this
// reason and the message completes.
public sealed class MarketMessageRejectedException(string message) : DomainException(message);
