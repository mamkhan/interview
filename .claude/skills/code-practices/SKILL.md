---
name: code-practices
description: The production code, comment, document and test practices this repository holds to, distilled from the production-readiness refactor of an earlier proof of concept (the metering rail) on 2026-09-07. Use when writing or reviewing any C# in this repository, before every commit, when a change adds a type, an interface, a comment, a document, a test or a configuration key, and whenever a change looks like it needs a new abstraction, a second implementation or a new document. Pairs with the tdd skill, which owns the test method.
---

# Code practices

## Where these come from

The proof of concept was written fast by agents and then measured: its production-readiness assessment
(2026-09-04) found one comment line for every two code lines, 228 comment lines citing documents that no
longer existed, an interface with zero implementations still injected and looped, four spellings of "register
the clock", the same topic name in seven literals, and a parallel `IClock` beside `TimeProvider`. One hundred
and forty-four commits on 2026-09-07 fixed it: production comment ratio 52% to 16%, dead abstractions deleted,
one clock, one topology type, hand-written fakes, warnings as errors.

The same generation habits reappeared in this repository within a week (measured 2026-09-16: 136 `///` blocks
in 2,749 production lines, 35 comments citing ADRs or programme documents, 6 `<remarks>`, 7 markdown files restating
code, one store interface with two production implementations, a runner and unit-of-work stack in front of one
natural-key insert, a hosted outbox relay with no producer). Each rule below names the test that decides it.
[reference.md](reference.md) maps every rule to the finding that produced it, with the citations.

## 1. Comments

The test: **what does the next engineer lose if this line goes?** If the answer is "nothing they could not
read from the code", delete it.

Keep, on the member it constrains, present tense, at most eight lines:

- A rule the code reproduces and the source it was verified against, when the code alone would read as a bug.
- An invariant a caller must honour that the type system does not enforce (ack last; session id is the MPRN).
- The retirement condition of a deliberate workaround, one sentence.
- A test-class header stating the scenario and where its frozen literals came from, two or three lines.

Delete: restatement of the code or the member name; change history ("used to", "no longer", "replaced",
"the skeleton", "the rail"); origin stories; measurements and incident history; anything describing another
file's behaviour; `<param>` and `<returns>` that repeat the name; any citation of an ADR, a programme document, a
plan, a phase or a section symbol. If the cited fact matters and is not stated at the site, state it at the
site in one sentence and drop the citation.

`///` only on the public API of a library another project consumes (`Contracts`, `building-blocks`), one to
three lines, no `<remarks>`. Never on private, internal, nested or test members, and never on a module's own
types: a module is consumed by nothing but its hosts.

Target: production under 15% comment lines; a module's `Domain/` and handlers under 10%.

## 2. Documents

**No document describes code.** A page that restates what a class does, which subscription a consumer reads
or which validator runs is stale the moment the code moves, and nobody notices. The POC lost ten links and one
data-shape description this way before the rule was written.

- `docs/architecture.md` is a map: what exists, which piece to open, the flow at the level of hosts, topics
  and stores. No method names, no config values, no algorithms.
- A surprising behaviour is explained at the site (a comment) or in the test that pins it. Never a document.
- An agreement or a piece of work in progress is a plan, outside the standing documents; it retires when the work lands.
- No per-feature or per-capability markdown. A module may carry one `Module.md` of at most fifteen lines
  stating purpose, owned facts, published contracts and non-goals, with no type names in it.
- No agent memories, no notes files. Working files go to `.ai-scratch/` and are never cited.
- If a change makes a document's claim untrue, fix or delete the claim in the same change.

## 3. Abstractions and types

The test for an interface: **is it stubbed in a test, or does it have two production implementations?** If
neither, it is a class.

- No abstraction with zero implementations. No second production implementation "while the choice is open":
  decide, keep one, delete the other.
- No parallel abstraction over a framework type (`IClock` beside `TimeProvider`).
- One owner per fact and per event. The domain raises a domain event; one place translates it into the
  contract event. Two types building the same wire message is a defect.
- A port asks for what the caller needs, not for the whole read model. A handler that needs one boolean takes
  an interface with one method.
- Calculations live in pure functions with frozen-literal unit tests, never interleaved with SQL assembly.
- A record with more than five constructor parameters uses `required init` properties. Positional parameters
  rebind callers silently when one is inserted.
- A role or mode switch is parsed once, at composition, into an enum. Hosts name the roles they run.
- Test-only or local-only code lives in a test project or a local-graph project, never in a module.
- Mechanics built for a slice that does not exist yet are speculation. Build the slice, then extract.

## 4. One place for each fact

Every name that two things must agree on lives in exactly one type, and both reference it:

- Topic, subscription, queue and container names: the publishing domain's `Contracts` topology type for
  topics; the consuming module for its own subscriptions. Terragrunt mirrors them as instance file names
  under `infrastructure/base/variables/<type>/`.
- The clock: `TryAddTimeProvider` in `ServiceDefaults`, once per host.
- A configuration pin (the Azurite API version): one key, read by one extension.
- Connection-string handling, processor lifecycle, message body reading: one helper in building blocks, not
  a copy per host.

If a grep for a literal finds it in two files, that is the defect, whatever the two files are.

