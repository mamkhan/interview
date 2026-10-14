namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class RemovedMeterRegisters
{
    public string? ExchangedMeterReference { get; set; }

    public string? MeterCategoryCode { get; set; }

    public string? SerialNumber { get; set; }

    public IReadOnlyList<RegisterLevelInfo>? RegisterLevelInfos { get; set; }
}
