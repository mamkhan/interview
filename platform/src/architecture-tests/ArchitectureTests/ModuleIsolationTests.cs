using System.Xml.Linq;
using AwesomeAssertions;

namespace ArchitectureTests;

// The domain isolation rules, enforced from the csproj files on disk so a violating reference fails
// the build even before the target project joins a solution.
public class ModuleIsolationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string DomainsRoot = Path.Combine(RepoRoot, "domains");
    private static readonly string BuildingBlocks = Path.Combine(RepoRoot, "platform", "src", "building-blocks");
    private static readonly string PlatformLocalGraph = Path.Combine(RepoRoot, "platform", "src", "LocalGraph");
    private static readonly string PlatformSrc = Path.Combine(RepoRoot, "platform", "src");
    private static readonly string PlatformContracts = Path.Combine(PlatformSrc, "Contracts");

    [Fact]
    public void ADomainProjectReferencesOnlyItsOwnDomainBuildingBlocksAndPublishedContracts()
    {
        foreach (var project in DomainProjects())
        {
            var domainRoot = DomainRootOf(project);
            var composesTheLocalGraph = IsUnder(project, Path.Combine(domainRoot, "src", "AppHost"))
                                        || IsUnder(project, Path.Combine(domainRoot, "src", "LocalGraph"));
            foreach (var reference in ReferencesOf(project))
            {
                var allowed = IsUnder(reference, domainRoot)
                              || IsUnder(reference, BuildingBlocks)
                              || IsContractsProject(reference)
                              || (composesTheLocalGraph && (IsUnder(reference, PlatformLocalGraph) || IsLocalGraphProject(reference)));
                allowed.Should().BeTrue(
                    $"{Relative(project)} references {Relative(reference)}, which is outside its domain, the building blocks, published contracts and, for a local graph, other domains' local graphs");
            }
        }
    }

    [Fact]
    public void AContractsProjectReferencesOnlyContracts()
    {
        foreach (var project in DomainProjects().Concat(ProjectsUnder(PlatformContracts)).Where(IsContractsProject))
        {
            foreach (var reference in ReferencesOf(project))
            {
                IsContractsProject(reference).Should().BeTrue($"{Relative(project)} references {Relative(reference)}");
            }
        }
    }

    [Fact]
    public void ABuildingBlockReferencesNoDomainAndNoContract()
    {
        foreach (var project in ProjectsUnder(BuildingBlocks))
        {
            foreach (var reference in ReferencesOf(project))
            {
                IsUnder(reference, BuildingBlocks).Should().BeTrue($"{Relative(project)} references {Relative(reference)}");
            }
        }
    }

    // In the one-project layout a module reaches another module only through the domain's
    // published contracts, never through the module project itself.
    [Fact]
    public void AModuleReferencesNoOtherModule()
    {
        foreach (var project in DomainProjects().Where(IsModuleProject))
        {
            var ownFolder = Path.GetDirectoryName(project)!;
            foreach (var reference in ReferencesOf(project).Where(IsModuleProject))
            {
                IsUnder(reference, ownFolder).Should().BeTrue(
                    $"{Relative(project)} references module {Relative(reference)}; modules share only Contracts");
            }
        }
    }

    private static bool IsModuleProject(string projectPath) =>
        IsUnder(projectPath, DomainsRoot)
        && IsUnder(projectPath, Path.Combine(DomainRootOf(projectPath), "src", "Modules"));

    // Which modules deploy together is a host's decision. A module project that is itself
    // runnable has made that decision on the host's behalf.
    [Fact]
    public void ARunnableProjectInADomainIsAHostAnAppHostOrATest()
    {
        foreach (var project in DomainProjects())
        {
            var sdk = XDocument.Load(project).Root?.Attribute("Sdk")?.Value ?? "";
            if (!sdk.StartsWith("Microsoft.NET.Sdk.Web", StringComparison.Ordinal)
                && !sdk.StartsWith("Microsoft.NET.Sdk.Worker", StringComparison.Ordinal))
            {
                continue;
            }

            var domainRoot = DomainRootOf(project);
            var src = Path.Combine(domainRoot, "src");
            var placed = IsUnder(project, Path.Combine(src, "Hosts"))
                         || IsUnder(project, Path.Combine(src, "AppHost"))
                         || IsUnder(project, Path.Combine(domainRoot, "tests"))
                         || Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(project))!) == "tests";
            placed.Should().BeTrue($"{Relative(project)} is a runnable project outside src/Hosts/");
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AcmePlatform.slnx"))
                && File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found above the test assembly.");
    }

    private static IEnumerable<string> DomainProjects() => ProjectsUnder(DomainsRoot);

    private static IEnumerable<string> ProjectsUnder(string root) =>
        Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static IEnumerable<string> ReferencesOf(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(r => r.Attribute("Include")!.Value)
            .Select(include => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!,
                include.Replace('\\', Path.DirectorySeparatorChar))));

    private static string DomainRootOf(string projectPath)
    {
        var domain = Path.GetRelativePath(DomainsRoot, projectPath).Split(Path.DirectorySeparatorChar)[0];
        return Path.Combine(DomainsRoot, domain);
    }

    // Contracts are the one thing a domain publishes for others to reference, domains/<d>/src/Contracts/, and the
    // platform's services publish theirs in platform/src/Contracts/.
    private static bool IsContractsProject(string projectPath) =>
        IsUnder(projectPath, PlatformContracts)
        || (IsUnder(projectPath, DomainsRoot)
            && IsUnder(projectPath, Path.Combine(DomainRootOf(projectPath), "src", "Contracts")));

    // A consumer's local graph takes the publisher's fixtures to subscribe to its topics: domains/<d>/src/LocalGraph/.
    private static bool IsLocalGraphProject(string projectPath) =>
        IsUnder(projectPath, DomainsRoot)
        && IsUnder(projectPath, Path.Combine(DomainRootOf(projectPath), "src", "LocalGraph"));

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string Relative(string path) => Path.GetRelativePath(RepoRoot, path);
}
