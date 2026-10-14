using System.Diagnostics.Metrics;
using AwesomeAssertions;
using MeteringConsumption.Consumption.Application.Features.Record;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// K-1: one MM343 carries two meters for one supply-point day, and Market Messaging gives both readings one id.
// Meter M123456 (0.5 KWT, 0.25 kWh a half-hour) is kept first; meter M999888 (0.6 KWT, 0.30 kWh) arrives as the
// same version with other values. The first stays, nothing more is written or announced, and the second is counted and named.
public sealed class WhenTwoMetersReportTheSameSupplyPointDayInOneFile : IAsyncLifetime
{
    private readonly RecordHandling _handling = new();
    private long _counted;

    public async ValueTask InitializeAsync()
    {
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meters) =>
        {
            if (instrument.Meter.Name == RecordTelemetry.MeterName && instrument.Name == RecordTelemetry.ConflictingResendsName)
            {
                meters.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => Interlocked.Add(ref _counted, measurement));
        listener.Start();

        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().Build());
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder()
            .FromMeter(MeterDayReadingBuilder.ExchangedMeterSerial).WithUniformValue(0.60m).Build());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void KeepsTheFirstMetersReadingOnly()
    {
        _handling.Store.Appends.Should().Be(1);
        _handling.Store.Kept.Single().Entry.MeterSerial.Should().Be("M123456");
    }

    [Fact]
    public void AnnouncesOnlyTheFirst() => _handling.Publisher.Published.Should().ContainSingle();

    [Fact]
    public void WarnsNamingBothMeters() =>
        _handling.Logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning
            && entry.Message.Contains("M999888") && entry.Message.Contains("M123456"));

    [Fact]
    public void CountsTheConflictingResendOnce() => Interlocked.Read(ref _counted).Should().Be(1);
}
