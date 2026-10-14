using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.TestsShared;

// Entries as the real ledger posts them, for the persona of MeterDayReadingBuilder: each reading given is
// recorded in turn on one meter-day, and every recorded entry keeps the provenance of the file below.
public sealed class RecordedEntryBuilder(MeterDay? meterDay = null)
{
    public static readonly DateTimeOffset PostedAt = new(2026, 6, 21, 2, 14, 0, 417, TimeSpan.Zero);

    public static ReadingProvenance Provenance(string contentHash = MeterDayReadingBuilder.ContentHash) => new(
        "roi-esb",
        "343",
        new DateTimeOffset(2026, 6, 21, 1, 50, 10, 918, TimeSpan.Zero),
        $"roi-esb/{contentHash}.xml",
        contentHash,
        "DSO_MIM_343_1782000000000_3800001.xml");

    private readonly List<MeterDayReading> _readings = [];

    public RecordedEntryBuilder Recording(MeterDayReading reading)
    {
        _readings.Add(reading);
        return this;
    }

    public IReadOnlyList<RecordedEntry> Build()
    {
        var ledger = MeterDayLedger.Rehydrate(meterDay ?? MeterDayReadingBuilder.MeterDay, []);
        var recorded = new List<RecordedEntry>();
        foreach (var (reading, index) in _readings.Select((reading, index) => (reading, index)))
        {
            if (ledger.Record(reading, PostedAt.AddDays(index)) is RecordOutcome.Recorded { Entry: var entry })
            {
                recorded.Add(new RecordedEntry(entry, Provenance()));
            }
        }

        return recorded;
    }
}
