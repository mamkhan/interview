# CLAUDE.md

Guidance for Claude Code working in this repository.

## What this repo is

The **Acme V2 platform**: a modular monolith on the six Acme business domains, .NET 10 with Aspire,
Azure Service Bus as the event backbone, Azure Container Apps as the compute target. Today it holds
the platform building blocks and three modules of the Metering & Consumption domain:

- **Market Messaging**, the market gateway: from an operator file dropped in blob storage to the business
  events its handler raises.
- **Consumption**, whose Profile feature keeps each supply point's latest meter configuration and usage
  factors, and the tariff values and smart meter flag the legacy V1 platform's reader (not in this
  repository) publishes each night.
- **SupplyPoints**, whose Store feature keeps each supply point's dated history of the meter point codes,
  usage factors, meters and registers ESB states, in its own `supply` schema of the domain database. Its
  Reload feature replaces a supply point's codes and usage factors with V1's when the V1 reader's meter
  point sync loads it. Its Provide feature answers what was in effect on a day.

The consumption ledger is the next feature.

Read [docs/architecture.md](docs/architecture.md) for the map of what the code does,
[docs/repository-structure.md](docs/repository-structure.md) for where everything lives and why,
and [docs/adr/](docs/adr/) for the decisions. **The code and its tests are the source of truth for every
behaviour.**

## Commands

Run from the repository root: `global.json` there pins the SDK. Docker must be running for anything
beyond unit tests.

```bash
dotnet build AcmePlatform.slnx                                        # everything, zero warnings
dotnet build domains/metering-consumption/MeteringConsumption.slnx    # one domain
dotnet test  domains/metering-consumption/MeteringConsumption.slnx --filter "Category!=AcceptanceTests"   # its unit and integration tiers
dotnet test  platform/src/architecture-tests/ArchitectureTests        # the isolation rules alone (fast)
aspire run --project domains/metering-consumption/src/AppHost         # this domain locally
aspire run --project platform/src/AppHost                             # every domain locally
dotnet run --project domains/<domain>/src/Hosts/<Domain>.Migrator     # apply the schema to a database
dotnet test  domains/metering-consumption/tests/MeteringConsumption.AcceptanceTests   # against the AppHost now running; before every push
```

Infrastructure runs from a layer's `units/` folder or one unit, never from the repository root. Units take
the environment from `ACME_ENVIRONMENT`:

```bash
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv) ACME_ENVIRONMENT=dev
cd platform/infrastructure/units && terragrunt run --all plan
cd domains/metering-consumption/infrastructure/units && terragrunt run --all plan
cd domains/growth/infrastructure/units && terragrunt run --all plan
```

## Rules

