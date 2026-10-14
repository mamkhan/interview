using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.TestsShared;

// Defaults to an MM300 stating a timeslot's estimate 4127.50000000 from 2026-03-01, with no actual.
public sealed class SupplyPointUsageFactorStatementBuilder
{
    private DateOnly _startDate = new(2026, 3, 1);
    private decimal? _estimated = 4127.50000000m;
    private decimal? _actual;

    public SupplyPointUsageFactorStatementBuilder StartingOn(DateOnly startDate)
    {
        _startDate = startDate;
        return this;
    }

    public SupplyPointUsageFactorStatementBuilder WithValues(decimal? estimated, decimal? actual)
    {
        _estimated = estimated;
        _actual = actual;
        return this;
    }

    public Dated<UsageFactorValues> Build() => new(_startDate, new UsageFactorValues(_estimated, _actual));
}
