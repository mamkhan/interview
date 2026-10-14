using System.Reflection;
using System.Text.RegularExpressions;
using DbUp;
using DbUp.Engine;

namespace BuildingBlocks.Persistence;

/// <summary>Applies a module's schema to its database: the platform tables first, then the module's own
/// scripts, both journalled in one <c>SchemaVersions</c> table so every script runs once per database.
/// </summary>
public static class SchemaMigrator
{
    private const string PlatformScriptPrefix = "Platform";

    /// <summary>Runs every script the database has not seen. Throws on the first script that fails, and before
    /// connecting when a module script is named for neither <paramref name="scriptPrefix"/> nor one of
    /// <paramref name="otherDatabasePrefixes"/>, the other databases whose scripts the same assembly holds.</summary>
    public static void Apply(string connectionString, Assembly moduleScripts, string scriptPrefix,
        params string[] otherDatabasePrefixes)
    {
        foreach (var upgrader in Upgraders(connectionString, moduleScripts, scriptPrefix, otherDatabasePrefixes))
        {
            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
            {
                throw new InvalidOperationException(
                    $"Schema script '{result.ErrorScript?.Name ?? "unknown"}' failed.", result.Error);
            }
        }
    }

    /// <summary>The scripts this database has not seen, by file name, in the order they would run.</summary>
    public static IReadOnlyList<string> Pending(string connectionString, Assembly moduleScripts, string scriptPrefix,
        params string[] otherDatabasePrefixes) =>
        Upgraders(connectionString, moduleScripts, scriptPrefix, otherDatabasePrefixes)
            .SelectMany(upgrader => upgrader.GetScriptsToExecute())
            .Select(script => script.Name)
            .ToArray();

    // Two upgraders rather than one with two script sources: the platform tables have to exist before a
    // module script can reference them, and call order is a surer sequence than sorting names across two
    // assemblies. Both script sets are read and checked before either engine touches the database, so a
    // misnamed script refuses the host rather than leaving a schema half applied.
    private static UpgradeEngine[] Upgraders(string connectionString, Assembly moduleScripts, string scriptPrefix,
        string[] otherDatabasePrefixes)
    {
        var platform = ScriptsIn(typeof(SchemaMigrator).Assembly, PlatformScriptPrefix, []);
        var module = ScriptsIn(moduleScripts, scriptPrefix, otherDatabasePrefixes);
        return [Upgrader(connectionString, platform), Upgrader(connectionString, module)];
    }

    // DbUp decides a script has run by its journal name alone, and an embedded script's own name is its
    // resource path, folders included. Journalling the file name instead keeps a moved script the script
    // that ran; the prefix every file carries keeps one module's scripts apart from another's in the one
    // journal a domain's database shares, and a second database's scripts out of this one.
    private static SqlScript[] ScriptsIn(Assembly assembly, string prefix, string[] otherDatabasePrefixes)
    {
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var misnamed = resources
            .Where(name => !otherDatabasePrefixes.Prepend(prefix).Any(known => FileName(known).IsMatch(name)))
            .ToArray();
        if (misnamed.Length > 0)
        {
            throw new InvalidOperationException(
                $"Schema scripts in {assembly.GetName().Name} must be named {prefix}-NNNNN - <description>.sql: {string.Join(", ", misnamed)}.");
        }

        var fileName = FileName(prefix);
        return [.. resources.Where(name => fileName.IsMatch(name)).Select(name =>
        {
            using var content = assembly.GetManifestResourceStream(name)!;
            return SqlScript.FromStream(fileName.Match(name).Groups["file"].Value, content);
        })];
    }

    private static Regex FileName(string prefix) =>
        new($@"\.(?<file>{Regex.Escape(prefix)}-\d{{5}} - [^\\/]+\.sql)$", RegexOptions.CultureInvariant);

    private static UpgradeEngine Upgrader(string connectionString, IEnumerable<SqlScript> scripts) =>
        DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScripts(scripts)
            .JournalToSqlTable("dbo", "SchemaVersions")
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();
}
