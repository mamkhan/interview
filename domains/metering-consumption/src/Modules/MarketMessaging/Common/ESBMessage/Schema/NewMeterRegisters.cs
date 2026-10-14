namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class NewMeterRegisters
{
    public string? ExchangedMeterReference { get; set; }

    public string? MeterCategoryCode { get; set; }

    public string? MeterLocationCode { get; set; }

    public string? SerialNumber { get; set; }

    public IReadOnlyList<RegisterLevelInfo>? RegisterLevelInfos { get; set; }

    // An MM331 lists a smart meter's channels where an MM332 lists registers.
    public IReadOnlyList<RegisterChannelInformation>? RegisterChannelInformations { get; set; }
}
