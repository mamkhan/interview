using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The builder's MM101 bound onto the schema the way the parser binds the real files: the registration, and every
// personal-data element under it, all made up. The same PhoneOne and Address classes bind under both the customer's and
// the technical contact's elements. Frozen literals from MarketMessageXmlBuilder: MPRN 10900000343, status E, MCC02,
// load profile 02, DUOS group DG1, received 2026-06-20, effective 2026-06-24, customer Ms Test Tester.
public class WhenParsingAnMm101Message
{
    private const string Mprn = "10900000343";

    private readonly Message<MIM101_NewConnectionRegistrationAcceptance> _parsed;

    public WhenParsingAnMm101Message()
    {
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("101").WithMprn(Mprn).BuildStream();
        _parsed = ESBMessageParser.ParseDocument<Message<MIM101_NewConnectionRegistrationAcceptance>>(payload);
    }

    private MIM101_NewConnectionRegistrationAcceptance Body => _parsed.MessageBody!;

    [Fact]
    public void ReadsTheMeterPointsConfiguration()
    {
        Body.MPRN.Should().Be(Mprn);
        Body.MeterPointStatusCode.Should().Be("E");
        Body.MeterConfigurationCode.Should().Be("MCC02");
        Body.LoadProfileCode.Should().Be("02");
        Body.DUOS_Group.Should().Be("DG1");
        Body.EffectiveFromDate.Should().Be(new DateOnly(2026, 6, 24));
        Body.RegistrationReceiptDate.Should().Be(new DateOnly(2026, 6, 20));
    }

    [Fact]
    public void ReadsTheRegistrationsMarketDetails()
    {
        Body.SupplierMPID.Should().Be("S22");
        Body.SupplierUnitID.Should().Be("SU_400147");
        Body.SSAC.Should().Be("A");
        Body.SettlementClassCode.Should().Be("N");
        Body.ConnectionSystemCode.Should().Be("DSO_GRID");
        Body.DLF_Code.Should().Be("LV");
        Body.MaximumImportCapacity.Should().Be(12m);
        Body.COT_LE_Flag.Should().BeTrue();
        Body.EssentialPlantFlag.Should().BeFalse();
        Body.ReadFrequencyCode.Should().Be("B");
        Body.ReadCycle.Should().Be("07");
    }

    [Fact]
    public void ReadsCommsTechnicallyFeasibleAsTheCodeItIs() => Body.CommsTechnicallyFeasible.Should().Be("04");

    [Fact]
    public void ReadsTheCustomerAndTheirContactDetails()
    {
        Body.CustomerName!.FirstName.Should().Be("Test");
        Body.CustomerName.LastName.Should().Be("Tester");
        Body.CustomerName.Title.Should().Be("Ms");
        Body.CustomerContactDetails!.Email.Should().Be("test@example.invalid");
        Body.CustomerContactDetails.PhoneOne!.Number.Should().Be("0850000000");
        Body.CustomerContactDetails.PhoneTwo!.Extn.Should().Be("12");
        Body.CustomerContactDetails.Fax!.Number.Should().Be("0100000000");
    }

    [Fact]
    public void ReadsTheNotificationAddressAndPostBox()
    {
        Body.NotificationAddress!.Address!.COName.Should().Be("Test Tester");
        Body.NotificationAddress.Address.Street.Should().Be("CHURCH ROAD");
        Body.NotificationAddress.PostBox!.POBoxNumber.Should().Be("42");
    }

    [Fact]
    public void ReadsTheTechnicalContact()
    {
        Body.TechnicalContact!.ContactName.Should().Be("Test Engineer");
        Body.TechnicalContact.TechnicalContactDetails!.PhoneOne!.Number.Should().Be("0850000002");
        Body.TechnicalContact.Address!.Street.Should().Be("DEPOT LANE");
    }

    [Fact]
    public void ReadsTheSpecialNeedsAndTheMedicalEquipmentCode()
    {
        Body.CustomerServiceSpecialNeeds!.Select(n => n.CustomerServiceDetailsCode).Should().Equal("SN01");
        Body.MedicalEquipmentDetailsCode.Should().Be("M1");
    }

    [Fact]
    public void ReadsTheSmartDataServices()
    {
        Body.SmartDataServices!.SmartDataServicesCode.Should().Be("SDS1");
        Body.SmartDataServices.RegistrationMeterConfigurationCode.Should().Be("MCC02");
    }

    [Fact]
    public void ReadsThePremisesAddress() => Body.MeterPointAddress!.Street.Should().Be("MAIN STREET");
}
