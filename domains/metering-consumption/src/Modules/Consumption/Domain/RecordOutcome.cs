namespace MeteringConsumption.Consumption.Domain;

public abstract record RecordOutcome
{
    private RecordOutcome()
    {
    }

    // ReplacedMeterSerial is set when the version replaced came from another meter: an exchange, which changes
    // no decision but is worth a log line.
    public sealed record Recorded(LedgerEntry Entry, string? ReplacedMeterSerial = null) : RecordOutcome;

    // The version is already kept with these values: nothing is posted, and the entry kept is announced again.
    public sealed record AlreadyRecorded(LedgerEntry Stored) : RecordOutcome;

    // A newer version is already in effect: this one never becomes effective, so it posts nothing.
    public sealed record AlreadySuperseded(int EffectiveRrvn) : RecordOutcome;

    // The version is already kept with other values: the first received stays, and the second is flagged.
    public sealed record ConflictingResend(LedgerEntry Stored) : RecordOutcome;
}
