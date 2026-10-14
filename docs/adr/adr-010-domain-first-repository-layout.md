# ADR-010: Domain-First Repository Layout

Status: Accepted
Date: 2026-09-10
Supersedes: the solution layout section of ADR-001 (its isolation rules stand, restated in §Decision below)
Evidence: technical direction of 2026-09-10 (each domain operates in complete isolation: code, infrastructure and pipeline inside the domain folder); ADR-002 as amended (deployment granularity is each domain's own concern); ADR-006 as amended (each domain applies its own infrastructure stack)

## Context

ADR-001 laid the repository out by concern: `src/domains/<domain>/`, `infrastructure/domains/<domain>/`,
`pipelines/<domain>.yml`. A domain team therefore worked in three top-level trees, and adding a domain
touched three places. ADR-002 and ADR-006 were both amended to make each domain the owner of its own
deployment and infrastructure, which made the concern-first split a relic of the earlier "one deployment"
reading.

Three further needs emerged as the first domain filled in:

- A domain team needs a solution and a local run that contain only its own domain, without a second
  copy of the Aspire wiring.
- Inside a module, the capability being worked on should be one folder, not one file in each of three
  type-named folders.
- Which modules deploy together must be a host's decision, so a module cannot itself be runnable.

## Decision

**Domain-first layout.** Each domain is one top-level folder under `domains/` holding everything it
owns; `platform/` holds what Platform Engineering owns and every domain consumes; the root holds only
the build baseline and the full solution.

```
acme-platform/
  AcmePlatform.slnx  Directory.Build.props  Directory.Packages.props  global.json  nuget.config  .config/
  docs/  tools/
  platform/
    pipelines/         platform.yml, templates/
    infrastructure/    root.hcl, common.hcl, layer.hcl, bootstrap/, _modules/, dev/ tst/ prd/
    src/               building-blocks/, LocalGraph/, AppHost/, architecture-tests/, tests/
  domains/<domain>/
    <Domain>.slnx
    pipelines/         <domain>.yml
    infrastructure/    layer.hcl, domain.hcl, sql/, dev/ tst/ prd/
    tools/
    src/               AppHost/, LocalGraph/, Contracts/, Modules/<Module>/, Hosts/
    tests/             <Domain>.<Module>.UnitTests/, <Domain>.<Module>.IntegrationTests/, <Domain>.AcceptanceTests/
```

**Inside a module** (ADR-005's intent kept; layout revised 2026-09-10 to vertical slices): one project
`<Domain>.<Module>` with `Module.md` (the charter), `ModuleRegistration.cs` (a roles enum and
`Add<Module>(roles)`, `Map<Module>Endpoints()`), `Domain/` by aggregate (the rules every slice shares),
`Features/<UseCase>/` holding everything the use case needs (handler, outcome types, the interfaces it
depends on and their implementations, its trigger, a `Capability.md` naming the capability-map rows it
implements), and the shared concepts the module names: `Persistence/` (its tables, its schema scripts
and any store interface two slices share), `Markets/<operator>/`, `MeterFamilies/<family>/`. An
interface belongs to the slice that needs it or to the shared concept several slices need; there is no
`Ports/` or `Adapters/` bucket. Namespaces follow the top-level folders; feature folders add none.
Enforced on the compiled types: Domain depends on nothing outward, a shared concept never depends on a
slice.

**Hosts** live under `domains/<domain>/src/Hosts/` and are the only runnable projects in a domain
outside the AppHost and tests. A host names the module roles it runs (`AddMarketMessaging(Index | Api)`),
registers the Aspire client integrations, and nothing else; starting a worker never starts every consumer.

**Contracts** live in the publishing domain at `domains/<domain>/src/Contracts/<Domain>.Contracts`.

**Local run.** Each domain has an AppHost under `src/AppHost/` and a `<Domain>.LocalGraph` library
whose extension methods on `IDistributedApplicationBuilder` define its resources and hosts. The platform
AppHost under `platform/src/AppHost/` composes every domain's graph methods on `Platform.LocalGraph`'s
shared fixtures. No AppHost contains wiring of its own.

**Infrastructure.** Both Terragrunt layers keep ADR-006's model. Units include
`${get_repo_root()}/platform/infrastructure/root.hcl`; module sources and platform dependencies are
addressed from `get_repo_root()`. The state key is derived from the unit's repository path with the
`infrastructure` segment removed, so keys stay `platform/<env>/<unit>` and
`domains/<domain>/<env>/<unit>` and a folder move never moves state.

**Pipelines.** `platform/pipelines/templates/test-jobs.yml` is parameterised by solution and test root. A
domain pipeline passes its own `tests/` folder; the platform pipeline builds the root solution and runs
`platform/src/tests/`, so each test runs in one pipeline (amended 2026-10-12 with ADR-007). Template references
are root-relative.

**Isolation rules**, enforced by `platform/src/architecture-tests` from the csproj files on disk:

1. A project under `domains/<X>/` references only projects under `domains/<X>/`,
   `platform/src/building-blocks/` and any `domains/*/src/Contracts/`; the domain's AppHost and
   LocalGraph may additionally reference `platform/src/LocalGraph/`.
2. A module project references no other module project.
3. A contracts project references only contracts projects; a building block references only building blocks.
4. A runnable project (Web or Worker SDK) inside a domain sits under `src/Hosts/`, `src/AppHost/` or the
   domain's `tests/` folder.
5. Inside a module, the `.Domain` namespace depends on no `.Features`, shared-concept or vendor SDK
   namespace, and no shared concept (`.Persistence`, `.Markets`, `.MeterFamilies`) depends on `.Features`
   (ADR-005).

## Consequences

- A domain team's whole world is `domains/<domain>/`: one solution, one AppHost, one pipeline, one
  infrastructure stack. Adding a domain is one folder (`tools/new-domain.md`) plus one pipeline
  registration and one composition line in the platform AppHost.
- The platform pipeline remains the proof that every domain compiles and composes into one graph; the
  domain pipeline is the domain's own gate and deploy.
- Two AppHosts share one graph definition, so what a team runs daily is what the composed platform runs.
- Splitting or merging hosts is a `Program.cs` and a Terragrunt `app` unit, never a module change.
- ADR-001's rationale for one repository and one full solution stands; its layout diagram and the
  first-level paths in its rules are superseded by this record. `docs/repository-structure.md` is the
  working reference.

## Relation to the platform owner's guide

The platform owner's guide (*Acme V2: Architecture, explained*, September 2026) is guidance, not a
rule set (direction of 2026-09-10). Its concerns are met by decisions already taken; the table records
where, and the two deliberate departures.

| Concern in the guide | How this repository meets it | Decision |
| --- | --- | --- |
| One owner per authoritative fact; other modules use contracts, never private tables | A module's tables are reached only through its own code; another module reaches it through the published contracts project or the bus. A cross-domain project reference to anything but `src/Contracts/` fails the architecture tests, and so does a module referencing another module. | ADR-001 rules 1 to 3, ADR-010 rule 1 |
| Explainable history: revisions and postings preserved, evidence linked to inputs | The consumption and billing ledgers are append-only with corrections as new entries; every reading carries provenance to the sealed source file; the raw archive is write-once. | ADR-003 as amended, ADR-004 rule on the outbox |
| Safe recovery: committed intent durable, retries safe, long-running progress recorded | State-plus-publish goes through the outbox; consumers are idempotent by message id and acknowledge last; deterministic failures dead-letter on first delivery; sessions order per key. | ADR-004 |
| Hosts declare the roles they run; starting a worker must not start every consumer | A host is a list of `Add<Module>Module()` calls; the capture worker composes only the capture half. Roles are an explicit parameter of the module registration: `AddMarketMessaging(MarketMessagingRoles.Capture)`. | ADR-002, ADR-010 §Hosts |
| The module kit: charter, contracts, features as vertical slices, persistence, tests, registration | Adopted as the module layout with the slices taken to their conclusion: `Module.md`, `ModuleRegistration`, `Domain/`, `Features/<UseCase>/` holding everything a use case needs, shared concepts (`Persistence/`, `Markets/`), one project per module. The kit's separate `Ports/` and `Infrastructure/` buckets are not used: an interface sits with the slice that needs it or in the shared concept several slices need. | ADR-005, ADR-010 §Inside a module |
| Do not create sixty empty modules to mirror the map; several modules may belong to one team | A module folder exists only when it has a real slice and tests. The map's sixty sub-domains stay in the capability map, each with a status of `exists`, `planned` or `not-software`, so the repository does not have to mirror it. | ADR-010 §Consequences |
| Deployment as placement choice: consolidated core, selective scale-out, extraction later | Each domain decides its deployable units and may run one host or several; splitting a host is a `Program.cs` and a Terragrunt `app` unit. | ADR-002 as amended |

**Deliberate departures.** Each domain owns a database rather than sharing one database with owned
schemas, because each domain must be able to operate, deploy and recover independently (ADR-003, ADR-006
as amended); a module's tables and migrations are still its own within that database. Azure is the first
runtime (Container Apps, Service Bus, Azure SQL, Blob) behind the ServiceDefaults and contracts boundary
rather than cloud-neutral code from the start; the domain model references no vendor SDK (ADR-005), and
the containers and Terraform modules are the portability line.

**Open questions the guide raises, still ours to answer.**

| Question | Owner | Due |
| --- | --- | --- |
| Ledger storage at the expected volume: which ledger, rows per reading or posting, access paths, retention; benchmark of the ADLS event store plus Azure SQL projections against a partitioned relational ledger; acceptance criteria (p95 and p99 latency, sustained throughput, cost per retained year, restore time) per the guide's §07 | Billing & Pay (billing ledger selection) | Proposed 2026-10-16 |
| Whether module boundaries become assembly boundaries (one project per module today; a `Contracts` project per module when a second module consumes it) | Platform Engineering | At the first cross-module contract |

## Addendum, 2026-09-23: feature slices named for the flow; infrastructure beside them

1. **Slices live under `Features/` and are named for the step of the flow they perform.** MarketMessaging
   is `Features/Archiving/` (a dropped file is archived and announced), `Features/Processing/` (it is
   classified and handled) and `Features/Tracking/` (its receipt and outcome are recorded). Inside a slice
   the event handler and its consumer sit at the top and the ports, adapters and repository in
   sub-folders. The `Persistence/` concept is gone: recording receipts is a slice, and the scripts are
   `Infrastructure/Schema/`, the one place for what the module needs technically but no slice owns.
2. **Namespaces follow the folders**, sub-folders included, replacing the rule above that feature folders
   add none. `LayeringTests` names each module's slices (`Features.Archiving`, ...) and forbids a slice to
   depend on another and the domain to depend on a feature or on `Infrastructure`.
3. **The domain's event types live in `Contracts/<Domain>.Contracts/Events/`**, those another module
   consumes and those one module's slices pass to each other alike. The topology names another module
   needs stay in the contracts topology; the topics, subscriptions, queues and containers only the owning
   module uses are in the module's own topology type.

## Addendum, 2026-09-23: features named for the module's capabilities

The MarketMessaging features take the capability names of the module brief and sit in a folder per
direction: `Features/Inbound/Receive/` (was Archiving), `Features/Inbound/Interpret/` (was Processing; it
includes accepting a message, which the brief lists as a capability of its own) and
`Features/Inbound/Audit/` (was Tracking). Outbound is out of scope until it is built. The roles follow the
features, `Receive`, `Interpret` and `Audit`, replacing `Archive`, `Process` and `Track` in the ADR-002
addendum of the same date. Class names, the host and every deployed name are unchanged.

## Addendum, 2026-09-25: an application half and an infrastructure half; a role is a registration pair

1. **A module is `Domain/`, `Application/` and `Infrastructure/`**, ADR-005's layering made the top-level
   folders. Slices move to `Application/Features/<Feature>/` with no direction folder (`Receive/`,
   `Interpret/`, `Audit/`). A slice keeps its event handlers at the top, the interfaces it depends on in
   `Abstraction/`, its outcome types in `DTO/` and its operator-specific code in one sub-folder per market.
2. **Adapters leave the slices.** The Service Bus consumers (`Infrastructure/Consumers/`), the
   implementations of the slices' interfaces (`Infrastructure/BlobStorage/`,
   `Infrastructure/Persistence/Repositories/`) and the options (`Infrastructure/Configuration/`) sit beside
   `Infrastructure/Schema/`. Two slices that need the same store each declare their own interface, and one
   implementation may serve both, so no slice depends on another. This replaces "an interface sits with the
   slice that needs it, next to its implementation" and the rule of the first 2026-09-23 addendum that a
   slice holds its consumer and adapters.
3. **`Common/` holds what several slices parse**, the ESB message parser and its schema-shaped types
   (`Common/ESBMessage/`), under the namespace its folder names.
4. **A role is one feature, registered in two halves.** `ModuleRegistration.cs`, its roles enum and
   `Add<Module>(roles)` are gone. `Application/Extensions/ApplicationRegistration.cs` exposes
   `AddBaseApplication()` and `Add<Feature>Application()`; `Infrastructure/Extensions/InfrastructureRegistration.cs`
   exposes `AddBaseInfrastructure(configuration)` and `Add<Feature>Infrastructure(configuration)`. A host calls
   both base methods and one pair per role it runs; §Hosts otherwise stands. A role registers nothing another
   role needs, so the guard that refused a host naming a role without the role whose table it reads has
   nothing left to guard, and is removed.
5. **`LayeringTests` names the slices as `Application.Features.<Feature>`**; its three rules are unchanged.

## Addendum, 2026-10-06: Pricing in Growth; one local graph may take another's

1. **Pricing & Rate Cards sits in Growth**, decided by the user on 2026-10-02, so `TariffResolved` is Growth's
   event on Growth's topic. Growth starts with a contracts project, a local graph and infrastructure only. The
   other placements in the open question above stay open.
2. **A domain's LocalGraph may reference another domain's LocalGraph**, extending isolation rule 1. A
   consumer's fixtures take the publisher's fixtures record and subscribe to its topics there, as
   `tools/new-domain.md` describes; without the reference the consumer cannot reach the publisher's topic
   resource.

## Addendum, 2026-10-12: the consumption ledger's file format is a contract

1. **The ledger's file format is published**: its row type, its file names and its Parquet options, in
   `domains/metering-consumption/src/Contracts/MeteringConsumption.Contracts.Ledger`, a second contracts project kept
   apart so only the ledger's writers and readers carry Parquet. Record's store writes through it, and Forecast reads
   through it.
2. **The V1 reader (not in this repository) is the one writer of the ledger outside the Consumption module.** It writes first entries only,
   create-only; every later version of a day goes through Record's version rules.

## Addendum, 2026-10-12: the platform publishes contracts

1. **A platform service's events are the platform's**, on topics named with the `plat` code, and their types and
   topic names live in `platform/src/Contracts/Platform.Contracts`, the publisher's contracts as ADR-009 §7.1
   requires. The first is `SupplyPointLoadedFromV1`, which the V1 reader publishes and SupplyPoints consumes; nothing
   else will ever publish it, unlike `TariffResolved`, which the reader publishes on Growth's topic in Pricing's
   place.
2. **Isolation rule 1 extends to it**: a domain project may reference `platform/src/Contracts/`, and that project,
   like every contracts project, references only contracts. A platform contract defines its own envelope and data
   types rather than reference a domain's.
