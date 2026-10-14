using AwesomeAssertions;
using Mono.Cecil;
using NetArchTest.Rules;

namespace ArchitectureTests;

// The module layout on the compiled types: Domain (pure rules) depends on no feature, no infrastructure and
// no vendor; no feature slice depends on another, so slices meet only through the domain's events; and SQL is
// touched only by a repository. What a module names beyond these folders is its own business.
public class LayeringTests
{
    private static readonly string[] VendorNamespaces =
    [
        "Dapper",
        "Azure",
        "Microsoft.Data.SqlClient",
        "DbUp",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Hosting",
    ];

    private static readonly string[] SqlNamespaces = ["Dapper", "Microsoft.Data.SqlClient", "DbUp"];

    // The anchor is only read for its assembly, so it is a type the module will not move: its topology
    // rather than a registration entry point a host may stop calling.
    public static TheoryData<string, Type> Modules => new()
    {
        { "MeteringConsumption.MarketMessaging", typeof(MeteringConsumption.MarketMessaging.MarketMessagingTopology) },
        { "MeteringConsumption.Consumption", typeof(MeteringConsumption.Consumption.ConsumptionTopology) },
    };

    public static TheoryData<string, Type, string[]> ModuleSlices => new()
    {
        {
            "MeteringConsumption.MarketMessaging",
            typeof(MeteringConsumption.MarketMessaging.MarketMessagingTopology),
            ["Application.Features.Receive", "Application.Features.Interpret", "Application.Features.Audit"]
        },
        {
            "MeteringConsumption.Consumption",
            typeof(MeteringConsumption.Consumption.ConsumptionTopology),
            ["Application.Features.Record", "Application.Features.Profile", "Application.Features.Forecast"]
        },
        {
            "MeteringConsumption.SupplyPoints",
            typeof(MeteringConsumption.SupplyPoints.SupplyPointTopology),
            ["Application.Features.Store", "Application.Features.Reload", "Application.Features.Provide"]
        },
    };

    [Theory]
    [MemberData(nameof(ModuleSlices))]
    public void TheDomainDependsOnNothingOutward(string root, Type anyTypeInTheModule, string[] slices)
    {
        var result = Types.InAssembly(anyTypeInTheModule.Assembly)
            .That().ResideInNamespaceStartingWith($"{root}.Domain")
            .ShouldNot().HaveDependencyOnAny([.. slices.Select(s => $"{root}.{s}"), $"{root}.Infrastructure", .. VendorNamespaces])
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Theory]
    [MemberData(nameof(ModuleSlices))]
    public void NoSliceDependsOnAnotherSlice(string root, Type anyTypeInTheModule, string[] slices)
    {
        foreach (var slice in slices)
        {
            var result = Types.InAssembly(anyTypeInTheModule.Assembly)
                .That().ResideInNamespaceStartingWith($"{root}.{slice}")
                .ShouldNot().HaveDependencyOnAny([.. slices.Where(s => s != slice).Select(s => $"{root}.{s}")])
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{slice} {Because(result)}");
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void AModuleTouchesSqlOnlyInARepository(string root, Type anyTypeInTheModule)
    {
        var result = Types.InAssembly(anyTypeInTheModule.Assembly)
            .That().ResideInNamespaceStartingWith(root)
            .And().MeetCustomRule(new IsNotARepository())
            .ShouldNot().HaveDependencyOnAny(SqlNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    internal static string Because(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful ? "" : "these types cross the boundary: " + string.Join(", ", result.FailingTypeNames ?? []);

    // A repository's async methods compile to nested state-machine types, so the enclosing types count too.
    private sealed class IsNotARepository : ICustomRule
    {
        public bool MeetsRule(TypeDefinition type)
        {
            for (var current = type; current is not null; current = current.DeclaringType)
            {
                if (current.Name.EndsWith("Repository", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
