using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.LocalGraph;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using Platform.LocalGraph;

namespace MeteringConsumption.AcceptanceTests;

// The running environment the suite drives. It never starts one: a developer's AppHost when nothing is
// named, otherwise the deployed environment ACCEPTANCE_ENVIRONMENT names. A deployed environment's stores
// are found by the names Terragrunt composes; its hosts are passed in, because a Container App's address
// carries a segment Azure chooses and an environment may sit behind its own domain.
public sealed record TargetEnvironment
{
    private const string EnvironmentVariable = "ACCEPTANCE_ENVIRONMENT";
    private const string Local = "local";
    private static readonly string[] DeployedEnvironments = ["dev", "tst", "prd"];

    private const string DomainCode = "metr";
    private const string MarketMsgService = "marketmsg";
    private const string ReceiverService = "receiver";

    private const string PlatformCode = "plat";

    // The Service Bus emulator's published development key; a deployed namespace allows no key at all.
    private const string EmulatorKey = "SAS_KEY_VALUE";

    private const string AzuriteAccount = "devstoreaccount1";
    private const string AzuriteKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    public required string SqlConnectionString { get; init; }

    // The market reference data Forecast reads sits in a database of its own on the same server.
    public required string ReferenceDataConnectionString { get; init; }

    public required Uri MarketMsgUrl { get; init; }

    public required Uri ReceiverUrl { get; init; }

    // Azurite is reached with its published account key; a deployed account as the signed-in principal.
    private string? BlobConnectionString { get; init; }

    private Uri? BlobEndpoint { get; init; }

    private Uri? LedgerEndpoint { get; init; }

    // The emulator is reached with its published key; a deployed namespace as the signed-in principal.
    private string? ServiceBusConnectionString { get; init; }

    private string? ServiceBusNamespace { get; init; }

    public static TargetEnvironment Discover() => Resolve(Environment.GetEnvironmentVariable);

    public static TargetEnvironment Resolve(Func<string, string?> variable)
    {
        var environment = variable(EnvironmentVariable) is { Length: > 0 } named ? named : Local;
        if (environment == Local)
        {
            return LocalAppHost(variable);
        }

        return DeployedEnvironments.Contains(environment)
            ? Deployed(environment, variable)
            : throw new InvalidOperationException(
                $"{EnvironmentVariable} is '{environment}'; it must be one of {Local}, {string.Join(", ", DeployedEnvironments)}.");
    }

    public BlobServiceClient Blobs() =>
        BlobConnectionString is not null
            ? new BlobServiceClient(BlobConnectionString)
            : new BlobServiceClient(BlobEndpoint, new DefaultAzureCredential());

    // Locally the ledger container sits on the same Azurite; deployed, on its own ledger account.
    public BlobServiceClient Ledger() =>
        BlobConnectionString is not null
            ? new BlobServiceClient(BlobConnectionString)
            : new BlobServiceClient(LedgerEndpoint, new DefaultAzureCredential());

    public ServiceBusClient ServiceBus() =>
        ServiceBusConnectionString is not null
            ? new ServiceBusClient(ServiceBusConnectionString)
            : new ServiceBusClient(ServiceBusNamespace, new DefaultAzureCredential());

    private static TargetEnvironment LocalAppHost(Func<string, string?> variable) => new()
    {
        SqlConnectionString =
            $"Server=127.0.0.1,{PlatformGraph.SqlPort};Initial Catalog={MarketMessagingSchema.ConnectionName};" +
            $"User ID=sa;Password={PlatformGraph.SqlPassword};TrustServerCertificate=True;",
        ReferenceDataConnectionString =
            $"Server=127.0.0.1,{PlatformGraph.SqlPort};Initial Catalog={ReferenceDataSchema.ConnectionName};" +
            $"User ID=sa;Password={PlatformGraph.SqlPassword};TrustServerCertificate=True;",
        BlobConnectionString =
            $"DefaultEndpointsProtocol=http;AccountName={AzuriteAccount};AccountKey={AzuriteKey};" +
            $"BlobEndpoint=http://127.0.0.1:{PlatformGraph.BlobPort}/{AzuriteAccount};",
        ServiceBusConnectionString =
            $"Endpoint=sb://127.0.0.1:{PlatformGraph.ServiceBusPort};SharedAccessKeyName=RootManageSharedAccessKey;" +
            $"SharedAccessKey={EmulatorKey};UseDevelopmentEmulator=true;",
        MarketMsgUrl = HostUrl(variable, MarketMsgService)
            ?? new Uri($"http://localhost:{MeteringConsumptionGraph.MeteringConsumptionHostPort}/"),
        ReceiverUrl = HostUrl(variable, ReceiverService)
            ?? new Uri($"http://localhost:{MeteringConsumptionGraph.ReceiverHostPort}/"),
    };

    private static TargetEnvironment Deployed(string environment, Func<string, string?> variable)
    {
        var prefix = $"acm{environment}{DomainCode}";
        return new TargetEnvironment
        {
            SqlConnectionString =
                $"Server=tcp:{prefix}sqlmain.database.windows.net,1433;Initial Catalog={MarketMessagingSchema.ConnectionName};" +
                "Authentication=Active Directory Default;Encrypt=True;",
            ReferenceDataConnectionString =
                $"Server=tcp:{prefix}sqlmain.database.windows.net,1433;Initial Catalog={ReferenceDataSchema.ConnectionName};" +
                "Authentication=Active Directory Default;Encrypt=True;",
            BlobEndpoint = new Uri($"https://{prefix}starchive.blob.core.windows.net/"),
            LedgerEndpoint = new Uri($"https://{prefix}stledger.blob.core.windows.net/"),
            ServiceBusNamespace = $"acm{environment}{PlatformCode}sbnsshared.servicebus.windows.net",
            MarketMsgUrl = HostUrl(variable, MarketMsgService) ?? throw Missing(prefix, MarketMsgService),
            ReceiverUrl = HostUrl(variable, ReceiverService) ?? throw Missing(prefix, ReceiverService),
        };
    }

    // Named by domain and service code, the segments of the Container App's own name, so a host another
    // module adds gets a variable that cannot clash with these.
    private static string HostVariable(string service) =>
        $"ACCEPTANCE_{DomainCode.ToUpperInvariant()}_{service.ToUpperInvariant()}_URL";

    private static Uri? HostUrl(Func<string, string?> variable, string service) =>
        variable(HostVariable(service)) is { Length: > 0 } url ? new Uri(url, UriKind.Absolute) : null;

    private static InvalidOperationException Missing(string prefix, string service) =>
        new($"{HostVariable(service)} is not set: it is the address of the {prefix}ca{service} Container App.");
}
