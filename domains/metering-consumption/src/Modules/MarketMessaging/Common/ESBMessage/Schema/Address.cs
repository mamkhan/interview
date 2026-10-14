namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

// A postal address, under NotificationAddress or TechnicalContact; personal data that no event or log carries.
public class Address
{
    public string? AddrLine1 { get; set; }

    public string? AddrLine2 { get; set; }

    public string? AddrLine4 { get; set; }

    public string? AddrLine5 { get; set; }

    public string? City { get; set; }

    public string? COName { get; set; }

    public string? Country { get; set; }

    public string? CountyIreland { get; set; }

    public string? CountyState { get; set; }

    public string? HouseNo { get; set; }

    public string? PostCode { get; set; }

    public string? Street { get; set; }

    public string? UnitNo { get; set; }
}
