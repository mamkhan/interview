namespace Microsoft.Extensions.Hosting;

// Azurite serves only older blob API versions than the SDK default, so a local run and the
// emulator-based tests pin one and every request 400s with InvalidHeaderValue without it; unset
// against real Azure, where the SDK default applies. One key rather than one per consumer: the pin
// describes the storage endpoint they all talk to.
public static class BlobServiceVersion
{
    public const string ConfigurationKey = "Azure:Blob:ServiceVersion";
}
