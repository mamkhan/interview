using AwesomeAssertions;
using BuildingBlocks.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// An XPRN is the supply point's reference: at most 11 characters (the ESB XSD 14.00.00 MPRN), never empty.
public class WhenAnXPRNIsCreated
{
    [Fact]
    public void KeepsAnElevenCharacterReference()
    {
        new XPRN("10000000001").Value.Should().Be("10000000001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("100000000012")]
    public void RefusesAReferenceThatCannotBeASupplyPoint(string value)
    {
        var create = () => new XPRN(value);

        create.Should().Throw<DomainException>();
    }
}
