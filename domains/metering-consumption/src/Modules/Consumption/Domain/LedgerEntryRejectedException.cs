using BuildingBlocks.Seedwork;

namespace MeteringConsumption.Consumption.Domain;

// A reading that breaks its contract: deterministic, so the message is dead-lettered on first delivery for
// someone to look at, and nothing of it reaches the ledger.
public sealed class LedgerEntryRejectedException(string message) : DomainException(message);
