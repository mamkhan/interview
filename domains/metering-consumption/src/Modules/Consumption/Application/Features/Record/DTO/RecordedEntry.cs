using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Consumption.Application.Features.Record.DTO;

// An entry as the ledger keeps it: the domain's entry beside the file its reading came from, which the domain
// carries none of and the row needs to describe itself.
public sealed record RecordedEntry(LedgerEntry Entry, ReadingProvenance Provenance);
