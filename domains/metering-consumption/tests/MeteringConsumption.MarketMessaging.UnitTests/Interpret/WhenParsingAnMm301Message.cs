using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The builder's MM301 bound onto the schema the way the parser binds the real files: the meter point's
// characteristics and its premises address. Frozen literals from MarketMessageXmlBuilder: MPRN 10900000343, DLF LV,
// DSO_GRID, load profile 01, import 12 from 2026-06-24, export 5, MCC12, DUOS group DG1, effective 2026-06-24,
// essential plant 0, address 1 MAIN STREET, TESTTOWN, KE, IE.
public class WhenParsingAnMm301Message
{
    private const string Mprn = "10900000343";

    private readonly Message<MIM301_MeterPointCharacteristics> _parsed;

    public WhenParsingAnMm301Message()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("301").WithMprn(Mprn).BuildStream();
        _parsed = ESBMessageParser.ParseDocument<Message<MIM301_MeterPointCharacteristics>>(payload);
    }

    private MIM301_MeterPointCharacteristics Body => _parsed.MessageBody!;

    [Fact]
    public void ReadsTheMeterPointsConfiguration()
    {
        Body.MPRN.Should().Be(Mprn);
        Body.MeterConfigurationCode.Should().Be("MCC12");
        Body.LoadProfileCode.Should().Be("01");
        Body.DUOS_Group.Should().Be("DG1");
        Body.EffectiveFromDate.Should().Be(new DateOnly(2026, 6, 24));
        Body.EssentialPlantFlag.Should().BeFalse();
    }

    [Fact]
    public void ReadsTheConnectionCharacteristics()
    {
        Body.DLF_Code.Should().Be("LV");
        Body.ConnectionSystemCode.Should().Be("DSO_GRID");
        Body.MaximumImportCapacity.Should().Be(12m);
        Body.MICStartDate.Should().Be(new DateOnly(2026, 6, 24));
        Body.MaximumExportCapacity.Should().Be(5m);
    }

    [Fact]
    public void LeavesTheCharacteristicsTheFileDoesNotStateEmpty()
    {
        Body.TransformerLossFactor.Should().BeNull();
        Body.LoadFactorCode.Should().BeNull();
        Body.CurrentChargeableServiceCapacity.Should().BeNull();
        Body.CSCStartDate.Should().BeNull();
        Body.CSC5YrEndDate.Should().BeNull();
    }

    [Fact]
    public void ReadsThePremisesAddress()
    {
        var address = Body.MeterPointAddress!;

        address.HouseNo.Should().Be("1");
        address.Street.Should().Be("MAIN STREET");
        address.City.Should().Be("TESTTOWN");
        address.CountyIreland.Should().Be("KE");
        address.Country.Should().Be("IE");
        address.UnitNo.Should().BeNull();
    }
}
