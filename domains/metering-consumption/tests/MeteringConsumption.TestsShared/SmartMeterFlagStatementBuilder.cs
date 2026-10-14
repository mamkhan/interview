using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.TestsShared;

// Defaults are a supply point V1 treats as smart, as the V1 reader reads it on its run of 2026-10-07 at 05:00
// Dublin time (04:00 UTC); the source is the content hash the reader stamps on that run's smart-meter events.
public sealed class SmartMeterFlagStatementBuilder
{
    public const string DefaultSource = "v1-reader:2026-10-07:1";

    public static readonly DateTimeOffset DefaultAsOf = new(2026, 10, 7, 4, 0, 0, TimeSpan.Zero);

    private bool _isNsmp = true;
    private DateTimeOffset _asOf = DefaultAsOf;
    private string _source = DefaultSource;

    public SmartMeterFlagStatementBuilder NotSmart()
    {
        _isNsmp = false;
        return this;
    }

    public SmartMeterFlagStatementBuilder ReadAt(DateTimeOffset asOf)
    {
        _asOf = asOf;
        return this;
    }

    public SmartMeterFlagStatementBuilder FromSource(string source)
    {
        _source = source;
        return this;
    }

    public SmartMeterFlagStatement Build() => SmartMeterFlagStatement.Of(_isNsmp, _asOf, _source);
}
