namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class RetainedMeterRegisters
{
    public string? MeterCategoryCode { get; set; }

    public string? SerialNumber { get; set; }

    public IReadOnlyList<RegisterLevelInfo>? RegisterLevelInfos { get; set; }
}
