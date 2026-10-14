using BuildingBlocks.Persistence;

namespace BuildingBlocks.IntegrationTests;

// These suites stand in for a module: the platform tables come from BuildingBlocks.Persistence and the
// one table a test handler writes to comes from this assembly's own script, through the same migrator a
// host runs. No test-only description of the platform tables exists, so a suite cannot pass against a
// schema a host will not have. The Sibling scripts stand in for a second database the same module owns.
public static class PlatformSchema
{
    public const string ScriptPrefix = "Suite";

    public const string SiblingPrefix = "Sibling";

    public static Task CreateAsync(string connectionString)
    {
        SchemaMigrator.Apply(connectionString, typeof(PlatformSchema).Assembly, ScriptPrefix, SiblingPrefix);
        return Task.CompletedTask;
    }

    public static Task CreateSiblingAsync(string connectionString)
    {
        SchemaMigrator.Apply(connectionString, typeof(PlatformSchema).Assembly, SiblingPrefix, ScriptPrefix);
        return Task.CompletedTask;
    }
}
