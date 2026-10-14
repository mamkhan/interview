using System.Diagnostics.CodeAnalysis;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Mirrors the ESB XSD element and attribute names; ESBMessageParser binds by name.")]
public class MIM332_NonIntervalTechnicalMeterDetails
{
    public string? DUOS_Group { get; set; }

    public DateOnly? EffectiveFromDate { get; set; }

    public bool? EssentialPlantFlag { get; set; }

    public string? KeypadPremisesNumber { get; set; }

    public string? LoadProfileCode { get; set; }

    public string? MeterConfigurationCode { get; set; }

    public string? MeterPointStatusCode { get; set; }

    public string? MPBusinessReference { get; set; }

    public string? MPRN { get; set; }

    public string? NetworksReferenceNumber { get; set; }

    public string? TariffConfigurationCode { get; set; }

    public IReadOnlyList<NewMeterRegisters>? NewMeterRegisters { get; set; }

    public IReadOnlyList<RemovedMeterRegisters>? RemovedMeterRegisters { get; set; }

    public IReadOnlyList<RetainedMeterRegisters>? RetainedMeterRegisters { get; set; }

    public IReadOnlyList<UsageFactors>? UsageFactors { get; set; }
}
