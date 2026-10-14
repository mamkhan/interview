using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// A supply point with no profile yet receives the usage factors of the illustrative MM332 meter exchange the
// event contracts were agreed on: the removed meter's actual 24H factor, 3842.5173 kWh a year effective
// 2026-07-16, and the new meter's estimated 24H factor, 3910.0 effective 2026-09-21.
public class WhenApplyingUsageFactors
{
    private const string Source = "801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25";

    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenApplyingUsageFactors()
    {
        _profile.Apply(
        [
            UsageFactorStatement.Of("24H", new DateOnly(2026, 7, 16), estimated: null, actual: 3842.5173m, Source),
            UsageFactorStatement.Of("24H", new DateOnly(2026, 9, 21), estimated: 3910.0m, actual: null, Source),
        ]);
    }

    [Fact]
    public void StoresEachFactorUnderItsTimeslotAndKind()
    {
        _profile.UsageFactors[new UsageFactorSlot("24H", UsageFactorKind.Actual)].Value.Should().Be(3842.5173m);
        _profile.UsageFactors[new UsageFactorSlot("24H", UsageFactorKind.Estimated)].Value.Should().Be(3910.0m);
    }
}
