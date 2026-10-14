# Adding a module

A module is one area of responsibility inside a domain: one cohesive model, its authoritative state,
its contracts and its persistence boundary. It usually corresponds to one capability-map sub-domain
and sometimes to several. `domains/metering-consumption/src/Modules/MarketMessaging`
is the reference copy source. Paths are relative to the repository root, where every command runs.

## Before creating it: the readiness checklist

1. Write `Module.md`: purpose, owned facts, published contracts, non-goals, in at most fifteen lines with
   no type names. If another module already owns the same facts with the same meaning, add a slice there
   instead.
2. Agree the owned facts and whether each is entity state, process state or a derived view.
3. Define the contracts it publishes and one real success and failure example; list its dependencies.
4. Decide its tables (in the domain's database) and who runs its migrations (the domain pipeline).
5. Plan one useful vertical slice with its domain rules, not scaffolding for the whole roadmap.
6. Add only the roles a host needs; record telemetry, recovery and deployment constraints.

Done means another team can use its contract, explain its decisions and recover its failures without
editing its private implementation.

## The layout: slices plus the concepts they share

```
domains/<domain>/src/Modules/<Module>/
  <Domain>.<Module>.csproj      one project; the rules below are enforced by the architecture tests on namespaces
  Module.md                     at most fifteen lines: purpose, owned facts, published contracts, non-goals; no type names
  <Module>Topology.cs           the topics, subscriptions, queues and containers only this module uses; a
                                subscription is named after the event handler that reads it
  Domain/                       the rules every slice shares; no vendor SDK, no clock, no minted identity
  Application/Extensions/       ApplicationRegistration: AddBaseApplication() and one Add<Feature>Application() per role
  Application/Features/<Feature>/
                                one behaviour: at the top, one <Feature><Event>EventHandler (an
                                IEventHandler<TEvent>) per event; Abstraction/ for the interfaces it depends on;
                                DTO/ for its outcome types; operator-specific code in one sub-folder per market
  Common/                       parsers and payload schemas more than one slice reads (only when there are some)
  Infrastructure/Extensions/    InfrastructureRegistration: AddBaseInfrastructure(configuration) and one
                                Add<Feature>Infrastructure(configuration) per role
  Infrastructure/Consumers/     the triggers: one <Feature><Event>EventConsumer per handler
  Infrastructure/<Technology>/  the implementations of the slices' interfaces (BlobStorage/,
                                Persistence/Repositories/ with a <Name>Repository for SQL); Configuration/ for options
  Infrastructure/Schema/        Scripts/ (the module's schema, embedded SQL) and the policy for applying it
domains/<domain>/src/Contracts/<Domain>.Contracts/Events/   the domain's event types, the module's among them
domains/<domain>/tests/<Domain>.<Module>.UnitTests/          the unit tier: Domain/  <Feature>/  Composition/
domains/<domain>/tests/<Domain>.<Module>.IntegrationTests/   the container tier, one SuiteScope per class
domains/<domain>/tests/<Domain>.TestsShared/                 builders, fakes and real samples the test projects share
```

**Where an interface goes.** In the `Abstraction/` folder of the slice that needs it, with its
implementation under `Infrastructure/`. Two slices that need the same capability each declare their own
interface (`IReceiveArchiveBlobStore`, `IInterpretArchiveBlobStore`), and one implementation may serve both,
so no slice depends on another. An interface exists because a test stubs it or two production
implementations exist; otherwise it is a class.

**Namespaces follow the folders**: `<Domain>.<Module>.Domain`, `.Application.Features.<Feature>` and its
sub-folders, `.Infrastructure.Consumers`, `.Infrastructure.Schema`. Enforced on the compiled types: `Domain`
depends on no feature, no infrastructure and no vendor; no feature slice depends on another; only a
`...Repository` type touches SQL. List the module's features (`Application.Features.<Feature>`) in
`LayeringTests.ModuleSlices`. A build gate
bans the wall clock and `Guid.NewGuid()` in every module: inject `TimeProvider`, derive identities.

## Steps

1. Copy the layout above; rename; write `Module.md`.
2. Registration, one pair per role on `IServiceCollection`. `ApplicationRegistration.AddBaseApplication()`
   and `Add<Feature>Application()` register the role's scoped event handlers under their own types and what
   they compose. `InfrastructureRegistration.AddBaseInfrastructure(configuration)` registers
   `AddEventTopics(map => ...)` for the events the module publishes; `Add<Feature>Infrastructure(configuration)`
   registers the role's options (`ValidateDataAnnotations().ValidateOnStart()`), the implementations of its
   interfaces, and its hosted consumers (`EventConsumer<TEvent, THandler>` when two slices in one host
   subscribe to the same event). A role registers nothing another role needs, so a host that adds one pair
   gets only that role's consumers and dependencies (`Composition/` proves it). Aspire client integrations
   (`AddAzureServiceBusClient`, `AddAzureBlobServiceClient`) stay in the host. A slice with a SQL aggregate also registers `AddUnitOfWork(connectionString)` and
   `AddCommandRunner()` and runs its `ICommandHandler<,>` through the runner (§4.6 of
   `docs/repository-structure.md`); a slice whose writes carry their own natural key does not.
3. Tables: numbered SQL in `Infrastructure/Schema/Scripts/` named `<Domain>-<Module>-NNNNN - <description>.sql`,
   embedded by folder rather than file by file (`<EmbeddedResource Include="Infrastructure\Schema\Scripts\*.sql"/>`),
   so a new script cannot be left out. `SchemaMigrator` applies them with the module's prefix, runs the
   platform tables' script first and journals each script by its file name. A script that has run never
   changes, its file name included; the next change is the next script. Expose
   `Apply<Module>SchemaAsync(connectionString)` as the one way in, and `Apply<Module>MigrationsAsync()` for
   the host to call (Development applies, elsewhere refuse on an unapplied script). The domain's migrator
   host applies them in a deployed environment.
   Runtime reads and writes go through one Dapper repository: a connection per call unless a unit of work
   owns the transaction, identifiers bound as `varchar`, every temporal parameter typed through `SqlTemporal`.
4. Topics, queues and containers: the domain's contracts topology type for what other modules consume,
   `<Module>Topology` for the rest and for the module's subscriptions; then `<Domain>.LocalGraph`'s
   `Add<Domain>Fixtures`, then one file per entity under the domain's `infrastructure/base/variables/<type>/`,
   named for it. Production code resolves every name through
   `EntityNames`, so a feature test can prefix them per class.
5. Tests, one project per tier. `<Domain>.<Module>.UnitTests` is the unit tier: `Domain/` and `<Feature>/` with
   hand-written fakes (one test per outcome, `When<Scenario>` classes, PascalCase outcome names, frozen
   literals derived offline), plus `Composition/` for what each role registers.
   `<Domain>.<Module>.IntegrationTests` is the container tier on the shared stack, each class on its own
   `SuiteScope` built once in a class fixture, with one feature test per role that boots the base methods and
   the role's registration pair with in-memory
   configuration; it alone references `BuildingBlocks.TestsShared`, so a unit test cannot reach a
   container. The feature test drives each of the role's consumers once; per-type values are unit tests. The
   pipelines glob `tests/*.UnitTests` for the unit tier and `tests/*.IntegrationTests` for the container tier.
   Each feature the module adds gets an acceptance scenario from its real input (the `testing-strategy` skill).
6. Host: add the base methods and the role's `Add<Feature>Application()` and
   `Add<Feature>Infrastructure(builder.Configuration)` to an existing host under `domains/<domain>/src/Hosts/`,
   or create one there. A new host also needs a graph
   method in `<Domain>.LocalGraph` (with `WithHttpHealthCheck("/health")` and any configuration the role
   requires), a call in both the domain AppHost and `platform/src/AppHost`, a file each under the domain's
   `infrastructure/base/variables/container-apps/` and `managed-identities/`, and a deploy stage in the
   domain pipeline.
7. Add the project and both its test projects to `domains/<domain>/<Domain>.slnx` and `AcmePlatform.slnx`
   (a project missing from the domain solution breaks that pipeline's `--no-build` test loop). Both
   solutions build at zero warnings; `dotnet test platform/src/architecture-tests/ArchitectureTests` stays green.
