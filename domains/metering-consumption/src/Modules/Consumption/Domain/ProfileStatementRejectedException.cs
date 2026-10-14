using BuildingBlocks.Seedwork;

namespace MeteringConsumption.Consumption.Domain;

// A statement that breaks its contract: deterministic, so the message is dead-lettered on first delivery for
// someone to look at, and nothing of it reaches the profile.
public sealed class ProfileStatementRejectedException(string message) : DomainException(message);
