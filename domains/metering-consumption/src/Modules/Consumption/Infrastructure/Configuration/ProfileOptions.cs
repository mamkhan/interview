using System.ComponentModel.DataAnnotations;

namespace MeteringConsumption.Consumption.Infrastructure.Configuration;

// The V1-fed Profile consumers, tariff values and meter configuration, each take this many sessions. Replicas times
// their sessions, with the host's other handlers, is how many requests the database receives at once; the defaults
// keep the whole host within the 30 that dev's Basic tier accepts.
public sealed class ProfileOptions
{
    public const string Section = "Profile";

    [Range(1, 128)]
    public int MaxConcurrentSessions { get; set; } = 6;

    // The V1 reader sends one message per supply point, so nearly every session holds one message and the wait for a
    // next one is mostly wasted.
    [Range(typeof(TimeSpan), "00:00:00.050", "00:00:30")]
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMilliseconds(250);
}
