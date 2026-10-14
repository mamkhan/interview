using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;

public interface IConsumptionLedgerReader
{
    // The current version of each read date in the given number of days up to the supply point's latest read date,
    // oldest first; none when the ledger holds nothing for it. No read date is later than asOf, the day the
    // triggering entry was posted, so the reader can look only at the dates the window can cover.
    Task<IReadOnlyList<LedgerDay>> ReadRecentDaysAsync(
        string supplyPointId, DateOnly asOf, int days, CancellationToken cancellationToken);
}
