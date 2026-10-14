# Acme Platform: Repository Structure

Where everything lives and why. The decision is ADR-010; this page is the working reference.
`docs/architecture.md` is the map of what the code does.

## 1. The three goals the layout serves

The platform is a modular monolith on the six Acme business domains (the capability map).
The layout delivers three things at once, and every rule below traces to one of them:

1. **Domain isolation.** A domain team works in one folder and touches nothing outside it. Its code,
   tests, infrastructure, pipeline, solution and local run all live under `domains/<domain>/`. It
   shares only the platform building blocks and the contracts other domains publish. Architecture tests
   fail the build when a project reference crosses the line.
2. **A modular monolith with deployment as a domain choice.** Inside a domain, modules are libraries
   that compose into hosts. A host names the module roles it runs; which modules and roles run in which
   process is decided in the host, never in the module (ADR-002).
3. **Code that reads like the business map.** A module's name is the capability-map sub-domain it
   implements (or the platform owner's proposed home for it); inside the module, a slice folder is one
   behaviour, holding everything it needs.

### 1.1 Vocabulary

The word *domain* is used two ways in this repository, and the difference matters. The platform owner
and the capability map use it for a **business domain**: one of six areas of the business with a mission
and a team. Domain-Driven Design uses it for the whole problem space, and *domain model* or *domain
layer* for the code that holds one bounded context's rules. Both survive here; prose says "business
domain" for the six and the module's `Domain/` folder is always the domain model of that module.

| Term | Who uses it | What it means here | Where it lives |
| --- | --- | --- | --- |
| Business domain (6) | Platform owner, capability map | A business area with a mission and a team: Growth, Engage, Metering & Consumption, Billing & Pay, Customer Experience, Platform & Data Insights. A portfolio, not a bounded context. | `domains/<domain>/`: one folder, one solution, one pipeline, one infrastructure stack, one database, one Contracts project (two for Metering & Consumption, 4.3), its hosts and modules |
| Sub-domain | Capability map | A focused area of business knowledge inside a business domain (Rating & Charging, Read Acquisition). | Not a folder. Each sub-domain has a module home; several sub-domains may share one module (the payment sub-domains are `billing-pay/Payments`) and one may span two |
| Capability | Capability map | An ability the business needs, in the business's words, with its state today ("Interval read ingestion (MM343)", Present). | Not code. Implemented by one or more features |
| Bounded context | DDD | A boundary inside which one model and one language hold. The analysis names candidate contexts; the modules we build are the bounded contexts we chose, often consolidating several (the three Market Messaging contexts are one module). | The capability map links each capability to a context |
| Module | Platform owner's guide, ADR-010 | The bounded context as built: one cohesive model, its authoritative state, its contracts, its persistence boundary, its roles. One project. | `domains/<domain>/src/Modules/<Module>/` |
| Aggregate | DDD | The consistency boundary inside a module: one transaction changes one aggregate and its invariants hold at commit. | `Modules/<Module>/Domain/<Aggregate>.cs` |
| Feature, use case, slice | Platform owner's guide, vertical-slice architecture | One behaviour with one owning module: a trigger, a handler, an outcome, and everything it needs. A capability may need several; one slice may serve one capability. | `Modules/<Module>/Application/Features/<Feature>/` (`Receive/`, `Interpret/`, `Audit/`); its consumer and implementations under `Infrastructure/` |
| Domain event | DDD | A fact an aggregate raises, handled in-process inside the same unit of work, never leaving the module. | `Domain/<Fact>.cs` implementing `IDomainEvent`; handlers inside the module's slices (§4.6) |
| Contract event (integration event) | ADR-004; "integration event" in the platform owner's guide | A fact published for other modules and other business domains, versioned, delivered at least once, consumed under eventual consistency. | `domains/<domain>/src/Contracts/`, its topic in the topology type, written through the outbox (§4.6) |
| Role | Platform owner's guide | The part of a module a host runs: one feature (`Receive`, `Interpret`, `Audit`). | `Add<Feature>Application()` in `Application/Extensions/` and `Add<Feature>Infrastructure(configuration)` in `Infrastructure/Extensions/` |
| Host | ADR-002 | A deployable unit that names the module roles it runs. | `domains/<domain>/src/Hosts/<Name>/` |
| Shared concept | This repository | What several slices of one module share, under the business name for it. | `Domain/`, `Common/`, `Infrastructure/` inside the module; the domain's events in `Contracts/` |
| Building block | ADR-008 | Technical mechanics with no business vocabulary: messaging, outbox, seedwork, service defaults. | `platform/src/building-blocks/` |

The two hierarchies are read side by side, not nested into one: business domain → sub-domain →
capability is the business view; domain folder → module → feature is the software view. The join is
sub-domain to module, many-to-many by design. A business domain or sub-domain is not automatically a project, schema or
service (platform owner's guide, §03).

## 2. Top level

```
acme-platform/
├── AcmePlatform.slnx            every project; what the platform pipeline builds
├── Directory.Build.props        one build baseline for every project (ADR-008)
├── Directory.Packages.props     central package versions (ADR-008)
├── global.json                  the pinned SDK; `dotnet` works from any folder in the repository
├── nuget.config  .config/  .editorconfig
├── CLAUDE.md                    agent guidance
├── docs/
│   ├── architecture.md          the map of what the code does
│   ├── repository-structure.md  this page
│   └── adr/
├── tools/                       new-module.md, new-domain.md
├── platform/                    what Platform Engineering owns and every domain consumes
└── domains/                     one folder per business domain
    └── metering-consumption/    the first inhabitant; the others are created when their first module lands
```

## 3. `platform/`: shared assets

```
platform/
├── pipelines/
│   ├── platform.yml               builds the root solution, runs every tier, applies the platform stack
│   └── templates/                 test-jobs.yml (the ADR-007 tiers), iac-steps.yml
├── infrastructure/
│   ├── root.hcl  common.hcl  resources.hcl  layer.hcl  bootstrap/  _modules/
│   ├── units/<type>/              one unit per shared resource type; base/variables/<type>/ holds its instances
│   └── dev/ tst/ prd/             env.hcl and variables/<type>/ overrides
└── src/
    ├── building-blocks/           Seedwork, Messaging, Outbox, Sql, ServiceDefaults, TestsShared
    ├── Contracts/                 Platform.Contracts: the events no domain owns, on plat topics (the legacy V1 reader, not in this repository, publishes them)
    ├── LocalGraph/                Platform.LocalGraph: the shared local fixtures as Aspire extension methods
    ├── AppHost/                   Platform.AppHost: every domain running together
    ├── architecture-tests/        the isolation and layering rules, run by every pipeline
    └── tests/
        ├── BuildingBlocks.UnitTests/         the building blocks whose behaviour needs no store
        └── BuildingBlocks.IntegrationTests/  the building blocks on the shared container stack
```

Building blocks reference nothing outside `building-blocks/` and carry no domain vocabulary.

## 4. `domains/<domain>/`: one business domain

```
domains/metering-consumption/
├── MeteringConsumption.slnx         the domain plus the building blocks; the team's daily solution
├── pipelines/metering-consumption.yml
├── infrastructure/                  layer.hcl, sql/, units/<type>/ (one unit per resource type), base/variables/<type>/
│                                    (its instances), dev/ tst/ prd/ (env.hcl and variables/<type>/ overrides)
├── tools/                           recipes and scripts that belong to this domain only
├── src/
│   ├── AppHost/                     MeteringConsumption.AppHost: this domain alone, under `aspire run`
│   ├── LocalGraph/                  MeteringConsumption.LocalGraph: the domain's graph as extension methods
│   ├── Contracts/                   MeteringConsumption.Contracts: the domain's events (Events/) and the names other modules need
│   │                                MeteringConsumption.Contracts.Ledger: the consumption ledger's file format (4.3)
│   ├── Modules/
│   │   ├── MarketMessaging/         one project; see 4.1
│   │   ├── Consumption/             one project: the supply point profile (Profile), the consumption ledger (Record)
│   │   └── SupplyPoints/            one project: the supply point's dated meter point and usage factor history (Store)
│   └── Hosts/                       MarketMessaging.Receiver, MeteringConsumption.Host, MeteringConsumption.Migrator
└── tests/
    ├── MeteringConsumption.MarketMessaging.UnitTests/          the unit tier: Domain/ Receive/ Interpret/ Composition/
    ├── MeteringConsumption.MarketMessaging.IntegrationTests/   the container tier, one SuiteScope per class
    ├── MeteringConsumption.Consumption.UnitTests/              the unit tier: Domain/ Profile/ Record/ Composition/
    ├── MeteringConsumption.Consumption.IntegrationTests/       the container tier, one SuiteScope per class
    ├── MeteringConsumption.SupplyPoints.UnitTests/             the unit tier: Domain/ Store/ Composition/
    ├── MeteringConsumption.SupplyPoints.IntegrationTests/      the container tier, one SuiteScope per class
    ├── MeteringConsumption.AcceptanceTests/                    the acceptance tier: every feature end to end on a running environment
    └── MeteringConsumption.TestsShared/                        builders, fakes and the real extract the test projects share
```

### 4.1 A module

A module is one area of responsibility: one cohesive model, its authoritative state, its contracts and
its persistence boundary. It is one project: `Domain/` holds the rules, `Application/Features/` one folder
per feature slice holding its handlers, the interfaces it depends on and its outcome types, and
`Infrastructure/` the adapters: the implementations of those interfaces, the Service Bus consumers, the
options and the schema. The features are the module's capabilities: Receive, Interpret (which includes
accepting a message) and Audit, inbound; outbound is later and out of scope.

```
Modules/MarketMessaging/
├── MeteringConsumption.MarketMessaging.csproj
├── Module.md                        at most fifteen lines: purpose, owned facts, published contracts, non-goals; no type names
├── MarketMessagingTopology.cs       the topics, subscriptions, queue and containers only this module uses
├── Domain/                          pure rules: the sealed file's content address, identity tags, the interval series, the deterministic exceptions
├── Application/
│   ├── Extensions/                  ApplicationRegistration: AddBaseApplication() and one Add<Feature>Application() per role
│   └── Features/
│       ├── Receive/                 seals a dropped file in the archive and announces the receipt: the event handler; Abstraction/ holds the blob store interfaces it depends on
│       ├── Interpret/               classifies, dispatches and announces the outcome: the event handler; Abstraction/, DTO/, Classifiers/<market>/, MessageStrategies/<market>/ and Xml/
│       └── Audit/                   records receipts and outcomes: three event handlers and the recorder; Abstraction/ holds the repository interface
├── Common/
│   └── ESBMessage/                  the ESB message parser and its schema-shaped types, for the strategies that read ESB payloads
└── Infrastructure/
    ├── Extensions/                  InfrastructureRegistration: AddBaseInfrastructure(configuration) and one Add<Feature>Infrastructure(configuration) per role
    ├── Consumers/                   one <Feature><Event>EventConsumer per handler: the Service Bus adapters
    ├── BlobStorage/                 the Azure implementations of the slices' blob store interfaces
    ├── Persistence/Repositories/    the Dapper repository
    ├── Configuration/               the options the roles bind and validate on start
    └── Schema/                      the module's table: Scripts/ (numbered, embedded) and the policy for applying them
```

**A slice holds its behaviour; `Infrastructure/` holds its adapters.** The event handlers sit at the top of
the slice folder; the interfaces they depend on sit in `Abstraction/` and the outcome types in `DTO/`. The
trigger that invokes a handler (a consumer, an endpoint, a timer) and the implementations of its interfaces
live under `Infrastructure/`, registered by that feature's `Add<Feature>Infrastructure`. Each is named for its
role: `<Feature><Event>EventHandler` reacts to one event, `<Feature><Event>EventConsumer` feeds it,
`...Repository` stores.

**What several slices share sits beside `Application/`.** `Domain/` is the rules; `Common/` the parsers and
payload schemas more than one slice reads; `Infrastructure/Schema/` the module's tables and their scripts;
the events the slices tell each other are the domain's, in `Contracts/<Domain>.Contracts/Events/`. The
operator-specific code sits in one sub-folder per market inside the slice that uses it, so a new operator is
a new sub-folder and a new configuration, never a change to the market-agnostic path.

**Namespaces follow the folders**: `.Domain`, `.Application.Features.Receive`,
`.Application.Features.Receive.Abstraction`, `.Application.Features.Interpret.Classifiers.RoiEsb`,
`.Infrastructure.Consumers`, `.Infrastructure.Schema`. `LayeringTests` checks on the compiled types that
`.Domain` depends on no feature, no infrastructure and no vendor, that no feature slice depends on another,
and that only a `...Repository` type touches SQL. A new slice is listed in its `ModuleSlices` as
`Application.Features.<Feature>`, or the slice rule does not see it.

**An interface exists because a test stubs it or two implementations exist.** A port asks for what its
caller needs (`IDropBlobStore` opens and deletes; `IInterpretArchiveBlobStore` opens by path), and a handler that
needs one fact takes an interface with one method. The repository has one Dapper implementation, and the
module's SQL scripts are the only description of its tables.

**The schema is the module's.** Its table and its scripts live under `Infrastructure/Schema/` in the
domain's one database; `Apply<Module>MigrationsAsync()` is what a host calls, and the domain's migrator host is what
the pipeline runs.

### 4.2 Roles and hosts

A role is one feature, registered in two halves: `Add<Feature>Application()` in
`Application/Extensions/ApplicationRegistration.cs` (its handlers and what they compose) and
`Add<Feature>Infrastructure(configuration)` in `Infrastructure/Extensions/InfrastructureRegistration.cs` (its
options, implementations and hosted consumers). `AddBaseApplication()` and `AddBaseInfrastructure(configuration)`
register what every role needs, the module's topic map among it. A host is a `Program.cs` that calls both
base methods and one pair per role it runs:

```csharp
builder.AddServiceDefaults();
builder.Services.TryAddTimeProvider();
builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs");

builder.Services.AddBaseApplication();
builder.Services.AddBaseInfrastructure(builder.Configuration);
builder.Services.AddInterpretApplication();
builder.Services.AddInterpretInfrastructure(builder.Configuration);
builder.Services.AddAuditApplication();
builder.Services.AddAuditInfrastructure(builder.Configuration);

var app = builder.Build();
await app.ApplyMarketMessagingMigrationsAsync();
app.MapDefaultEndpoints();
app.Run();
```

| Host | Runtime | Roles |
| --- | --- | --- |
| `MarketMessaging.Receiver` | Azure Container App | `MarketMessaging.Receive` |
| `MeteringConsumption.Host` | Azure Container App | `MarketMessaging.Interpret`, `MarketMessaging.Audit`, `Consumption.Profile` |

A host gets exactly the consumers of the roles it adds, and only Audit registers the repository
(`WhenComposingAHost`). Running `Receive` in the same process as the other two is a `Program.cs` change; no
module changes. Every module names its base methods `AddBaseApplication` and `AddBaseInfrastructure`, so a host
that runs two modules calls each module's through its registration class, named by a using alias.

### 4.3 Contracts

`src/Contracts/<Domain>.Contracts` is the one thing a domain publishes: its event types in `Events/`
(those other modules consume, and those one module's slices pass to each other on the bus) and the
topology constants another module needs (topic and property names), nothing with behaviour. The topics,
subscriptions, queues and containers only the owning module uses stay in the module's `<Module>Topology`.
Metering & Consumption also publishes its consumption ledger's file format, which the legacy V1 platform's reader (not in this repository) also writes, in
`MeteringConsumption.Contracts.Ledger`: the row type, the file names and the Parquet options. It is the one contract
with behaviour, because the format is the contract, and it sits apart so only the ledger's writers and readers
carry Parquet.
A consuming domain references the contracts across the `domains/` boundary; the architecture tests allow exactly that and nothing else
across domains. A module reaches another module only through contracts. A platform service publishes its events in
`platform/src/Contracts/Platform.Contracts` (`SupplyPointLoadedFromV1`, the V1 reader's), on `plat` topics, and a
domain references that project as it references another domain's contracts.

### 4.4 Local run

Two AppHosts run the same code: `domains/<domain>/src/AppHost/` (this domain alone) and
`platform/src/AppHost/` (every domain). Neither contains wiring: a domain's graph is
`<Domain>.LocalGraph`, extension methods on `IDistributedApplicationBuilder`
(`AddMeteringConsumptionFixtures`, `AddReceiverHost<TProject>`, `AddMeteringConsumptionHost<TProject>`,
`AddEsbDropBlobEvents<TProject>`), which both AppHosts call. The `Projects.*` types are generated per
AppHost, hence the generic parameter. `LocalBlobEvents` (`platform/src/LocalGraph/`) stands in for Event
Grid locally by announcing each new blob in the drop container on the queue.

### 4.5 Tests

One test project per module per tier under `domains/<domain>/tests/`, mirroring the module.
`<Domain>.<Module>.UnitTests` is the unit tier: `Domain/` and one folder per feature slice (`Receive/`,
`Interpret/`) with hand-written fakes, plus `Composition/`, which proves what each role's registration
pair registers. `<Domain>.<Module>.IntegrationTests` is the container tier on the shared stack
(`BuildingBlocks.TestsShared`: SQL Server, the Service Bus emulator and Azurite, each test class on its
own `SuiteScope` of database, prefixed topics, queues and blob containers). The tiers are separate
projects so the unit tier holds no reference to the container fixtures and cannot reach a container;
the pipelines select the tier by project name, not by namespace. Builders and fakes the domain's test
projects share live in `<Domain>.TestsShared`; the building blocks' own tiers live in
`platform/src/tests/BuildingBlocks.UnitTests` and `BuildingBlocks.IntegrationTests`.
Each role has one feature test that
boots the base methods and the role's registration pair on the class's scope, drives each of its consumers once
with the real input and reads the output off a test-reader subscription or the table. The acceptance suite drives
every feature end to end on a running environment with no configuration overrides: the local AppHost before every
push, dev after every deploy. The `testing-strategy` skill says which tier owns which behaviour.

### 4.6 Commands: the runner, the unit of work, idempotency

`CommandRunner` (`BuildingBlocks.Commands`) and `IUnitOfWork` (`BuildingBlocks.Persistence`) are the wiring
for a slice with a SQL aggregate: validate the command, begin the scoped unit of work, guard idempotency
(the inbox for a consumer, the `x-requestid` client-request table for an HTTP command), run the
`ICommandHandler<TCommand, TOutcome>`, dispatch the tracked aggregates' domain events, commit; a failure
at any step commits nothing. The outbox (`BuildingBlocks.Outbox`) is the state-plus-publish path for such
a slice. Their behaviour is pinned in `platform/src/tests/BuildingBlocks.UnitTests` and `BuildingBlocks.IntegrationTests`.

**No module uses them yet.** The first SQL aggregate is the consumption ledger's `RecordConsumption`. Market Messaging needs none of it: its receipt insert is
idempotent by natural key (the receipt id), its outcome update is idempotent by construction, and its
readings are idempotent by message id on the bus. A slice whose writes carry their own natural key does
not take on a runner, a unit of work or an inbox to be idempotent.

Expected business results are typed outcomes; the two deterministic exceptions are
`CommandValidationException` (the shape is wrong) and `DomainException` (an invariant refuses).
`EventConsumer` dead-letters both on first delivery; the `PlatformExceptionHandler` in ServiceDefaults
answers 400 with the failures by property and 422 with the rule. A processing failure that is a business
outcome (an unclassifiable file, a payload the interpreter refuses) is published as `MarketMessageFailed` and
the message completes; the dead-letter queue holds transient exhaustion only. Anything else propagates:
the message is abandoned for redelivery.

The transport is separate from the reaction: a slice implements `IEventHandler<TEvent>`
(`BuildingBlocks.Messaging`); `EventConsumer<TEvent>` is the Service Bus adapter that opens a DI scope
per delivery, stamps the scoped clock from the event's `AsOf` when it carries one, and resolves the
handler (`EventConsumer<TEvent, THandler>` when several slices in one host subscribe to the same
event). Its subscription is named after the handler, feature then event
(`metr.audit-market-message-received` for `AuditMarketMessageReceivedEventHandler`), so the portal shows
which feature reads which event. Entity names are the topology constants resolved through
`EntityNames`, whose prefix is empty everywhere but a test scope.

### 4.7 Events inside a module and between modules

Two kinds of event, two consistency models, two homes. The rule is ADR-004 rule 2 and rule 5; this is
how it looks in the layout.

**A domain event stays inside the module and inside the transaction.** An aggregate in `Domain/` raises
it (`Raise(new MeterExchanged(...))`) as a plain C# record implementing `IDomainEvent`, past tense,
carrying identifiers and facts and no infrastructure type. The aggregate collects it; nothing publishes
it. When the slice's handler has finished changing state, the unit of work dequeues the events from every
aggregate it touched and dispatches them in-process to the module's `IDomainEventHandler<T>`
implementations **before commit**, looping until no handler raises a further event. A handler runs inside
the same transaction: it may change another aggregate of the same module, add an outbox row, or refuse by
throwing, in which case the whole unit of work rolls back and nothing is observed by anyone. A domain
event is therefore immediately consistent, never serialised, never on the bus, and never handled by
another module: a domain event type lives in `Domain/` and no other module can reference it (the module
isolation rule). Its handlers live in the slice that owns the reaction: in the raising use case's folder
when the reaction is part of that use case's promise (publishing the fact is part of the use case), in
its own feature folder when it is a capability of its own.

**A contract event leaves the module and is eventually consistent.** It is a versioned, serialisable type
in the business domain's `Contracts/` project with the platform envelope (event id, correlation, occurred
at, market and fuel tags), published on a topic the publishing domain owns. It is produced by a domain
event handler that translates the internal fact into the contract and adds the outbox row through the unit
of work, so the row commits with the state change; `OutboxRelay` publishes it after commit. A slice whose
writes carry their own natural key has no transaction to join and publishes directly instead: Record keeps
the ledger entry, then publishes `ConsumptionRecorded`, and a redelivery that finds the entry kept
announces it again under the same message id. Consumers, in another module of
the same business domain or in another business domain, receive it at least once, on their own unit of
work, idempotently (ADR-004 rule 3). Between business domains this is the only channel. Inside one
business domain it is the channel between modules (`IntervalReadingReceived` from MarketMessaging to the
consumption ledger).

**Inside one module, slices can also meet on the bus.** When a reaction inside one module must survive
a restart, retry on its own schedule or run at a different scale, the slices pass a contract event with a
consumer, not a domain event handler (`MarketMessageReceived` between the Receive, Audit and
Interpret features). The event type is declared with the domain's other events in `Contracts/.../Events/`;
its topic stays in the module's topology type while only that module reads it.

**One unit of work, one module.** A transaction commits one module's tables only. A slice never enlists
another module's tables or context, whatever the host it shares; the seam between modules is a contract
event or an explicitly permitted query, and the seam between business domains is a contract event alone.

| | Domain event | Contract event |
| --- | --- | --- |
| Declared in | `Modules/<M>/Domain/` | `domains/<domain>/src/Contracts/` |
| Raised by | an aggregate | a domain event handler (through the outbox) |
| Handled by | `IDomainEventHandler<T>` in the same module | `IEventHandler<T>` fed by an `EventConsumer<T>` in any module or domain that subscribes |
| Consistency | immediate, same transaction, rollback on failure | eventual, at least once, idempotent consumer, own transaction |
| Crosses a module | never | yes |
| Crosses a business domain | never | the only way |
| Versioned | no | yes, additive |

In the two books of record (the consumption and billing ledgers) the stored entry is itself the fact and
projections rebuild from it (ADR-005); the contract event published for others is still a separate
shape, so the ledger's storage can change without changing what consumers see.

## 5. Solutions

| Solution | Contains | Who opens it |
| --- | --- | --- |
| `AcmePlatform.slnx` | Every project | The platform pipeline; anyone changing a building block |
| `domains/<domain>/<Domain>.slnx` | The domain's projects plus the building blocks and `Platform.LocalGraph` | The domain team, daily |

## 6. Infrastructure

Two Terragrunt layers (ADR-006), each inside its owner's folder: `platform/infrastructure/` for the shared
fixtures, `domains/<domain>/infrastructure/` for what only the domain uses. Each holds one unit per resource
type under `units/`, one YAML file per instance under `base/variables/<type>/`, and per-environment overrides
under `<env>/variables/<type>/` beside that environment's `env.hcl`. Every unit includes
`${get_repo_root()}/platform/infrastructure/root.hcl`; module sources and platform dependencies are addressed
from `get_repo_root()`. State keys are layer-first, with the environment from `ACME_ENVIRONMENT` before the
type (`platform/dev/servicebus-namespaces`, `domains/metering-consumption/dev/container-apps`).

## 7. Pipelines

| Pipeline | YAML | Builds | Path filter |
| --- | --- | --- | --- |
| `acme-platform` | `platform/pipelines/platform.yml` | `AcmePlatform.slnx`, the platform's own tiers (`platform/src/tests/`), the platform stack | `platform/`, the root build files |
| `acme-metering-consumption` | `domains/metering-consumption/pipelines/metering-consumption.yml` | `MeteringConsumption.slnx`, the domain's unit and integration tiers, the domain stack, the deploy, the acceptance suite against dev | The domain folder, Growth's contracts, `platform/src/Contracts`, `platform/src/building-blocks`, `platform/src/LocalGraph`, `platform/src/architecture-tests`, `platform/pipelines/templates`, the shared Terragrunt files, the root build files |

`templates/test-jobs.yml` takes `solution` and `testRoot`; the unit job globs `<testRoot>/*.UnitTests`, the
integration job `<testRoot>/*.IntegrationTests`, in parallel. A pipeline runs only its own test folder, so a push
runs each test once. Each job fails rather than passing empty when its glob matches nothing, or when a matched
project produced no results because the solution does not build it. The architecture tests run in every
pipeline; the acceptance suite runs after a domain's deploy, against that environment.

## 8. Isolation rules, as enforced

`platform/src/architecture-tests` fails on:

1. A project under `domains/<A>/` referencing anything outside `domains/<A>/`,
   `platform/src/building-blocks/`, any `domains/*/src/Contracts/` and `platform/src/Contracts/` (the domain AppHost and LocalGraph
   may also reference `platform/src/LocalGraph/` and another domain's `src/LocalGraph/`, whose topics they
   subscribe to).
2. A module project referencing another module project.
3. A contracts project referencing anything but contracts; a building block referencing anything
   outside `building-blocks/`.
4. A runnable project (Web or Worker SDK) inside a domain outside `src/Hosts/`, `src/AppHost/` or `tests/`.
5. A `.Domain` namespace depending on a feature, `.Infrastructure` or a vendor SDK; a feature slice
   (`.Application.Features.Receive`, `.Application.Features.Interpret`, `.Application.Features.Audit`) depending on another; any type
   that is not a `...Repository` (or nested in one) depending on Dapper, SqlClient or DbUp
   (`LayeringTests`, per module).
6. A module reading the wall clock or minting a GUID (`BannedSymbols.txt`, applied to every project under
   `src/Modules/`).

## 9. Where a new thing goes

| You are adding | Put it in | Recipe |
| --- | --- | --- |
| A slice inside an existing module | its own folder in `Application/Features/` holding the event handlers at the top, its interfaces in `Abstraction/` and outcome types in `DTO/`; its consumer in `Infrastructure/Consumers/` and its implementations under `Infrastructure/`; its subscription and entities in the module's topology type; an `Add<Feature>Application()` and an `Add<Feature>Infrastructure(configuration)`; its namespace in `LayeringTests.ModuleSlices` | this page, 4.1, 4.2 and 4.6 |
| A module | `domains/<domain>/src/Modules/<Module>/`, its unit and integration test projects | `tools/new-module.md` |
| A deployable unit | `domains/<domain>/src/Hosts/<Name>/` calling the base methods and its roles' registration pairs, a graph method in LocalGraph, a file under `infrastructure/base/variables/container-apps/` (and one under `managed-identities/`), a stage in the domain pipeline | `tools/new-module.md` step 6 |
| A domain | `domains/<domain>/` with the folder set of section 4, a composition line in the platform AppHost, a pipeline registered in Azure DevOps | `tools/new-domain.md` |
| An event, whoever consumes it | The publishing domain's `src/Contracts/<Domain>.Contracts/Events/`; its topic in the contracts topology when another module reads it, in the module's topology type otherwise | ADR-004; this page, 4.3 and 4.7 |
| A second market operator or message type | a classifier under `Application/Features/Interpret/Classifiers/<market>/`, a message strategy under `Application/Features/Interpret/MessageStrategies/<market>/`, one line each in `AddInterpretApplication`; a second `Drop` configuration (container, market, fuel) for a second operator | this page, 4.1 |
| A shared fixture every domain needs | a file under `platform/infrastructure/base/variables/<type>/`; for a new type, a module under `_modules/<type>/` and a unit under `units/<type>/` | ADR-006 |
| A shared code concern with no domain vocabulary | `platform/src/building-blocks/<Concern>/` | ADR-008 |

## 10. Commands

```bash
dotnet build AcmePlatform.slnx                                        # everything, zero warnings
dotnet build domains/metering-consumption/MeteringConsumption.slnx    # one domain
dotnet test  domains/metering-consumption/MeteringConsumption.slnx --filter "Category!=AcceptanceTests"   # its unit and integration tiers (Docker)
dotnet test  platform/src/architecture-tests/ArchitectureTests        # the isolation and layering rules alone
aspire run --project domains/metering-consumption/src/AppHost         # this domain locally
aspire run --project platform/src/AppHost                             # every domain locally
dotnet run --project domains/<domain>/src/Hosts/<Domain>.Migrator     # apply the schema to a database
```
