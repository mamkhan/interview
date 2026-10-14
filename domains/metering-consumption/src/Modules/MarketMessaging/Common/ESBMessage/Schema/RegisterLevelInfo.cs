using System.Diagnostics.CodeAnalysis;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class RegisterLevelInfo
{
    public decimal? Consumption { get; set; }

    public decimal? EstimatedConsumption { get; set; }

    public decimal? MeterMultiplier { get; set; }

    public string? MeterRegisterSequence { get; set; }

    public string? PostDecimalDetails { get; set; }

    public string? PreDecimalDetails { get; set; }

    public DateOnly? PreviousReadDate { get; set; }

    public decimal? ReadingValue { get; set; }

    public string? ReadReasonCode { get; set; }

    public string? ReadStatusCode { get; set; }

    public string? ReadTypeCode { get; set; }

    public string? RegisterTypeCode { get; set; }

    public string? TimeslotCode { get; set; }

    [SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Mirrors the ESB XSD attribute name; ESBMessageParser binds by property name.")]
    public string? UOM_Code { get; set; }
}
