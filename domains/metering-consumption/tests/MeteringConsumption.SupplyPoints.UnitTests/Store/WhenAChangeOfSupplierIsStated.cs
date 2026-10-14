using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Store;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// The real MM320 sample as the builder states it, read on 2026-09-30 with no effective date: meter 90000320 (RM107,
// location 07) in place with registers 001 (01D, type 78), 002 (01N, 79) and 003 (01P, 80), each KWH, multiplier 1,
// pre-decimal 07, post-decimal 0, read 0 (type A, reason 26). Everything takes effect on the read date; today is
// 2026-10-13, so those rows are today's.
public class WhenAChangeOfSupplierIsStated
{
    private readonly StoreRig _rig = new();

    public WhenAChangeOfSupplierIsStated()
    {
        _rig.Handler<MeterConfigurationChangeReceivedEventHandler>()
            .HandleAsync(new MeterConfigurationChangeReceivedBuilder().AsTheRealMm320States().Build(), CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    [Fact]
    public void StoresTheMeterInPlaceFromTheReadDate() =>
        _rig.Repository.Meter("10900000320", "90000320").Current.Should().Equal(
            new Dated<MeterDetails>(new DateOnly(2026, 9, 30), new MeterDetails("RM107", "07", null)));

    [Fact]
    public void StoresTheRegisterWithItsReadUnderItsSequenceNumber() =>
        _rig.Repository.Register("10900000320", "90000320", "001").Current.Should().Equal(new Dated<RegisterValues>(
            new DateOnly(2026, 9, 30),
            new RegisterValues(
                new RegisterDetails
                {
                    TimeslotCode = "01D",
                    RegisterTypeCode = "78",
                    UomCode = "KWH",
                    MeterMultiplier = 1m,
                    PreDecimalDetails = "07",
                    PostDecimalDetails = "0",
                },
                new RegisterRead(0m, "A", "26", null, null))));

    [Fact]
    public void StoresEveryOtherRegisterTheMeterListsUnderItsOwnSequenceNumber()
    {
        _rig.Repository.Register("10900000320", "90000320", "002").Current.Should().ContainSingle()
            .Which.Values.Details.TimeslotCode.Should().Be("01N");
        _rig.Repository.Register("10900000320", "90000320", "003").Current.Should().ContainSingle()
            .Which.Values.Details.TimeslotCode.Should().Be("01P");
    }

    [Fact]
    public void StoresTheMeterPointAndAnEstimateForEachRateInTheSameUnitOfWork()
    {
        _rig.Repository.MeterPoint("10900000320").Current.Single().Values.Should().Be(
            new MeterPointValues("MCC16", "25", "DG1", "E", "019000000320"));
        _rig.Repository.UsageFactor("10900000320", "01D").Current.Should().ContainSingle().Which.Values.Estimated.Should().Be(4389.66894m);
        _rig.Repository.UsageFactor("10900000320", "01N").Current.Should().ContainSingle().Which.Values.Estimated.Should().Be(6402.89496m);
        _rig.Repository.UsageFactor("10900000320", "01P").Current.Should().ContainSingle().Which.Values.Estimated.Should().Be(886.4361m);
        _rig.UnitOfWork.Commits.Should().Be(1);
    }
}
