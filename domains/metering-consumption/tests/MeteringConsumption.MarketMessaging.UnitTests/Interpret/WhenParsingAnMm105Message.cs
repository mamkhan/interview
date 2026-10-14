using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The builder's MM105 bound onto the schema the way the parser binds the real files: the change of supplier, its
// personal-data elements, all made up, and the smart data services under the name ESB sends. Frozen literals from
// MarketMessageXmlBuilder: MPRN 10900000343, status E, MCC12, DUOS group DG2, effective 2026-06-24, old supplier S10,
// estimated annual consumption 2453, customer Mr Test Tester.
public class WhenParsingAnMm105Message
{
    private const string Mprn = "10900000343";

    private readonly Message<MIM105_ChangeofSupplierConfirmation> _parsed;

    public WhenParsingAnMm105Message()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("105").WithMprn(Mprn).BuildStream();
        _parsed = ESBMessageParser.ParseDocument<Message<MIM105_ChangeofSupplierConfirmation>>(payload);
    }

    private MIM105_ChangeofSupplierConfirmation Body => _parsed.MessageBody!;

    [Fact]
    public void ReadsTheMeterPointsConfiguration()
    {
        Body.MPRN.Should().Be(Mprn);
        Body.MeterPointStatusCode.Should().Be("E");
        Body.MeterConfigurationCode.Should().Be("MCC12");
        Body.DUOS_Group.Should().Be("DG2");
        Body.EffectiveFromDate.Should().Be(new DateOnly(2026, 6, 24));
    }

    [Fact]
    public void ReadsTheChangeOfSuppliersMarketDetails()
    {
        Body.SupplierMPID.Should().Be("S22");
        Body.SupplierMPIDOld.Should().Be("S10");
        Body.SupplierUnitID.Should().Be("SU_400400");
        Body.SettlementClassCode.Should().Be("P");
        Body.MaximumImportCapacity.Should().Be(12m);
        Body.MICStartDate.Should().Be(new DateOnly(2026, 1, 1));
        Body.CurrentChargeableServiceCapacity.Should().Be(9m);
        Body.DUoSBillingFrequencyCode.Should().Be("B");
        Body.DuosBillingCycle.Should().Be("38");
        Body.CommsTechnicallyFeasible.Should().Be("04");
    }

    [Fact]
    public void ReadsTheSmartDataServicesUnderTheNameEsbSends()
    {
        Body.MIM105_SmartDataServices!.SmartDataServicesCode.Should().Be("01");
        Body.MIM105_SmartDataServices.RegistrationMeterConfigurationCode.Should().Be("MCC12");
        Body.MIM105_SmartDataServices.EstimatedAnnualConsumption.Should().Be(2453m);
    }

    [Fact]
    public void ReadsTheCustomersPersonalDataIntoTheBody()
    {
        Body.CustomerName!.LastName.Should().Be("Tester");
        Body.CustomerContactDetails!.PhoneOne!.Number.Should().Be("0850000000");
        Body.NotificationAddress!.Address!.Street.Should().Be("CHURCH ROAD");
        Body.TechnicalContact!.ContactName.Should().Be("Test Engineer");
        Body.CustomerServiceSpecialNeeds!.Select(n => n.CustomerServiceDetailsCode).Should().Equal("SN01");
        Body.MedicalEquipmentDetailsCode.Should().Be("M1");
        Body.MeterPointAddress!.Street.Should().Be("MAIN STREET");
    }
}

// The real file names the smart data services MIM105_SmartDataServices, not the guide's SmartDataServices_MIM105; read
// from the anonymised sample so a rename against the guide fails here.
public class WhenParsingARealMm105File
{
    [Fact]
    public void ReadsTheSmartDataServicesUnderTheNameEsbSends()
    {
        using var payload = File.OpenRead("samples/ESB_MM105_10900000105_0000000790000105.xml");

        var smartDataServices = ESBMessageParser.ParseDocument<Message<MIM105_ChangeofSupplierConfirmation>>(payload).MessageBody!.MIM105_SmartDataServices!;

        smartDataServices.SmartDataServicesCode.Should().Be("01");
        smartDataServices.RegistrationMeterConfigurationCode.Should().Be("MCC12");
        smartDataServices.EstimatedAnnualConsumption.Should().Be(2453m);
    }
}
