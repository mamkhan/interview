using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// Register 001 of meter 90000320 is held from 2026-09-30 with read 0 (the real MM320 sample); today is 2026-11-25. An
// MM332 keeps the meter on 2026-11-20 and states the register again with the same details and read 12345 (type A,
// reason 04, status V, previous read 2026-09-30). A read is a value like the details, so a new one is a change.
public class WhenARegisterIsStatedAgainWithANewRead
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    private static readonly RegisterDetails Details = new()
    {
        TimeslotCode = "01D",
        RegisterTypeCode = "78",
        UomCode = "KWH",
        MeterMultiplier = 1m,
        PreDecimalDetails = "07",
        PostDecimalDetails = "0",
    };

    private readonly KeyRows<RegisterValues> _register = new();

    private readonly Recorded<RegisterValues> _recorded;

    public WhenARegisterIsStatedAgainWithANewRead()
    {
        _register.Record(new RegisterStatementBuilder().Build(), Today);
        _recorded = _register.Record(new RegisterStatementBuilder()
            .StartingOn(new DateOnly(2026, 11, 20))
            .WithRead(new RegisterRead(12345m, "A", "04", "V", new DateOnly(2026, 9, 30)))
            .Build(), Today);
    }

    [Fact]
    public void KeepsTheNewReadAsHistoryOfItsOwnDate()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Added);
        _register.History.Select(row => (row.EffectiveDate, row.Values.Read)).Should().Equal(
            (new DateOnly(2026, 9, 30), new RegisterRead(0m, "A", "26", null, null)),
            (new DateOnly(2026, 11, 20), new RegisterRead(12345m, "A", "04", "V", new DateOnly(2026, 9, 30))));
    }

    [Fact]
    public void MakesTheNewReadPartOfTodaysRow() =>
        _register.Current.Should().Equal(new Dated<RegisterValues>(new DateOnly(2026, 11, 20),
            new RegisterValues(Details, new RegisterRead(12345m, "A", "04", "V", new DateOnly(2026, 9, 30)))));
}
