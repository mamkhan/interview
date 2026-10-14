using System.Security.Cryptography;

namespace BuildingBlocks.Seedwork;

/// <summary>A GUID derived from its seed, never minted per attempt: the first 16 bytes of the seed's UTF-8
/// SHA-256. A redelivery that derives an event id from the same inputs gets the same id.</summary>
public static class DeterministicGuid
{
    public static Guid From(string seed) =>
        new(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed)).AsSpan(0, 16));
}
