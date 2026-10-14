using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Testcontainers.ServiceBus;

namespace BuildingBlocks.TestsShared;

// The one container stack shared by every integration suite: one SQL Server (which also backs the
// Service Bus emulator), one emulator and one Azurite, attached to through Testcontainers resource
// reuse and left running between runs; a class resets its own state on the way in (SuiteScope).
// The builder configuration must be byte-identical for every suite because the reuse hash derives
// from it: one fixed emulator config path, and SQL always joins the network even for suites that
// never touch the bus. Ports sit clear of the AppHost graph and of the metering rail's shared stack.
public static class SharedPlatformContainers
{
    // Bump on any change to the containers below (image, port, network, config), so a stack from an
    // older revision is dropped and rebuilt instead of colliding on its pinned name.
    private const string Revision = "v2";

    private const string MsSqlImage = "mcr.microsoft.com/mssql/server:2022-latest";
    private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.35.0";
    private const string ServiceBusImage = "mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.0";

    private const string SqlName = "acme-shared-sql";
    private const string ServiceBusName = "acme-shared-servicebus";
    private const string AzuriteName = "acme-shared-azurite";
    private const string NetworkName = "acme-shared-network";
    private const string RevisionLabel = "acme.revision";

    private const int SqlHostPort = 14500;
    private const int BlobHostPort = 12100;
    private const int QueueHostPort = 12101;
    private const int TableHostPort = 12102;
    private const int ServiceBusHostPort = 15872;
    private const int ServiceBusHttpHostPort = 15500;

    private static readonly bool DropFirst =
        Environment.GetEnvironmentVariable("ACME_DROP_CONTAINERS") is "1" or "true";

    private static readonly SemaphoreSlim InProcessGate = new(1, 1);

    private static string SharedDirectory => Path.Combine(Path.GetTempPath(), "acme-shared");

    private static string LockPath => Path.Combine(SharedDirectory, ".startup.lock");

    private static string EmulatorConfigPath => Path.Combine(SharedDirectory, "servicebus-emulator-config.json");

    private static MsSqlContainer? _sql;
    private static ServiceBusContainer? _serviceBus;
    private static AzuriteContainer? _azurite;

    public static async Task<SharedEndpoints> EnsureAsync(bool needsSql, bool needsServiceBus, bool needsAzurite)
    {
        var wantSql = needsSql || needsServiceBus;

        await InProcessGate.WaitAsync();
        try
        {
            if ((!wantSql || _sql is not null) && (!needsServiceBus || _serviceBus is not null)
                && (!needsAzurite || _azurite is not null))
            {
                return Endpoints();
            }

            Directory.CreateDirectory(SharedDirectory);
            using var fileLock = await AcquireCrossProcessLockAsync(TimeSpan.FromMinutes(10));

            if (DropFirst || await IsStaleAsync())
            {
                await DropAsync();
            }

            var network = new NetworkBuilder().WithName(NetworkName).WithReuse(true).Build();

            if (wantSql && _sql is null)
            {
                var sql = BuildSql(network);
                await sql.StartAsync();
                _sql = sql;
            }

            if (needsServiceBus && _serviceBus is null)
            {
                await WriteEmulatorConfigAsync();
                var serviceBus = BuildServiceBus(network, _sql!);
                await serviceBus.StartAsync();
                _serviceBus = serviceBus;
            }

            if (needsAzurite && _azurite is null)
            {
                var azurite = BuildAzurite();
                await azurite.StartAsync();
                _azurite = azurite;
            }

            return Endpoints();
        }
        finally
        {
            InProcessGate.Release();
        }
    }

    private static MsSqlContainer BuildSql(INetwork network) =>
        new MsSqlBuilder(MsSqlImage)
            .WithName(SqlName)
            .WithLabel(RevisionLabel, Revision)
            .WithNetwork(network)
            .WithNetworkAliases(ServiceBusBuilder.DatabaseNetworkAlias)
            .WithPortBinding(SqlHostPort, MsSqlBuilder.MsSqlPort)
            .WithReuse(true)
            .Build();

