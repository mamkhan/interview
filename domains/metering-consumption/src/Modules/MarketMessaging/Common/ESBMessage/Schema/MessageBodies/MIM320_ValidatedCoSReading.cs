using System.Diagnostics.CodeAnalysis;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Mirrors the ESB XSD element and attribute names; ESBMessageParser binds by name.")]
public class MIM320_ValidatedCoSReading
{
    public string? DUOS_Group { get; set; }

    public string? KeypadPremisesNumber { get; set; }

    public string? LoadProfileCode { get; set; }

    public string? MeterConfigurationCode { get; set; }

    public string? MeterPointStatusCode { get; set; }

    public string? MPBusinessReference { get; set; }

    public string? MPRN { get; set; }

    public string? NetworksReferenceNumber { get; set; }

    public DateOnly? ReadDate { get; set; }

    public IReadOnlyList<MeterID>? MeterIDs { get; set; }

    public IReadOnlyList<UsageFactors>? UsageFactors { get; set; }
}
