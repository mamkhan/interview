using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// A reload states MCC02, 02, DG2 from 2026-08-03 with the 24H estimate 4127.5 and the 00N estimate 1500.0 from
// 2026-08-03, the 24H actual 3980.25 from 2026-09-01 and the 00N actual 1200.0 from 2026-07-01; today is 2026-10-13.
// Each key's rows are recorded from nothing, by the same rules as a message's: history keeps each statement, and today's
// row holds what is in effect.
public class WhenASupplyPointIsReloaded
{
    private readonly ReloadOutcome _outcome = SupplyPoint.Reload(
        new MeterPointStatementBuilder().StartingOn(new DateOnly(2026, 8, 3)).WithCodes("MCC02", "02", "DG2", null)
            .WithNetworksReferenceNumber(null).Build(),
        [
            ("24H", new SupplyPointUsageFactorStatementBuilder().StartingOn(new DateOnly(2026, 8, 3)).WithValues(4127.5m, null).Build()),
            ("00N", new SupplyPointUsageFactorStatementBuilder().StartingOn(new DateOnly(2026, 8, 3)).WithValues(1500.0m, null).Build()),
            ("24H", new SupplyPointUsageFactorStatementBuilder().StartingOn(new DateOnly(2026, 9, 1)).WithValues(null, 3980.25m).Build()),
            ("00N", new SupplyPointUsageFactorStatementBuilder().StartingOn(new DateOnly(2026, 7, 1)).WithValues(null, 1200.0m).Build()),
        ],
        new DateOnly(2026, 10, 13));

    [Fact]
    public void GivesTheMeterPointOneRowFromTheReloadsDate()
    {
        var loaded = new Dated<MeterPointValues>(new DateOnly(2026, 8, 3), new MeterPointValues("MCC02", "02", "DG2", null, null));

        _outcome.MeterPoint.History.Should().Equal(loaded);
        _outcome.MeterPoint.Current.Should().Equal(loaded);
    }

    [Fact]
    public void HoldsOnlyTheTimeslotsTheReloadStates() =>
        _outcome.UsageFactors.Keys.Should().BeEquivalentTo("24H", "00N");

    [Fact]
    public void CarriesTheEstimateIntoALaterActual()
    {
        _outcome.UsageFactors["24H"].History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 8, 3), new UsageFactorValues(4127.5m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 1), new UsageFactorValues(null, 3980.25m)));
        _outcome.UsageFactors["24H"].Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 1), new UsageFactorValues(4127.5m, 3980.25m)));
    }

    [Fact]
    public void CarriesAnEarlierActualIntoTheEstimate() =>
        _outcome.UsageFactors["00N"].Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 8, 3), new UsageFactorValues(1500.0m, 1200.0m)));
}
