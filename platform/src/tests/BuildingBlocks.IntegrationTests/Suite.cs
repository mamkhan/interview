using BuildingBlocks.Messaging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.IntegrationTests;

// This project's naming root on the shared stack and the logical bus names its classes share; the
// same logical topic in two classes is deliberate, so the scope prefix is what keeps them apart.
internal static class Suite
{
    public const string Id = "bb";

    public const string EventsTopic = "events";

    public const string TestReader = "test-reader";

    // These tests hand the publisher an already-scoped topic, so no prefix is applied on top.
    public static EntityNames UnprefixedNames => new(Options.Create(new EntityNamingOptions()));
}
