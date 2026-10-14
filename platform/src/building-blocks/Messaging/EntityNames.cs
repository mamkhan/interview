using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging;

/// <summary>Resolves a logical bus or storage entity name to the one addressed at runtime. The prefix is
/// empty everywhere but a test scope, which uses it to give each test class its own entities.</summary>
public sealed class EntityNames(IOptions<EntityNamingOptions> options)
{
    public string Resolve(string logicalName) => options.Value.Prefix + logicalName;
}

public sealed class EntityNamingOptions
{
    public const string Section = "Platform:Entities";

    public string Prefix { get; set; } = "";
}
