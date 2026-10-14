namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class MeterID
{
    public string? MeterCategoryCode { get; set; }

    public string? MeterLocationCode { get; set; }

    public string? SerialNumber { get; set; }

    public IReadOnlyList<ChannelInfo>? ChannelInfos { get; set; }

    public IReadOnlyList<RegisterLevelInfo>? RegisterLevelInfos { get; set; }
}
