---
name: testing-strategy
description: Which test tier owns which behaviour in this repository and how each tier stays fast and free of overlap. Unit tests own every rule that needs no I/O, integration tests only what a real SQL Server, Service Bus or blob store decides, and the acceptance suite (run locally before every push and against deployed dev in the pipeline) proves every feature end to end and gates the release. Use when adding, moving, reviewing or deleting a test, choosing where a test goes, adding a feature, consumer, message type, host or domain, when a suite is slow, when changing a pipeline's test jobs or triggers, and before every push. Pairs with the tdd skill (the method) and the code-practices skill (what code and tests look like).
---

# Testing strategy

Every behaviour is asserted once, in the cheapest tier that can prove it. The tiers do not repeat each other:
a value a unit test pins is not asserted again by an integration test, and an integration test does not
re-run a rule the acceptance suite needs only to see wired. The `tdd` skill owns how a test is written
(`When<scenario>`, outcome names, builders, hand-written fakes, frozen literals); this skill owns where it goes.

## The tiers

| Tier | Owns | Does not |
|---|---|---|
| **Unit** (`<X>.UnitTests`, no I/O, milliseconds) | Every rule: parsing, classification and dispatch to the registered strategy, calculations, merge and precedence, ids, session keys and correlation, handler outcomes through hand-written fakes, composition (the container resolves what a role needs) | Touch a store or a bus |
| **Integration** (`<X>.IntegrationTests`, the shared container stack) | What only a real store or bus decides: SQL mapping, constraints, locks, precision and every statement a repository runs; the Parquet and blob formats; one feature test per host role on production subscription names; dead-letter and redelivery, once, in the building blocks | Re-assert a value a unit test pins; one class per message type or event type; wait a fixed time |
| **Acceptance** (`<Domain>.AcceptanceTests`, a running environment) | Every feature from its real input to its observable output on the real wiring: names, identities, Event Grid, subscriptions, sessions, migrations. The release gate: run locally before every push, against dev after DeployDev | Rule permutations; test-only infrastructure in a deployed environment |
| **Architecture** (`ArchitectureTests`) | Isolation and layering | |

There is no AppHost boot tier. The local graph is proven by the acceptance suite run against `aspire start`
before a push; the deployed graph by the same suite against dev.

## Where a test goes

| Behaviour | Tier |
|---|---|
| A parse, a classification, a calculation, a date rule, a merge, which value wins | Unit (Domain) |
| What a handler saves, publishes or returns for an input; its outcome type; its ids and session key | Unit (Application, hand-written fakes) |
| Every message type reaches its strategy; a role's registrations resolve | Unit (composition) |
| A repository's SQL: what lands in which column, a constraint, a lock, decimal and millisecond precision | Integration (store test) |
| A Parquet or blob layout, an idempotent re-store | Integration (store test) |
| A role's consumers read their production subscriptions, with sessions where the topology says so | Integration (one feature test per role) |
| A rejected message is dead-lettered on first delivery; a failure is abandoned and redelivered | Integration, once, in `BuildingBlocks.IntegrationTests` |
| A feature works from the operator's file (or the external publisher's event) to the row or blob a person reads | Acceptance |
| A deployed name, grant, subscription or Event Grid route exists | Acceptance (it fails when they are missing) |

Ask, in order: can it be proven with no I/O? Then unit. Does a real store or bus decide it? Then integration.
Is it "this feature is in place in a running environment"? Then acceptance. A test that fits a cheaper tier
moves there.

## Integration rules

- **One `SuiteScope` per class, built in a class fixture** (`IClassFixture<...>`), never in the test class's
  own `IAsyncLifetime`: xUnit makes a new instance per test, so the database would be dropped, recreated and
  migrated before every fact. Facts in a class share the database and use their own keys. A class that needs only
  a database nests `public sealed class Database() : ClassDatabase(Suite.Id, nameof(<Class>), <migrate>)`
  (`BuildingBlocks.TestsShared`); one that boots a role keeps the host in its own fixture class.
- **No fixed delay.** Wait for a positive signal inside a deadline: the row, the message, an inbox marker. To
  prove something did not happen, send a sentinel after it on the same session and wait for the sentinel;
  never wait out a timeout.
- **A fake clock fires its timers.** A `TimeProvider` fake that only fixes `GetUtcNow` turns every retry wait
  into real time.
