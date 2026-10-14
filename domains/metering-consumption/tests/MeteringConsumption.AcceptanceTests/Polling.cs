namespace MeteringConsumption.AcceptanceTests;

// Every hop in a running environment is asynchronous (an Event Grid delivery, a host scaling up, a session handed
// over), so a scenario reads the state it waits for until it is there, and gives up loudly naming what is missing.
internal static class Polling
{
    public static readonly TimeSpan Deadline = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan BetweenPolls = TimeSpan.FromSeconds(5);

    public static async Task<T> UntilAsync<T>(Func<CancellationToken, Task<T>> read, Func<T, bool> isThere,
        Func<T, string> whatIsMissing, CancellationToken ct)
    {
        var giveUpAfter = DateTimeOffset.UtcNow + Deadline;
        var last = await read(ct);
        while (!isThere(last))
        {
            if (DateTimeOffset.UtcNow >= giveUpAfter)
            {
                throw new InvalidOperationException($"After {Deadline}: {whatIsMissing(last)}");
            }

            await Task.Delay(BetweenPolls, ct);
            last = await read(ct);
        }

        return last;
    }
}
