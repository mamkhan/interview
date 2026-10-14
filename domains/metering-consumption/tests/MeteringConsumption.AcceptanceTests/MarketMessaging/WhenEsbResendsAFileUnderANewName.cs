using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.MarketMessaging.Scenarios;
using MeteringConsumption.Contracts.Events;

namespace MeteringConsumption.AcceptanceTests.MarketMessaging;

// ESB sends the same MM343 bytes twice under two names. Each arrival is its own receipt, the payload is archived once
// under its hash, and the meter-day keeps the version from the first file: the resend is the same version, so the
// ledger keeps nothing new until the revision that follows it.
public sealed class WhenEsbResendsAFileUnderANewName(EsbResentAFileUnderANewNameScenario resent)
    : IClassFixture<EsbResentAFileUnderANewNameScenario>
{
    [Fact]
    public void RecordsEachArrivalAsItsOwnReceiptOfTheSameBytes()
    {
        resent.Resent.ReceiptId.Should().NotBe(resent.First.ReceiptId);
        resent.Resent.Hash.Should().Be(resent.First.Hash);
    }

    [Fact]
    public void ProcessesBothArrivals()
    {
        resent.First.Outcome.Should().Be(MarketMessageOutcomes.Processed);
        resent.First.ReadingsPublished.Should().Be(1);
        resent.Resent.Outcome.Should().Be(MarketMessageOutcomes.Processed);
        resent.Resent.ReadingsPublished.Should().Be(1);
    }

    [Fact]
    public void ArchivesThePayloadOnce()
    {
        resent.Resent.BlobPath.Should().Be(resent.First.BlobPath);
        resent.ArchivedUnderTheHash.Should().Equal(resent.First.BlobPath);
    }

    [Fact]
    public void KeepsTheVersionFromTheFirstFile()
    {
        resent.Ledger.Select(recorded => recorded.Entry.Rrvn).Should().Equal(1, 2);
        resent.Ledger[0].Provenance.ReceivedFileName.Should().Be(resent.FirstFileName);
    }
}
