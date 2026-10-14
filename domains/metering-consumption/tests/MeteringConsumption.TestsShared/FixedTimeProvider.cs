namespace MeteringConsumption.TestsShared;

// The clock stands still, so a wait taken on it is over at once: a retry's back-off costs a test no real time.
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    // Off the caller's thread, because Task.Delay keeps the timer it is handed before the callback may run.
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }

        return new StoppedTimer();
    }

    private sealed class StoppedTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
