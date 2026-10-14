namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

// The premises address is personal data: it is parsed so the body is complete, and no event or log carries it.
public class MeterPointAddress
{
    public string? AddrLine1 { get; set; }

    public string? AddrLine2 { get; set; }

    public string? AddrLine4 { get; set; }

    public string? AddrLine5 { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? CountyIreland { get; set; }

    public string? HouseNo { get; set; }

    public string? PostCode { get; set; }

    public string? Street { get; set; }

    public string? UnitNo { get; set; }
}
