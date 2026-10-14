using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Application.Features.Record.Abstraction;

public interface IConsumptionLedgerStore
{
    Task<IReadOnlyList<RecordedEntry>> LoadAsync(MeterDay meterDay, CancellationToken cancellationToken);

    // Keyed by supply point, read date and RRVN: appending the same entry again overwrites it with itself.
    Task AppendAsync(RecordedEntry recorded, CancellationToken cancellationToken);
}
