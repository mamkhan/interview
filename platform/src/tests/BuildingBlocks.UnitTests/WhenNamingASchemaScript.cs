using AwesomeAssertions;
using BuildingBlocks.Persistence;

namespace BuildingBlocks.UnitTests;

// Every module's scripts share one journal with the platform's in the domain's database, so each file
// carries its module's prefix. A script without it is refused before the migrator reaches a database;
// the connection string names a server that does not exist.
public class WhenNamingASchemaScript
{
    private const string NoDatabase = "Server=tcp:unreachable.invalid,1433;Database=none;Connect Timeout=1";

    [Fact]
    public void RefusesAScriptThatDoesNotCarryTheModulePrefix()
    {
        var act = () => SchemaMigrator.Pending(NoDatabase, typeof(WhenNamingASchemaScript).Assembly, "Expected");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Oth-Other-00001 - Another module.sql*");
    }

    [Fact]
    public void RefusesAScriptOfAnotherDatabaseTheAssemblyDoesNotDeclare()
    {
        var act = () => SchemaMigrator.Pending(NoDatabase, typeof(WhenNamingASchemaScript).Assembly, "Oth-Other");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Oth-Second-00001 - Another database.sql*");
    }
}
