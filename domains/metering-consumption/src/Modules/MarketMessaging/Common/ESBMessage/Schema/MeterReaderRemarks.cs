using System.Xml.Serialization;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class MeterReaderRemarks
{
    public string? MeterReaderRemarkCode { get; set; }

    [XmlAttribute("MeterReaderRemarks")]
    public string? RemarkText { get; set; }
}
