using System.Security.Cryptography;

namespace MeteringConsumption.MarketMessaging.Domain;

// Content-addressed: identical bytes from one market are one sealed file by construction, and the path is
// its own checksum. The operator's file name is a fact about the receipt, not about the sealed file.
public sealed record SealedFile(string Market, string Hash)
{
    public string Path => $"{Market}/{Hash}.xml";

    public static SealedFile Of(string market, ReadOnlySpan<byte> content) =>
        new(market, Convert.ToHexStringLower(SHA256.HashData(content)));
}
