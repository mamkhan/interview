using System.Diagnostics.CodeAnalysis;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Mirrors the ESB XSD element and attribute names; ESBMessageParser binds by name.")]
public class MIM105_ChangeofSupplierConfirmation
{
    // A two-digit code ("04"), not the flag its name suggests.
    public string? CommsTechnicallyFeasible { get; set; }

    public string? ConnectionSystemCode { get; set; }

    public decimal? CurrentChargeableServiceCapacity { get; set; }

    public string? DLF_Code { get; set; }

    public string? DUoSBillingFrequencyCode { get; set; }

    public string? DuosBillingCycle { get; set; }

    public string? DUOS_Group { get; set; }

    public DateOnly? EffectiveFromDate { get; set; }

    public bool? EssentialPlantFlag { get; set; }

    public string? KeypadPremisesNumber { get; set; }

    public string? LoadFactorCode { get; set; }

    public decimal? MaximumExportCapacity { get; set; }

    public decimal? MaximumImportCapacity { get; set; }

    // A medical equipment code is health data, a special category of personal data: no event or log carries it.
    public string? MedicalEquipmentDetailsCode { get; set; }

    public string? MeterConfigurationCode { get; set; }

    public string? MeterPointStatusCode { get; set; }

    public DateOnly? MICStartDate { get; set; }

    public string? MPBusinessReference { get; set; }

    public string? MPRN { get; set; }

    public string? ReadCycle { get; set; }

    public string? ReadFrequencyCode { get; set; }

    public string? SettlementClassCode { get; set; }

    public string? SmartNonParticipationCode { get; set; }

    public string? SSAC { get; set; }

    public string? SupplierMPID { get; set; }

    public string? SupplierMPIDOld { get; set; }

    public string? SupplierUnitID { get; set; }

    public decimal? TransformerLossFactor { get; set; }

    public CustomerContactDetails? CustomerContactDetails { get; set; }

    public CustomerName? CustomerName { get; set; }

    public IReadOnlyList<CustomerServiceSpecialNeeds>? CustomerServiceSpecialNeeds { get; set; }

    public MeterPointAddress? MeterPointAddress { get; set; }

    public MIM105_SmartDataServices? MIM105_SmartDataServices { get; set; }

    public NotificationAddress? NotificationAddress { get; set; }

    public TechnicalContact? TechnicalContact { get; set; }
}
