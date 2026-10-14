namespace MeteringConsumption.MarketMessaging.UnitTests.Domain;

public sealed record MarketIdentity
{
    public MarketIdentity(string market, string fuel)
    {
        Market = Tag(market, nameof(market));
        Fuel = Tag(fuel, nameof(fuel));
    }

    public string Market { get; }

    public string Fuel { get; }

    // Lowercase hyphenated is the tag form the whole platform reads, and these values reach blob paths
    // and subscription filters, where a stray case difference is a silently empty subscription.
    private static string Tag(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An identity tag cannot be blank.", name);
        }

        var trimmed = value.Trim();
        return trimmed.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
            ? trimmed
            : throw new ArgumentException(
                $"'{value}' is not a lowercase hyphenated identity tag.", name);
    }
}