## 5. Time and identity

- No `DateTime.UtcNow`, `DateTimeOffset.UtcNow`, `DateTime.Now` or `Guid.NewGuid()` in a module. Handlers
  inject `TimeProvider`; repositories take `now` from the caller and hold no clock.
- An identity that must survive a retry or a replay is derived from its inputs (a reading id from hash, MPRN,
  date and version; a receipt id from the event that announced the file), never minted per attempt.
- A consumer's clock is the message's `AsOf` when present and the system clock otherwise.

## 6. Data access

- Dapper on `Microsoft.Data.SqlClient` for runtime reads and writes; EF Core for the schema model and its
  migrations only. One idiom per store.
- Every date and timestamp parameter is typed explicitly (`DbType.DateTime2`, `DbType.Date`); an untyped
  `DateTime` binds as `SqlDbType.DateTime` and rounds a `DATETIME2(3)` column to 1/300 s.
- String predicates bind as `varchar`, not `nvarchar`, or the index is skipped.
- Bulk paths use `SqlBulkCopy`; multi-row `VALUES` batching on wide rows is banned (measured at 466 rows/s
  against 52,434).
- A host never alters a table at startup. Schema changes are migrations, applied by the pipeline.

## 7. Options

Every options class is bound with `ValidateDataAnnotations().ValidateOnStart()` and carries `[Required]` and
range attributes. A host with a missing setting refuses to start; it does not fall back to a default path.

## 8. The minimal diff

Fix exactly what was asked. Diagnose as broadly as the problem needs; change only what the reported problem
requires. Never bundle observability, refactors, renames or document rewrites into a fix. Report the other
gaps in prose and let the owner ask. The POC reverted everything but two package floors from a production fix
that had shipped with extra logging, a new query and four document edits.

## 9. Tests

The tdd skill owns the method: classicist, red-green-refactor, `When<Scenario>` classes, real domain objects,
builders, frozen literals. These are the points the POC refactor had to re-establish and this repository has
already drifted on:

- **Method names are PascalCase outcomes**: `ArchivesTheFileUnderTheHashNamedPath`, `DeletesTheMirrorCopy`,
  `DoesNotDeleteWhenPublishingFails`. Not `The_file_is_archived_before_...`. Do not disable an analyzer
  (CA1707) to permit a style the skill forbids.
- **Hand-written fakes, no mocking library.** A fake records what it was given and exposes it as state.
- **Builders in a shared test project** with valid defaults; a test overrides only what it asserts
  (`new MarketMessageXmlBuilder().ForMm343().WithMprn("...")`).
- **Expected values are literals derived offline**, with the derivation recorded in the class header. Never
  arithmetic that mirrors the implementation.
- **Integration tests are feature tests on the real composition root.** Build the host with
  `Host.CreateApplicationBuilder()`, in-memory configuration pointing at the shared container stack and this
  class's scope, and the module's own `AddBaseApplication`/`AddBaseInfrastructure` plus the role's
  `Add<Feature>Application`/`Add<Feature>Infrastructure` pair. Drop the real input; read the output off a
  test-reader subscription; poll the table. A test that constructs a store by hand and runs it against the
  database is a fine store test, and not a substitute: it passes while the wiring is broken, so every
  consumer is driven once by its role's feature test. One feature test per role, not one per message or event
  type: the per-type values are unit tests (the `testing-strategy` skill owns which tier holds what).
- **One shared container stack** (SQL Server, Service Bus emulator, Azurite) with **per-class isolation**:
  own database, own topic and blob-container prefix, production subscription names. The builder configuration
  is byte-identical across suites; reuse depends on its hash.
- **The acceptance suite is green locally before every push**, against `aspire start` with no configuration
  overrides, and against dev after every deploy. It is the guard against topology drift between the graph and
  the hosts; no pipeline job boots an AppHost.
- **Tests change only where a change forces it**: a signature, a namespace, a moved type. Assertions, frozen
  literals, scenario coverage and test names do not change, and no test is deleted or skipped, without the
  owner's recorded decision.

## 10. Build gate

`TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`, central package
management with transitive pinning, `NuGetAudit` high as error, `dotnet format --verify-no-changes` in the
pipeline. A banned-API analyzer over `Modules/` for the clock and identity rule in §5. An analyzer is switched
off only for a measured reason recorded beside the switch, never to admit a forbidden style.

## Before committing

- [ ] No `///` outside `Contracts` and `building-blocks`; none longer than three lines; no `<remarks>`.
- [ ] No comment cites an ADR, a programme document, a plan, a phase or a section; none narrates history or measurements.
- [ ] No new markdown restates code; no `Capability.md`; `Module.md` under fifteen lines with no type names.
- [ ] Every new interface is stubbed in a test or has two production implementations; no second store idiom.
- [ ] Every new literal that another file must agree with lives in one type.
- [ ] No wall clock, no `Guid.NewGuid()` for an identity that must survive a retry.
- [ ] Every temporal SQL parameter typed; every options class validated on start.
- [ ] Test methods are PascalCase outcomes; fakes are hand-written; integration tests boot the role's registration pair.
- [ ] Every tier is green locally, the acceptance suite included, against `aspire start`.
- [ ] The diff contains only what was asked.
