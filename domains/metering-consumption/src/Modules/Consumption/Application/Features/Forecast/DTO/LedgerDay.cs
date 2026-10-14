using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.DTO;

// One read date's current version, with the ledger entry that holds it.
public sealed record LedgerDay(ReadDay Day, string LedgerEntryId, int Rrvn);
