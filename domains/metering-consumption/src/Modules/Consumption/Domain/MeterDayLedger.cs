namespace MeteringConsumption.Consumption.Domain;

// One meter-day's entries, rebuilt from the ledger, deciding what an incoming version posts. A version already
// kept is checked before one older than the version in effect: RRVN 1 arriving again after RRVN 2 is a repeat of
// a kept version, not a late one.
public sealed class MeterDayLedger
{
    private readonly MeterDay _meterDay;
    private readonly List<LedgerEntry> _entries;

    private MeterDayLedger(MeterDay meterDay, IEnumerable<LedgerEntry> entries)
    {
        _meterDay = meterDay;
        _entries = [.. entries];
    }

    public static MeterDayLedger Rehydrate(MeterDay meterDay, IEnumerable<LedgerEntry> entries) => new(meterDay, entries);

    public IReadOnlyList<LedgerEntry> Entries => _entries;

    public RecordOutcome Record(MeterDayReading incoming, DateTimeOffset postedAtUtc)
    {
        Validate(incoming);
        var periods = incoming.Periods.OrderBy(period => period.Period).ToArray();

        if (_entries.FirstOrDefault(entry => entry.Rrvn == incoming.Rrvn) is { } stored)
        {
            return stored.Reading.Periods.SequenceEqual(periods)
                ? new RecordOutcome.AlreadyRecorded(stored)
                : new RecordOutcome.ConflictingResend(stored);
        }

        var effective = _entries.MaxBy(entry => entry.Rrvn);
        if (effective is not null && incoming.Rrvn < effective.Rrvn)
        {
            return new RecordOutcome.AlreadySuperseded(effective.Rrvn);
        }

        var entry = new LedgerEntry
        {
            LedgerEntryId = incoming.ReadingId,
            MeterDay = _meterDay,
            MeterSerial = incoming.MeterSerial,
            EntryType = effective is null ? EntryType.ReadingReceived : EntryType.ReplacementReadingReceived,
            Rrvn = incoming.Rrvn,
            SupersedesRrvn = effective?.Rrvn,
            PostedAtUtc = ToTheMillisecond(postedAtUtc),
            MeteringIntervalMinutes = incoming.MeteringIntervalMinutes,
            UomCode = incoming.UomCode,
            Reversal = effective is null ? null : Cancel(effective.Reading),
            Reading = new EntryLeg(incoming.Rrvn, periods),
        };
        _entries.Add(entry);
        return new RecordOutcome.Recorded(entry,
            effective is not null && effective.MeterSerial != incoming.MeterSerial ? effective.MeterSerial : null);
    }

    // The ledger file keeps kWh to six decimals, so a value with more would be rounded on the way in, and the
    // cancellation of the version would no longer be its exact negation.
    private static void Validate(MeterDayReading incoming)
    {
        if (incoming.Rrvn < 0)
        {
            throw new LedgerEntryRejectedException($"RRVN {incoming.Rrvn} is negative.");
        }

        if (string.IsNullOrWhiteSpace(incoming.MeterSerial))
        {
            throw new LedgerEntryRejectedException("A reading names no meter serial.");
        }

        if (incoming.Periods.Count == 0)
        {
            throw new LedgerEntryRejectedException("A reading has no half-hours.");
        }

        if (incoming.Periods.GroupBy(period => period.Period).FirstOrDefault(group => group.Count() > 1) is { } repeated)
        {
            throw new LedgerEntryRejectedException($"Half-hour {repeated.Key} is stated twice.");
        }

        if (incoming.Periods.FirstOrDefault(period => decimal.Round(period.Kwh, 6) != period.Kwh) is { } precise)
        {
            throw new LedgerEntryRejectedException($"Half-hour {precise.Period} has more than six decimals: {precise.Kwh}.");
        }
    }

    // The ledger file keeps the posting time to the millisecond, so an entry announced again after it is read
    // back carries the time its first announcement did.
    private static DateTimeOffset ToTheMillisecond(DateTimeOffset time) =>
        new(time.UtcTicks - time.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    private static EntryLeg Cancel(EntryLeg version) =>
        new(version.Rrvn, [.. version.Periods.Select(period => period with { Kwh = -period.Kwh })]);
}