    private static ServiceBusContainer BuildServiceBus(INetwork network, MsSqlContainer sql) =>
        new ServiceBusBuilder(ServiceBusImage)
            .WithAcceptLicenseAgreement(true)
            .WithConfig(EmulatorConfigPath)
            .WithMsSqlContainer(network, sql, ServiceBusBuilder.DatabaseNetworkAlias)
            .WithName(ServiceBusName)
            .WithLabel(RevisionLabel, Revision)
            .WithPortBinding(ServiceBusHostPort, ServiceBusBuilder.ServiceBusPort)
            .WithPortBinding(ServiceBusHttpHostPort, ServiceBusBuilder.ServiceBusHttpPort)
            .WithReuse(true)
            .Build();

    private static AzuriteContainer BuildAzurite() =>
        new AzuriteBuilder(AzuriteImage)
            .WithName(AzuriteName)
            .WithLabel(RevisionLabel, Revision)
            .WithPortBinding(BlobHostPort, AzuriteBuilder.BlobPort)
            .WithPortBinding(QueueHostPort, AzuriteBuilder.QueuePort)
            .WithPortBinding(TableHostPort, AzuriteBuilder.TablePort)
            .WithReuse(true)
            .Build();

    private static SharedEndpoints Endpoints() => new(
        _sql?.GetConnectionString(),
        _serviceBus?.GetConnectionString(),
        _serviceBus is null
            ? null
            : $"Endpoint=sb://localhost:{ServiceBusHttpHostPort};SharedAccessKeyName=RootManageSharedAccessKey;"
              + "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        _azurite?.GetConnectionString());

    // The emulator provisions entities from this file at startup only, so the shared stack starts empty
    // and each class creates its own prefixed entities through the admin endpoint (SuiteScope).
    private static async Task WriteEmulatorConfigAsync()
    {
        const string config =
            """
            {
              "UserConfig": {
                "Namespaces": [
                  {
                    "Name": "sbemulatorns",
                    "Queues": [],
                    "Topics": []
                  }
                ],
                "Logging": {
                  "Type": "File"
                }
              }
            }
            """;

        if (File.Exists(EmulatorConfigPath)
            && string.Equals(await File.ReadAllTextAsync(EmulatorConfigPath), config, StringComparison.Ordinal))
        {
            return;
        }

        await File.WriteAllTextAsync(EmulatorConfigPath, config);
    }

    private static async Task<bool> IsStaleAsync()
    {
        var listing = await RunDockerAsync("ps", "-a", "--filter", $"label={RevisionLabel}",
            "--format", $"{{{{.Names}}}} {{{{.Label \"{RevisionLabel}\"}}}}");
        return listing.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', 2))
            .Any(parts => parts[0].StartsWith("acme-shared-", StringComparison.Ordinal)
                          && parts.Length == 2 && parts[1] != Revision);
    }

    private static async Task DropAsync()
    {
        await RunDockerAsync("rm", "-f", SqlName, ServiceBusName, AzuriteName);
        await RunDockerAsync("network", "rm", NetworkName);
        _sql = null;
        _serviceBus = null;
        _azurite = null;
    }

    private static async Task<string> RunDockerAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    // Reuse matches an existing container, but concurrent cold starts race to create the same pinned
    // name and all but one die on the name conflict; the lock is held across the readiness wait.
    private static async Task<FileStream> AcquireCrossProcessLockAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(250);
            }
        }
    }
}

// ServiceBusAdminConnectionString carries the emulator's HTTP port, which entity management needs and
// the AMQP connection string does not expose.
public sealed record SharedEndpoints(
    string? SqlMasterConnectionString,
    string? ServiceBusConnectionString,
    string? ServiceBusAdminConnectionString,
    string? AzuriteConnectionString);
