namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

// The customer's contact details are personal data: no event or log carries them.
public class CustomerContactDetails
{
    public string? Email { get; set; }

    public Fax? Fax { get; set; }

    public PhoneOne? PhoneOne { get; set; }

    public PhoneTwo? PhoneTwo { get; set; }
}
