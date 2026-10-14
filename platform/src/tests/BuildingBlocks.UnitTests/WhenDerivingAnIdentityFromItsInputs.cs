using AwesomeAssertions;
using BuildingBlocks.Seedwork;

namespace BuildingBlocks.UnitTests;

// The ids are derived offline in Python, independently of this code: the first 16 bytes of the seed's UTF-8
// SHA-256 read as a little-endian GUID, `uuid.UUID(bytes_le=hashlib.sha256(seed.encode()).digest()[:16])`.
// Published event ids depend on them, so a change here is a change to every redelivered event's identity.
public class WhenDerivingAnIdentityFromItsInputs
{
    [Fact]
    public void DerivesTheSameGuidForABlobEventId()
    {
        DeterministicGuid.From("aa1c0987-abc6-be22-791c-cb0cd05e7d3f")
            .Should().Be(Guid.Parse("414ffbbc-1d4b-7107-eb13-19b3e00b2917"));
    }

    [Fact]
    public void DerivesTheSameGuidForAReadingId()
    {
        DeterministicGuid.From("e00e47bd:10900000343:2026-06-24:1")
            .Should().Be(Guid.Parse("e4b36e28-195c-700c-5707-444d6a393e08"));
    }

    [Fact]
    public void DerivesDifferentGuidsForDifferentSeeds()
    {
        DeterministicGuid.From("e00e47bd:10900000343:2026-06-24:1")
            .Should().NotBe(DeterministicGuid.From("e00e47bd:10900000343:2026-06-24:2"));
    }
}
