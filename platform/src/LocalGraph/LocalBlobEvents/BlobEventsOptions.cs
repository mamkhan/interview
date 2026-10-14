using System.ComponentModel.DataAnnotations;

namespace LocalBlobEvents;

public sealed class BlobEventsOptions
{
    public const string Section = "BlobEvents";

    [Required]
    public string Container { get; set; } = "";

    [Required]
    public string Queue { get; set; } = "";
}
