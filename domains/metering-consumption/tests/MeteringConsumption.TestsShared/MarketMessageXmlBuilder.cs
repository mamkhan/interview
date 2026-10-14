using System.Globalization;
using System.Text;

namespace MeteringConsumption.TestsShared;

// Builds ESB market-message XML in the shape of the real extracts: a valid single-MPRN MM343 of 48 half-hour
// intervals by default, MM101 for code 101, MM105 for code 105, a valid single-meter MM300 for code 300, MM300S for
// code 300S, MM301 for code 301, MM306 for code 306, MM307 for code 307, MM310 for code 310, MM320 for code 320,
// MM331 for code 331, a valid MM332 for code 332, and a header-only document for any other message type code, which is enough for
// classification. A test overrides only what it asserts.
public sealed class MarketMessageXmlBuilder
{
    public const string NetworksReferenceNumber = "2100000012345";

    private const int IntervalMinutes = 30;
    private const int IntervalsPerDay = 48;

    private static readonly TimeSpan IrishSummerTime = TimeSpan.FromHours(1);

    private string _messageTypeCode = "343";
    private string _mprn = "10001234567";
    private string _meterSerial = "32300001";
    private DateOnly _readDate = new(2026, 6, 24);
    private int _rrvn = 1;
    private string _intervalValue = "0.5";
    private bool _withUsageFactors = true;
    private bool _withNetworksReferenceNumber = true;
    private bool _withEffectiveFromDate = true;

    public MarketMessageXmlBuilder WithMessageTypeCode(string code)
    {
        _messageTypeCode = code;
        return this;
    }

    public MarketMessageXmlBuilder WithMprn(string mprn)
    {
        _mprn = mprn;
        return this;
    }

    public MarketMessageXmlBuilder WithMeterSerial(string serial)
    {
        _meterSerial = serial;
        return this;
    }

    public MarketMessageXmlBuilder WithReadDate(DateOnly readDate)
    {
        _readDate = readDate;
        return this;
    }

    public MarketMessageXmlBuilder WithRrvn(int rrvn)
    {
        _rrvn = rrvn;
        return this;
    }

    public MarketMessageXmlBuilder WithIntervalValue(decimal intervalValue)
    {
        _intervalValue = intervalValue.ToString(CultureInfo.InvariantCulture);
        return this;
    }

    // UsageFactors is 0..n on an MM332; the MM320 always carries them.
    public MarketMessageXmlBuilder WithoutUsageFactors()
    {
        _withUsageFactors = false;
        return this;
    }

    // NetworksReferenceNumber is optional on an MM332.
    public MarketMessageXmlBuilder WithoutNetworksReferenceNumber()
    {
        _withNetworksReferenceNumber = false;
        return this;
    }

    // EffectiveFromDate is optional on an MM101.
    public MarketMessageXmlBuilder WithoutEffectiveFromDate()
    {
        _withEffectiveFromDate = false;
        return this;
    }

