using System.ComponentModel.DataAnnotations;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Configuration;

// One operator per deployment: the container GoAnywhere writes to and the identity tags stamped on
// every receipt from it. A second operator is a second configuration, not a second component.
public sealed class DropOptions
{
    public const string Section = "Drop";

    [Required]
    public string Container { get; set; } = "";

    [Required]
    public string Market { get; set; } = "";

    [Required]
    public string Fuel { get; set; } = "";
}