- **One feature test per host role.** It boots the module's base methods and the role's
  `Add<Feature>Application`/`Add<Feature>Infrastructure` pair, as the host does, on production subscription
  names, and drives each of the role's consumers once. Per-type variations are unit tests.
- **Build only what is asserted.** No database for a class that never reads one; no second role booted for
  outcomes the class never checks. One class can cover every topic a role publishes on by sending the few inputs
  that reach them all (`WhenSealedMessagesAreInterpreted`).
- **Dead-letter and redelivery belong to the building blocks.** A module test does not re-prove ADR-004
  semantics; it proves its own rule rejects (unit) and its role is wired (feature test).
- The assemblies run one after another on the shared stack; the Service Bus emulator holds at most 50 topics,
  so a class creates only the topics it reads and parallelism stays inside that cap.

## Acceptance rules

- **One scenario per feature**, from the real input: a file in the drop container, or an event published the
  way its publisher sends it when the publisher runs outside the environment (the V1 reader).
- **A fresh MPRN per run**, and wait on this run's input (its file name, its receipt), never on data an
  earlier run could have left.
- **One fixture for many inputs of one kind**: drop every message type at once and assert them as a theory,
  not one copied fixture per type (`WhenEsbSendsEachMessageType`). Wait with the shared `Polling` and `Receipts`
  helpers; a timeout says which hop is missing.
- **No test-only infrastructure in a deployed environment.** What the suite cannot observe there is proven by
  the lower tiers and recorded as such.
- The suite starts nothing. With no variables it targets the local AppHost on its pinned ports;
  `ACCEPTANCE_ENVIRONMENT=dev` plus the host address variables target dev (the pipeline sets them).

## Before every push

Developers and agents run all three tiers locally. The acceptance run is the guard for the local graph's
wiring, which no pipeline job boots.

```bash
dotnet build AcmePlatform.slnx                                        # zero warnings
dotnet test  domains/metering-consumption/MeteringConsumption.slnx --filter "Category!=AcceptanceTests"   # unit and integration (Docker)
aspire start --apphost domains/metering-consumption/src/AppHost --non-interactive
aspire wait  metering-consumption-host --apphost domains/metering-consumption/src/AppHost
aspire wait  market-messaging-receiver --apphost domains/metering-consumption/src/AppHost
dotnet test  domains/metering-consumption/tests/MeteringConsumption.AcceptanceTests
aspire stop  --apphost domains/metering-consumption/src/AppHost
```

The domain solution holds the acceptance project too; the filter keeps it out of the first run, because it
fails fast when no AppHost is up. For a change to the platform (building blocks, the V1 reader, a local graph), also run
`dotnet test platform/src/tests/<Project>` for the platform's own tiers, and start `platform/src/AppHost`
instead of the domain's: it is the domain graph plus the fake V1 and the V1 reader. Never `--isolated`: it
randomises the ports the suite targets.

## Pipelines

- A pipeline's Test stage has two jobs, run in parallel: style, architecture and unit tests; and integration
  tests on the container stack. Each runs only its own folder's projects: a domain pipeline
  `domains/<domain>/tests/`, the platform pipeline `platform/src/tests/`. No test runs twice for one push.
- A domain pipeline also triggers on the building blocks and the contracts it consumes, so a shared change is
  tested and deployed with the domain. AcceptanceDev runs after DeployDev and gates production.
- A new test project is picked up by its name (`*.UnitTests`, `*.IntegrationTests`) and must be in the
  pipeline's solution; the job fails when a matched project produced no results.

## Checklists

**A new feature slice:** unit tests for its rules and handlers; store tests for any repository; one feature
test for its role; an acceptance scenario from the feature's real input; `LayeringTests.ModuleSlices`.

**A new consumer in an existing role:** unit tests for its handler; the role's feature test drives it once;
the acceptance scenario that feeds it asserts its output.

**A new message type:** unit tests for what is specific to its parse and interpretation, with a real operator
file where one exists; rows in `WhenDispatchingEveryMessageType`, `WhenAnyStrategyPublishes` (the rules every
strategy shares, asserted once) and the acceptance theory `WhenEsbSendsEachMessageType`.

**A new host or domain:** its first slice's acceptance scenario; its pipeline uses the shared Test stage and
its own test folder.

**Deleting or merging a test:** each of its unique assertions lives somewhere else first, and the change
records where. The `code-practices` rule stands: the owner decides.