    public string BuildXml()
    {
        var header =
            $"""<MessageHeader MessageTypeCode="{_messageTypeCode}" VersionNumber="14.00.00" MarketTimestamp="2026-06-25T02:50:10.918Z" SenderID="DSO" RecipientID="S22" TxRefNbr="0000000705508326"/>""";

        if (_messageTypeCode == "320")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm320Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "101")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm101Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "105")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm105Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "300")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm300Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "300S")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm300SBody()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "306")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm306Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "307")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm307Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "310")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm310Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "301")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm301Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "331")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm331Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode == "332")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}{Mm332Body()}</ieXMLDocument>""";
        }

        if (_messageTypeCode != "343")
        {
            return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}</ieXMLDocument>""";
        }

        var invariant = CultureInfo.InvariantCulture;
        var body = new StringBuilder();
        body.Append(invariant,
            $"""<MPRNLevelInfo MPRN="{_mprn}" ReadDate="{_readDate:yyyy-MM-dd}" ReadingReplacementVersionNumber="{_rrvn}" AlertFlag="VV">""");
        body.Append(invariant, $"""<MeterID MeterCategoryCode="RM106" SerialNumber="{_meterSerial}">""");
        body.Append(invariant, $"""<ChannelInfo MeteringInterval="{IntervalMinutes}" RegisterTypeCode="70" UOM_Code="KWT">""");
        var dayStart = new DateTimeOffset(_readDate.Year, _readDate.Month, _readDate.Day, 0, 0, 0, IrishSummerTime);
        for (var period = 0; period < IntervalsPerDay; period++)
        {
            var periodStart = dayStart.AddMinutes(IntervalMinutes * period);
            body.Append(invariant,
                $"""<IntervalInfo IntervalValue="{_intervalValue}" IntervalPeriodTimestamp="{periodStart:yyyy-MM-ddTHH:mm:sszzz}" IntervalStatusCode="VVAK"/>""");
        }

        body.Append("</ChannelInfo></MeterID></MPRNLevelInfo>");

        return $"""<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>{header}<MIM343_SMARTMeteringIntervalData>{body}</MIM343_SMARTMeteringIntervalData></ieXMLDocument>""";
    }

    // A day/night meter point: one estimated usage factor and one register per timeslot, on a single meter.
    private string Mm320Body() =>
        $"""<MIM320_ValidatedCoSReading MPRN="{_mprn}" NetworksReferenceNumber="{NetworksReferenceNumber}" MeterPointStatusCode="1" LoadProfileCode="01" DUOS_Group="DG1" ReadDate="{_readDate:yyyy-MM-dd}" MeterConfigurationCode="NSH">"""
        + $"""<UsageFactors TimeslotCode="00D" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="2.4100"/>"""
        + $"""<UsageFactors TimeslotCode="00N" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="1.1300"/>"""
        + $"""<MeterID MeterCategoryCode="RM106" SerialNumber="{_meterSerial}" MeterLocationCode="01">"""
        + """<RegisterLevelInfo MeterRegisterSequence="1" TimeslotCode="00D" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="12345" ReadReasonCode="04" ReadTypeCode="A" RegisterTypeCode="01" PostDecimalDetails="0" PreDecimalDetails="5"/>"""
        + """<RegisterLevelInfo MeterRegisterSequence="2" TimeslotCode="00N" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="6789" ReadReasonCode="04" ReadTypeCode="A" RegisterTypeCode="01" PostDecimalDetails="0" PreDecimalDetails="5"/>"""
        + "</MeterID></MIM320_ValidatedCoSReading>";

    // A smart meter put in place on the read date, shaped like the real MM331 files: the meter point's codes without a
    // load profile, and one new meter with its two channels, half-hourly and daily. Real MM331s carry no networks
    // reference number.
    private string Mm331Body() =>
        $"""<MIM331_IntervalMeterTechnicalDetails MPRN="{_mprn}" DUOS_Group="DG1" MeterPointStatusCode="E" EffectiveFromDate="{_readDate:yyyy-MM-dd}" MeterConfigurationCode="MCC12">"""
        + $"""<NewMeterRegisters MeterCategoryCode="RM106" SerialNumber="{_meterSerial}" MeterLocationCode="07" ExchangedMeterReference="000000000032300001">"""
        + """<RegisterChannelInformation MeteringInterval="30" RegisterTypeCode="70" UOM_Code="KWT"/>"""
        + """<RegisterChannelInformation MeteringInterval="24" RegisterTypeCode="74" UOM_Code="KWH"/>"""
        + "</NewMeterRegisters></MIM331_IntervalMeterTechnicalDetails>";

    // A day/night meter point's technical details effective from the read date: its configuration, a day estimate,
    // a day actual and a night estimate, and the one meter it keeps. ESB states each figure on its own entry, so the
    // day rate takes two.
    private string Mm332Body()
    {
        var networksReferenceNumber = _withNetworksReferenceNumber ? $""" NetworksReferenceNumber="{NetworksReferenceNumber}" """ : " ";
        var usageFactors = _withUsageFactors
            ? $"""<UsageFactors TimeslotCode="00D" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="2.4100"/>"""
              + $"""<UsageFactors TimeslotCode="00D" EffectiveFromDate="{_readDate:yyyy-MM-dd}" ActualUsageFactor="2.5000"/>"""
              + $"""<UsageFactors TimeslotCode="00N" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="1.1300"/>"""
            : "";

        return $"""<MIM332_NonIntervalTechnicalMeterDetails MPRN="{_mprn}"{networksReferenceNumber}LoadProfileCode="01" DUOS_Group="DG1" MeterConfigurationCode="MCC01" MeterPointStatusCode="E" EffectiveFromDate="{_readDate:yyyy-MM-dd}">"""
            + usageFactors
            + $"""<RetainedMeterRegisters MeterCategoryCode="RM106" SerialNumber="{_meterSerial}">"""
            + """<RegisterLevelInfo TimeslotCode="00D" MeterRegisterSequence="1" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="12345" ReadReasonCode="04" ReadTypeCode="A" PreviousReadDate="2026-03-24" RegisterTypeCode="01" ReadStatusCode="V"/>"""
            + "</RetainedMeterRegisters></MIM332_NonIntervalTechnicalMeterDetails>";
    }

    // A day/night meter point's scheduled reading: two estimated usage factors (none when WithoutUsageFactors), one meter
    // with a register per timeslot, and one meter reader remark.
    private string Mm300Body()
    {
        var usageFactors = _withUsageFactors
            ? $"""<UsageFactors TimeslotCode="00D" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="2.4100"/>"""
              + $"""<UsageFactors TimeslotCode="00N" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="1.1300"/>"""
            : "";

        return $"""<MIM300_ValidatedNonIntervalReadingsScheduled MPRN="{_mprn}" NetworksReferenceNumber="{NetworksReferenceNumber}" LoadProfileCode="01" DUOS_Group="DG1" MeterPointStatusCode="1" ReadDate="{_readDate:yyyy-MM-dd}">"""
            + usageFactors
            + $"""<MeterID MeterCategoryCode="RM106" SerialNumber="{_meterSerial}">"""
            + """<RegisterLevelInfo MeterRegisterSequence="1" TimeslotCode="00D" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="12345" ReadReasonCode="01" ReadTypeCode="A" PreviousReadDate="2026-04-24" Consumption="812" ReadStatusCode="V" RegisterTypeCode="01"/>"""
            + """<RegisterLevelInfo MeterRegisterSequence="2" TimeslotCode="00N" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="6789" ReadReasonCode="01" ReadTypeCode="A" PreviousReadDate="2026-04-24" ReadStatusCode="V" RegisterTypeCode="01"/>"""
            + "</MeterID>"
            + """<MeterReaderRemarks MeterReaderRemarkCode="R07" MeterReaderRemarks="Meter behind locked gate"/>"""
            + "</MIM300_ValidatedNonIntervalReadingsScheduled>";
    }

    // A day/night meter point's special reading: two estimated usage factors (none when WithoutUsageFactors) and one meter
    // with a register per timeslot; unlike an MM300 it carries no meter reader remarks.
    private string Mm300SBody()
    {
        var usageFactors = _withUsageFactors
            ? $"""<UsageFactors TimeslotCode="00D" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="2.4100"/>"""
              + $"""<UsageFactors TimeslotCode="00N" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="1.1300"/>"""
            : "";

        return $"""<MIM300S_ValidatedNonIntervalReadingsSpecial MPRN="{_mprn}" NetworksReferenceNumber="{NetworksReferenceNumber}" LoadProfileCode="01" DUOS_Group="DG1" MeterPointStatusCode="1" ReadDate="{_readDate:yyyy-MM-dd}">"""
            + usageFactors
            + $"""<MeterID MeterCategoryCode="RM106" SerialNumber="{_meterSerial}">"""
            + """<RegisterLevelInfo MeterRegisterSequence="1" TimeslotCode="00D" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="12345" ReadReasonCode="09" ReadTypeCode="E" PreviousReadDate="2026-04-24" Consumption="812" ReadStatusCode="V" RegisterTypeCode="01"/>"""
            + """<RegisterLevelInfo MeterRegisterSequence="2" TimeslotCode="00N" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="6789" ReadReasonCode="09" ReadTypeCode="E" PreviousReadDate="2026-04-24" ReadStatusCode="V" RegisterTypeCode="01"/>"""
            + "</MeterID></MIM300S_ValidatedNonIntervalReadingsSpecial>";
    }

    // A day/night meter point de-energised from the read date: status DR, two estimated usage factors (none when
    // WithoutUsageFactors) and one meter with its closing register per timeslot.
    private string Mm306Body()
    {
        var usageFactors = _withUsageFactors
            ? $"""<UsageFactors TimeslotCode="00D" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="2.4100"/>"""
              + $"""<UsageFactors TimeslotCode="00N" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="1.1300"/>"""
            : "";

        return $"""<MIM306_MeterPointStatusChangeDeEnergisationRead MPRN="{_mprn}" NetworksReferenceNumber="{NetworksReferenceNumber}" MeterPointStatusCode="DR" LoadProfileCode="01" DUOS_Group="DG1" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EssentialPlantFlag="0">"""
            + usageFactors
            + $"""<MeterID MeterCategoryCode="RM106" SerialNumber="{_meterSerial}">"""
            + """<RegisterLevelInfo MeterRegisterSequence="1" TimeslotCode="00D" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="12345" ReadReasonCode="13" ReadTypeCode="A" PreviousReadDate="2026-04-24" Consumption="812" ReadStatusCode="RV" RegisterTypeCode="01"/>"""
            + """<RegisterLevelInfo MeterRegisterSequence="2" TimeslotCode="00N" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="6789" ReadReasonCode="13" ReadTypeCode="A" PreviousReadDate="2026-04-24" ReadStatusCode="RV" RegisterTypeCode="01"/>"""
            + "</MeterID></MIM306_MeterPointStatusChangeDeEnergisationRead>";
    }

    // A day/night meter point energised from the read date: status E, two estimated usage factors (none when
    // WithoutUsageFactors) and one meter with its opening register per timeslot.
    private string Mm307Body()
    {
        var usageFactors = _withUsageFactors
            ? $"""<UsageFactors TimeslotCode="00D" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="2.4100"/>"""
              + $"""<UsageFactors TimeslotCode="00N" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EstimatedUsageFactor="1.1300"/>"""
            : "";

        return $"""<MIM307_MeterPointStatusChangeEnergisationRead MPRN="{_mprn}" NetworksReferenceNumber="{NetworksReferenceNumber}" MeterPointStatusCode="E" LoadProfileCode="01" DUOS_Group="DG1" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EssentialPlantFlag="0">"""
            + usageFactors
            + $"""<MeterID MeterCategoryCode="RM106" SerialNumber="{_meterSerial}">"""
            + """<RegisterLevelInfo MeterRegisterSequence="1" TimeslotCode="00D" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="12345" ReadReasonCode="18" ReadTypeCode="A" PreviousReadDate="2026-04-24" Consumption="812" ReadStatusCode="RV" RegisterTypeCode="01"/>"""
            + """<RegisterLevelInfo MeterRegisterSequence="2" TimeslotCode="00N" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="6789" ReadReasonCode="18" ReadTypeCode="A" PreviousReadDate="2026-04-24" ReadStatusCode="RV" RegisterTypeCode="01"/>"""
            + "</MeterID></MIM307_MeterPointStatusChangeEnergisationRead>";
    }

    // A day/night meter point's closing read to the losing supplier: two actual usage factors (none when
    // WithoutUsageFactors), the only kind a 310 carries, and one meter with a register per timeslot.
    private string Mm310Body()
    {
        var usageFactors = _withUsageFactors
            ? $"""<UsageFactors TimeslotCode="00D" EffectiveFromDate="{_readDate:yyyy-MM-dd}" ActualUsageFactor="2.5000"/>"""
              + $"""<UsageFactors TimeslotCode="00N" EffectiveFromDate="{_readDate:yyyy-MM-dd}" ActualUsageFactor="1.2000"/>"""
            : "";

        return $"""<MIM310_ValidatedCoSReading MPRN="{_mprn}" NetworksReferenceNumber="{NetworksReferenceNumber}" MeterPointStatusCode="1" LoadProfileCode="01" DUOS_Group="DG1" ReadDate="{_readDate:yyyy-MM-dd}">"""
            + usageFactors
            + $"""<MeterID MeterCategoryCode="RM106" SerialNumber="{_meterSerial}">"""
            + """<RegisterLevelInfo MeterRegisterSequence="1" TimeslotCode="00D" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="12345" ReadReasonCode="26" ReadTypeCode="A" PreviousReadDate="2026-04-24" Consumption="812" ReadStatusCode="RV" RegisterTypeCode="01"/>"""
            + """<RegisterLevelInfo MeterRegisterSequence="2" TimeslotCode="00N" UOM_Code="KWH" MeterMultiplier="1" ReadingValue="6789" ReadReasonCode="26" ReadTypeCode="A" PreviousReadDate="2026-04-24" ReadStatusCode="RV" RegisterTypeCode="01"/>"""
            + "</MeterID></MIM310_ValidatedCoSReading>";
    }

    // A meter point's changed connection characteristics effective from the read date: its configuration code, load
    // profile and DUoS group, its import and export capacities, and a made-up premises address. A 301 has no status.
    private string Mm301Body() =>
        $"""<MIM301_MeterPointCharacteristics MPRN="{_mprn}" DLF_Code="LV" ConnectionSystemCode="DSO_GRID" LoadProfileCode="01" MaximumImportCapacity="12" MICStartDate="{_readDate:yyyy-MM-dd}" MaximumExportCapacity="5" MeterConfigurationCode="MCC12" DUOS_Group="DG1" EffectiveFromDate="{_readDate:yyyy-MM-dd}" EssentialPlantFlag="0">"""
        + """<MeterPointAddress HouseNo="1" Street="MAIN STREET" City="TESTTOWN" CountyIreland="KE" Country="IE"/>"""
        + "</MIM301_MeterPointCharacteristics>";

    // A new connection accepted from the read date, with every personal-data element filled in, all of it made up: the
    // customer, their contacts and notification address, a technical contact, one special need and smart data services.
    private string Mm101Body()
    {
        var effectiveFromDate = _withEffectiveFromDate ? $""" EffectiveFromDate="{_readDate:yyyy-MM-dd}" """ : " ";

        return $"""<MIM101_NewConnectionRegistrationAcceptance MPRN="{_mprn}" MPBusinessReference="010_S22_M3_0000000000000000101" MeterPointStatusCode="E" SupplierMPID="S22" SupplierUnitID="SU_400147" SSAC="A" SettlementClassCode="N" ConnectionSystemCode="DSO_GRID" DUOS_Group="DG1" MaximumImportCapacity="12" MeterConfigurationCode="MCC02" DLF_Code="LV" COT_LE_Flag="1" MedicalEquipmentDetailsCode="M1" ReadFrequencyCode="B" ReadCycle="07" RegistrationReceiptDate="2026-06-20" LoadProfileCode="02"{effectiveFromDate}EssentialPlantFlag="0" CommsTechnicallyFeasible="04">"""
            + """<MeterPointAddress HouseNo="1" Street="MAIN STREET" City="TESTTOWN" CountyIreland="DB" Country="IE"/>"""
            + """<CustomerName Title="Ms" LastName="Tester" FirstName="Test"/>"""
            + """<CustomerContactDetails Email="test@example.invalid"><PhoneOne Number="0850000000"/><PhoneTwo Number="0850000001" Extn="12"/><Fax Number="0100000000"/></CustomerContactDetails>"""
            + """<NotificationAddress><Address COName="Test Tester" Street="CHURCH ROAD" City="TESTTOWN" Country="IE"/><PostBox POBoxNumber="42" City="TESTTOWN"/></NotificationAddress>"""
            + """<TechnicalContact ContactName="Test Engineer"><TechnicalContactDetails Email="engineer@example.invalid"><PhoneOne Number="0850000002"/></TechnicalContactDetails><Address Street="DEPOT LANE" City="TESTTOWN"/></TechnicalContact>"""
            + """<CustomerServiceSpecialNeeds CustomerServiceDetailsCode="SN01"/>"""
            + """<SmartDataServices SmartDataServicesCode="SDS1" RegistrationMeterConfigurationCode="MCC02"/>"""
            + "</MIM101_NewConnectionRegistrationAcceptance>";
    }

    // A change of supplier confirmed from the read date, with every personal-data element filled in, all of it made up,
    // and the smart data services element under the name ESB sends it. A 105 has no load profile.
    private string Mm105Body() =>
        $"""<MIM105_ChangeofSupplierConfirmation MPRN="{_mprn}" MPBusinessReference="010_00000000000000000_010_000105" MeterPointStatusCode="E" SupplierMPID="S22" SupplierUnitID="SU_400400" SSAC="A" SettlementClassCode="P" ConnectionSystemCode="DSO_GRID" DUOS_Group="DG2" MaximumImportCapacity="12" MICStartDate="2026-01-01" CurrentChargeableServiceCapacity="9" MeterConfigurationCode="MCC12" DLF_Code="LV" MedicalEquipmentDetailsCode="M1" SupplierMPIDOld="S10" EffectiveFromDate="{_readDate:yyyy-MM-dd}" DUoSBillingFrequencyCode="B" DuosBillingCycle="38" CommsTechnicallyFeasible="04">"""
        + """<MeterPointAddress HouseNo="1" Street="MAIN STREET" City="TESTTOWN" CountyIreland="DL" Country="IE"/>"""
        + """<CustomerName Title="Mr" LastName="Tester" FirstName="Test"/>"""
        + """<CustomerContactDetails Email="test@example.invalid"><PhoneOne Number="0850000000"/></CustomerContactDetails>"""
        + """<NotificationAddress><Address Street="CHURCH ROAD" City="TESTTOWN"/></NotificationAddress>"""
        + """<TechnicalContact ContactName="Test Engineer"/>"""
        + """<CustomerServiceSpecialNeeds CustomerServiceDetailsCode="SN01"/>"""
        + """<MIM105_SmartDataServices SmartDataServicesCode="01" RegistrationMeterConfigurationCode="MCC12" EstimatedAnnualConsumption="2453"/>"""
        + "</MIM105_ChangeofSupplierConfirmation>";

    public byte[] BuildBytes() => Encoding.UTF8.GetBytes(BuildXml());

    public Stream BuildStream() => new MemoryStream(BuildBytes());
}
