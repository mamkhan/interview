using System.Text.Json.Serialization;

namespace MeteringConsumption.MarketMessaging.Application.Features.Receive;

// The CloudEvents 1.0 body Event Grid delivers for Microsoft.Storage.BlobCreated, reduced to the fields
// this path reads. The id is the receipt id: Event Grid redelivers the same id, so a redelivery is the
// same receipt.
public sealed record BlobCreated
{
    private const string BlobsSegment = "/blobs/";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("time")]
    public required DateTimeOffset Time { get; init; }

    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    [JsonPropertyName("data")]
    public required BlobCreatedData Data { get; init; }

    // Subject is /blobServices/default/containers/{container}/blobs/{name}; the name may itself contain slashes.
    public string BlobName => Subject[(Subject.IndexOf(BlobsSegment, StringComparison.Ordinal) + BlobsSegment.Length)..];
}

public sealed record BlobCreatedData
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("api")]
    public required string Api { get; init; }
}
