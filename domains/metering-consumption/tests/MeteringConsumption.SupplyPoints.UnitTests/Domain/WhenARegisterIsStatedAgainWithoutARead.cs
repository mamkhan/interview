using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// Register 001 of meter 90000320 is held from 2026-09-30 with read 0 (the real MM320 sample); today is 2026-11-25. A
// message on 2026-11-20 states the register with the same details and no read: the read is not stated, so the one in
// effect stays and nothing changed.
public class WhenARegisterIsStatedAgainWithoutARead
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    private readonly KeyRows<RegisterValues> _register = new();

    private readonly Recorded<RegisterValues> _recorded;

    public WhenARegisterIsStatedAgainWithoutARead()
    {
        _register.Record(new RegisterStatementBuilder().Build(), Today);
        _recorded = _register.Record(new RegisterStatementBuilder().StartingOn(new DateOnly(2026, 11, 20)).WithRead(null).Build(), Today);
    }

    [Fact]
    public void WritesNothing()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Unchanged);
        _register.Current.Should().ContainSingle().Which.Values.Read.Should().Be(new RegisterRead(0m, "A", "26", null, null));
    }
}
