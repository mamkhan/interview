namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

// The customer's name is personal data: it is parsed so the body is complete, and no event or log carries it.
public class CustomerName
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? OrganisationOne { get; set; }

    public string? OrganisationTwo { get; set; }

    public string? RegisteredCompanyNumber { get; set; }

    public string? Title { get; set; }

    public string? TradingAs { get; set; }
}
