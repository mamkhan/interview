using System.Diagnostics.CodeAnalysis;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Mirrors the ESB XSD element and attribute names; ESBMessageParser binds by name.")]
public class MIM301_MeterPointCharacteristics
{
    public string? ConnectionSystemCode { get; set; }

    public DateOnly? CSC5YrEndDate { get; set; }

    public DateOnly? CSCStartDate { get; set; }

    public decimal? CurrentChargeableServiceCapacity { get; set; }

    public string? DLF_Code { get; set; }

    public string? DUOS_Group { get; set; }

    public DateOnly? EffectiveFromDate { get; set; }

    public bool? EssentialPlantFlag { get; set; }

    public string? LoadFactorCode { get; set; }

    public string? LoadProfileCode { get; set; }

    public decimal? MaximumExportCapacity { get; set; }

    public decimal? MaximumImportCapacity { get; set; }

    public string? MeterConfigurationCode { get; set; }

    public DateOnly? MICStartDate { get; set; }

    public string? MPRN { get; set; }

    public decimal? TransformerLossFactor { get; set; }

    public MeterPointAddress? MeterPointAddress { get; set; }
}
