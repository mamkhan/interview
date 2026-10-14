using Xunit;

namespace BuildingBlocks.TestsShared;

// One test class's database on the shared stack, built and migrated once for the class. A test class takes it as a
// class fixture: xUnit creates the test class anew for every fact, so a scope built there would drop, recreate and
// migrate the database before each one.
public abstract class ClassDatabase(string suite, string testClass, Func<string, Task> migrate) : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var scope = await SuiteScope.CreateAsync(suite, testClass, new SuiteResources { Migrate = migrate },
            TestContext.Current.CancellationToken);
        ConnectionString = scope.SqlConnectionString!;
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
