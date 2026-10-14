using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.TestsShared;

// Defaults are the new meter's estimated 24H factor from the illustrative MM332 meter exchange the event
// contracts were agreed on: 3910.0 kWh a year, effective 2026-09-21.
public sealed class UsageFactorStatementBuilder
{
    private string _timeslot = "24H";
    private DateOnly _effectiveFrom = new(2026, 9, 21);
    private decimal? _estimated = 3910.0m;
    private decimal? _actual;
    private string _source = MeterConfigurationStatementBuilder.DefaultSource;

    public UsageFactorStatementBuilder ForTimeslot(string timeslot)
    {
        _timeslot = timeslot;
        return this;
    }

    public UsageFactorStatementBuilder EffectiveFrom(DateOnly date)
    {
        _effectiveFrom = date;
        return this;
    }

    public UsageFactorStatementBuilder Estimated(decimal value)
    {
        _estimated = value;
        _actual = null;
        return this;
    }

    public UsageFactorStatementBuilder Actual(decimal value)
    {
        _actual = value;
        _estimated = null;
        return this;
    }

    public UsageFactorStatementBuilder FromSource(string contentHash)
    {
        _source = contentHash;
        return this;
    }

    public UsageFactorStatement Build() =>
        UsageFactorStatement.Of(_timeslot, _effectiveFrom, _estimated, _actual, _source);
}
