namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

// The technical contact is personal data: no event or log carries it.
public class TechnicalContact
{
    public string? ContactName { get; set; }

    public Address? Address { get; set; }

    public TechnicalContactDetails? TechnicalContactDetails { get; set; }
}
