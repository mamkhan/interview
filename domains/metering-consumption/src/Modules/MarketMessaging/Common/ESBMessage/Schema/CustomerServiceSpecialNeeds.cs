namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

// A special need is health or vulnerability data, a special category of personal data: no event or log carries it.
public class CustomerServiceSpecialNeeds
{
    public string? CustomerServiceDetailsCode { get; set; }
}
