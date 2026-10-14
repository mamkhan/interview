using System.Diagnostics.CodeAnalysis;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

// ESB sends this element as MIM105_SmartDataServices, though the message guide names it SmartDataServices_MIM105; the
// parser binds by class name, so the class follows the files.
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Mirrors the ESB element name; ESBMessageParser binds by name.")]
public class MIM105_SmartDataServices
{
    public decimal? EstimatedAnnualConsumption { get; set; }

    public string? RegistrationMeterConfigurationCode { get; set; }

    public string? SmartDataServicesCode { get; set; }
}
