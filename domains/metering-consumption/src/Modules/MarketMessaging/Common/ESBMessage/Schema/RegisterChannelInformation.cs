using System.Diagnostics.CodeAnalysis;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class RegisterChannelInformation
{
    public int? MeteringInterval { get; set; }

    public string? RegisterTypeCode { get; set; }

    [SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Mirrors the ESB XSD attribute name; ESBMessageParser binds by property name.")]
    public string? UOM_Code { get; set; }
}