1. **Domain isolation is enforced, not requested** (ADR-010). A domain project references only its own
   `domains/<domain>/`, `platform/src/building-blocks/`, other domains' `src/Contracts/` and the platform's
   `platform/src/Contracts/` (the events the legacy V1 platform's reader, not in this repository, publishes); a module
   references no other module; contracts reference only contracts; building blocks reference neither.
   Runnable projects in a domain live under `src/Hosts/` (or `src/AppHost/`, `tests/`). The architecture
   tests fail the build on a violation.
2. **A domain folder holds everything the domain owns**: `src/`, `infrastructure/`, `pipelines/`,
   `tools/`, its `.slnx` and its AppHost. Shared assets live under `platform/`. Nothing else at the root
   but the build baseline and the full solution.
3. **A module is one project of slices** (ADR-005, ADR-010): `domains/<domain>/src/Modules/<Module>/`
   with `Module.md` (at most fifteen lines, no type names), `<Module>Topology.cs`, `Domain/` (pure rules),
   `Application/`, `Infrastructure/` and, when slices share a parser or schema, `Common/` (`Common/ESBMessage/`).
   `Application/Features/<Feature>/` is one feature slice (MarketMessaging: `Receive/`, `Interpret/`, `Audit/`):
   its event handlers at the top, the interfaces it depends on in `Abstraction/`, its outcome types in `DTO/`,
   and operator-specific code in one sub-folder per market inside the slice that uses it. `Infrastructure/`
   holds the implementations of those interfaces (`BlobStorage/`, `Persistence/Repositories/`), the Service
   Bus consumers (`Consumers/`), options (`Configuration/`) and the schema (`Schema/`). Namespaces follow the
   folders. The domain's event types live in
   `<Domain>.Contracts/Events/`; the names another module needs to consume them live in the contracts
   topology, and the module's own topics, subscriptions, queues and containers in `<Module>Topology`. A read one
   module answers and another module in the same host calls is an interface in `<Domain>.Contracts/Queries/`
   (`IMeterPointConfiguration`), registered by the answering module's role.
   `LayeringTests` enforces: Domain depends on no feature, no infrastructure and no vendor; no feature slice
   depends on another; only a `...Repository` type touches SQL; a new slice is listed in
   `LayeringTests.ModuleSlices` as `Application.Features.<Feature>`. A role is one feature a host runs:
   `Application/Extensions/ApplicationRegistration.cs` exposes `AddBaseApplication()` and
   `Add<Feature>Application()`, `Infrastructure/Extensions/InfrastructureRegistration.cs` exposes
   `AddBaseInfrastructure(configuration)` and `Add<Feature>Infrastructure(configuration)`; a host calls both
   base methods and one pair per role it runs, and nothing else. A slice reacts to an event as a
   `<Feature><Event>EventHandler : IEventHandler<TEvent>` (`AuditMarketMessageReceivedEventHandler`); its
   `<Feature><Event>EventConsumer : EventConsumer<TEvent>` in `Infrastructure/Consumers/` (or
   `<TEvent, THandler>` when two slices in one host subscribe to the same event) is the Service Bus adapter,
   reading a subscription or queue named `{domain}.{feature}-{event}` (`metr.audit-market-message-received`).
   A handler handles one event; two events are two handlers. SupplyPoints is the exception to these names: its
   handlers and consumers are named for the event (`<Event>EventHandler`, `<Event>EventConsumer`), its subscriptions
   for the event alone while it is the topic's only reader, its command handlers in `Commands/` take the event they
   react to as the command (no separate command record), and its inbox is keyed on the subscription.
   A slice with a SQL aggregate runs its `ICommandHandler<TCommand, TOutcome>` through `CommandRunner` on
   the scoped `IUnitOfWork` (validate, begin, guard idempotency, handle, dispatch domain events, commit);
   a slice whose writes carry their own natural key does not.
   Expected results are typed outcomes; `CommandValidationException` and `DomainException` are the only
   deterministic exceptions (dead-letter, 400, 422); a processing failure that is a business outcome is a
   published event and the message completes. An interface exists because a test stubs it or two
   implementations exist. No module reads the wall clock or mints a GUID (`BannedSymbols.txt` fails the
   build); identities that must survive a retry are derived from their inputs.
4. **Schema changes only through numbered SQL scripts** in the module's `Infrastructure/Schema/Scripts/`, named
   `<Domain>-<Module>-NNNNN - <description>.sql` (`Metr-MarketMessaging-00001 - Inbound market message.sql`),
   embedded and applied by DbUp through a `SchemaVersions` journal (`BuildingBlocks.Persistence.SchemaMigrator`).
   The journal records the file name without its folder, so a script may move; the prefix keeps modules
   apart in the journal, and the migrator refuses a script without it. A script that has run never
   changes, its file name included; the next change is the next script. The platform tables come from
   `BuildingBlocks.Persistence`'s own script, applied first. Hosts apply unapplied scripts in Development
   and refuse to start on an unapplied script anywhere else (`Apply<Module>MigrationsAsync`); the domain's
   migrator host applies them in a deployed environment. Never write startup DDL (ADR-003 as amended).
5. **Dapper over `Microsoft.Data.SqlClient` is the only store idiom, and the scripts are the only
   description of the schema** (ADR-003 as amended 2026-09-22; EF Core is not in this repository).
   One implementation per repository. Bulk paths use `SqlBulkCopy`; wide-row multi-row
   `VALUES` batching is banned; every temporal parameter is explicitly typed (`BuildingBlocks.Sql.SqlTemporal`);
   string predicates bind as `varchar`. Entity names are the topology constants resolved through
   `EntityNames`, whose prefix is empty everywhere but a test scope.
6. **Messaging semantics are the platform's spine** (ADR-004): at-least-once, idempotent consumers,
   acknowledge last, sessions where a key needs ordering, deterministic failures dead-lettered on
   first delivery, everything else abandoned for bounded redelivery. Entity names live only in the
   topology types: the contracts' for what other modules consume, the module's own for the rest. Topics are registered per module through `AddEventTopics`. State-plus-
   publish goes through the outbox.
7. **Local graphs are libraries** (ADR-010): a domain's Aspire resources and hosts are extension methods
   in `<Domain>.LocalGraph`; the domain AppHost and the platform AppHost both call them and contain no
   wiring of their own. Changing the graph means changing the library.
8. **Tests are classicist TDD in three tiers** (ADR-007; the `tdd` skill owns the method, the `code-practices`
   skill what the code must look like, and the `testing-strategy` skill which tier owns which behaviour, all
   under `.claude/skills/`): `When<scenario>` classes, PascalCase outcomes as method names (CA1707 is on), real
   domain objects, hand-written fakes (no mocking library), expected values frozen as literals with their
   offline derivation recorded. Unit tests own every rule that needs no I/O; integration tests only what a real
   store or bus decides, on the shared container stack (`BuildingBlocks.TestsShared`: SQL Server, the Service
   Bus emulator, Azurite), each class on its own `SuiteScope` built once in a class fixture, with one feature
   test per host role that boots the base methods and the role's `Add<Feature>Application`/`Add<Feature>Infrastructure`
   pair and drives each of its consumers once; the acceptance suite proves every feature end to end on a
   running environment and gates the release. A behaviour is asserted once, in the cheapest tier that can
   prove it. No pipeline boots an AppHost: the acceptance suite run against `aspire start` before a push guards
   the local graph.
9. **Trunk-based development** (ADR-008): `main` is always releasable; branches live hours; incomplete
   work lands dark, never on a long-lived branch. Before a push, every tier is green locally, the acceptance
   suite included, against `aspire start`.
10. **Comments carry a non-obvious why on the member they constrain**, at most eight lines, present
   tense; no history, no measurements, no restating the code. `///` only on contracts and building-
   block surfaces, one to three lines.
11. **Documentation is `docs/architecture.md` (a map), `docs/repository-structure.md` (the layout),
   `docs/adr/` (decisions), each module's `Module.md`, and the recipes in `tools/` and
   `domains/<domain>/tools/`. No other standing documents; no document describes code.** Behaviour lives
   in code and tests. No comment cites an ADR, a plan or a phase. Agent working files go to
   `.ai-scratch/` (gitignored), never cited.
12. **Infrastructure is two Terragrunt layers** (ADR-006): `platform/infrastructure/` for shared
   fixtures, `domains/<domain>/infrastructure/` applied by that domain's own pipeline. Units include
   `${get_repo_root()}/platform/infrastructure/root.hcl`. Every resource type is one unit,
   `infrastructure/units/<type>/`, serving every environment from `ACME_ENVIRONMENT` with one state each
   (`platform/<env>/<type>`, `domains/<domain>/<env>/<type>`): its instances are YAML files under
   `base/variables/<type>/`, named for the instance, a same-named file under `<env>/variables/<type>/`
   replaces top-level keys, and `platform/infrastructure/resources.hcl` loads them. Deleting a file
   destroys the instance and renaming one replaces it; deleting a unit folder strands its state, so its
   instances go first. Environment folders hold only `env.hcl` (environment-wide facts) and `variables/`,
   and an entity's instance file is named for the entity. A value one resource reads from another's output
   is `{ from: <ref> }` in the file, resolved by the unit. Tiers are data (base files, environment
   overrides, `env.hcl`), never module code. Every Azure name is composed in the Terragrunt unit from the
   org, environment, domain and instance segments of ADR-009, and no module composes a name.
13. **Deployed access is Entra ID only** (ADR-006 rule 6): the host reads the same configuration keys
   locally and deployed, authenticates with its user-assigned identity, and no connection string,
   key or password exists in configuration. Adding a secret to an app setting is a review finding.

## Layout

See [docs/repository-structure.md](docs/repository-structure.md). New modules follow
[tools/new-module.md](tools/new-module.md) with `market-messaging` as the copy source; new domains follow
[tools/new-domain.md](tools/new-domain.md) with `metering-consumption` as the copy source.
