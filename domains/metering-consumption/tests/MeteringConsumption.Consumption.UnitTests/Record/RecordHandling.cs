using MeteringConsumption.Consumption.Application.Features.Record;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// The Record handler over one fake ledger and publisher, each delivery at its own clock time, the way a
// redelivery reaches a fresh scope with the same kept state.
internal sealed class RecordHandling
{
    public static readonly DateTimeOffset Now = new(2026, 6, 21, 2, 14, 0, 417, TimeSpan.Zero);

    private int _deliveries;

    public RecordHandling()
    {
        Store = new FakeLedgerStore(Log);
        Publisher = new RecordingPublisher(Log);
    }

    public CallLog Log { get; } = new();

    public FakeLedgerStore Store { get; }

    public RecordingPublisher Publisher { get; }

    public RecordingLogger<RecordIntervalReadingReceivedEventHandler> Logger { get; } = new();

    public IEnumerable<ConsumptionRecorded> Announced => Publisher.Published.Select(published => (ConsumptionRecorded)published.Event);

    public Task DeliverAsync(IntervalReadingReceived reading) =>
        new RecordIntervalReadingReceivedEventHandler(Store, Publisher, new FixedTimeProvider(Now.AddHours(_deliveries++)), Logger)
            .HandleAsync(reading, CancellationToken.None);
}
