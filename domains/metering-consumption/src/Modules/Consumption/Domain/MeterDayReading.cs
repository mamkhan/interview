namespace MeteringConsumption.Consumption.Domain;

// One version of a meter-day as the market sent it, in kWh per half-hour.
public sealed record MeterDayReading
{
    public required string ReadingId { get; init; }

    public required string MeterSerial { get; init; }

    public required int Rrvn { get; init; }

    public required int MeteringIntervalMinutes { get; init; }

    public required string UomCode { get; init; }

    public required IReadOnlyList<EntryPeriod> Periods { get; init; }
}
