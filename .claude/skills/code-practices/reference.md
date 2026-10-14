# Code practices: where each rule came from

Every rule in `SKILL.md` traces to a measured finding in the proof of concept, the fix that landed, or the
same pattern re-measured in this repository. Citations are to the proof of concept, an earlier repository
not included here, at `main` on 2026-09-14, and to this repository at commit `a9cda93`.

## The source documents

| Document | What it holds |
| --- | --- |
| The proof of concept's production-readiness assessment | The assessment (Part 3), the decisions (Part 2) and the work list (Part 4). §3.3 is the comment policy; §3.5 is the design-quality table F1 to F29; Part 4 B is the cleanup that landed on 2026-09-07. |
| The proof of concept's `CLAUDE.md` | Rules 1 to 4 (documents, memories, scratch, minimal diff), "Verify, don't assume", the invariants, "Code shape and conventions". |
| The proof of concept's `tdd` skill | The test method. Identical copy in this repository. |
| The proof of concept's git log, 2026-09-07 | The commits, one per project or finding, each message carrying the before and after counts. |

## Rule to finding

| Skill rule | Finding in the POC | Fix that landed | Same pattern here (2026-09-16) |
| --- | --- | --- | --- |
| §1 comments: keep, move, delete; 8-line budget | §3.3: 52.2% comment ratio in production, 63% of it `///`; 12 longest blocks all on private members; 228 lines citing dead paths; `ADR-00xx` cited 71 times with no ADR in the repository | B2, B3: one commit per project ("comments to the why"), production 52% to 16%, moved V1 evidence to `docs/v1/9` to `14` | 136 `///` blocks in 2,749 lines; 35 comments citing ADRs, programme documents or plans; 6 `<remarks>`; `Contracts` at 44%, `Seedwork` at 28% |
| §1 `///` only on consumed public API | §3.3.3: `///` buys nothing on private or internal members; four library surfaces named | Same commits | `///` on module handlers, options, DbContext, registration methods |
| §2 no document describes code | CLAUDE.md rule 1: ten dead links and one wrong data shape found 2026-09-03; "V2 documentation is `docs/architecture.md` and nothing else" | B6: `CLAUDE.md` and `architecture.md` rewritten | 4 `Capability.md` and 2 `Module.md` naming classes, subscriptions, validators; `repository-structure.md` at 390 lines |
| §2 no agent memories; `.ai-scratch/` | CLAUDE.md rules 2 and 3: 30 memory files cleared, 6 unreachable, 2 contradicting the code | Rule written | Rule carried into this repository's `CLAUDE.md` rule 11 |
| §3 no zero-implementation abstraction | N2: `IPreComparisonTask`, zero implementations, still injected and looped | B4: deleted (commit `9aa8820`) | `OutboxRelay<MarketMessagingDbContext>` hosted with no outbox writer in any module |
| §3 no second implementation while a choice is open | D1a: keep Dapper on the production paths rather than rewrite behind a second idiom | Decision recorded, not built twice | `IInboundMessageIndex` with `DapperInboundMessageIndex` and `EfInboundMessageIndex`, selected by configuration, every test run against both |
| §3 no parallel abstraction over a framework type | F19: `IClock` and `SystemClock` beside `TimeProvider`; the acceptance suite needed an adapter | Deferred in the POC; adopted here from the start (`TimeProvider` everywhere) | Held |
| §3 one owner per event | F12: the ledger domain referenced the wire contracts and built `ConsumptionRecorded`, the application finished it with `with` | Deferred in the POC, recorded | `MarketMessageReceived` built in one place: held |
| §3 narrow ports | F11: the parser took the whole `IMeterConfigReadModel` to ask one boolean | Deferred, recorded | `IUnitOfWork` with eight members injected into a store that needs a connection and a transaction |
| §3 pure calculations out of repositories | F5, F6: register arithmetic interleaved with SQL assembly in two repositories, no unit test on the money-relevant part | Deferred, recorded | Held: `IntervalSeries` and `DropFileEligibility` are pure |
| §3 `required init` over long positional records | F15: twenty positional parameters, seventeen optional, column list copied four times | Deferred, recorded | Held |
| §3 roles parsed once | F13: the `DailyJob` role switch read twice, once case-insensitively | A2 added a third role without fixing it | Held: no role switch to parse; a host calls one `Add<Feature>Application`/`Add<Feature>Infrastructure` pair per role |
| §3 no test-only code in production | F14: a "Production does not call this" sweep with its own polling loop in a production class | B4: moved to `Tests.Shared` (commit `5792c4c`) | Held |
| §3 no speculative mechanics | Not a POC finding; the POC built nothing ahead of a caller | | `CommandRunner`, `IUnitOfWork`, inbox, `ClientRequests`, `DomainEventDispatcher` built for a SQL aggregate that does not exist, then retrofitted onto a natural-key insert |
| §4 topology names in one type | F29: `"reading-received"` in 7 literals across 6 files; `dbo.ForecastByBusinessDay` in 11 | B5: `RailTopology` in `MeteringRail.Contracts` (commit `b480e97`) | `MeteringConsumptionTopology`: held. Subscription names of future consumers sit in the publisher's contracts: to move |
| §4 one clock registration | F21: four spellings of "register the system clock", two shadowing test registrations | B5: `TryAddTimeProvider` in `ServiceDefaults` (commit `6e01ab7`) | Held |
| §4 one configuration pin | F24: the Azurite API version configured twelve times | B5: one `Azure:Blob:ServiceVersion` key (commit `4714399`) | Held, with the same key |
| §4 one helper, not copies | F16: connection-string normalisation in 12 copies; F17: processor lifecycle in four hosts | Deferred in the POC | `EventConsumer` base: held |
| §5 no wall clock in a module | F4: `DateTimeOffset.UtcNow` two lines from an injected `TimeProvider`; repositories stamping wall clock | Deferred, recorded | `ParseMeterMessage`, `CaptureMarketMessage` inject `TimeProvider`: held. No build gate enforces it |
| §5 derived identities | The POC's `IntervalReadingReceived` event id `{hash}:{mprn}:{date}:{rrvn}`; day commit carries its date so a retry cannot drift | Design from the start | `ReadingId` derived: held. `CorrelationId` and `EventId` minted per attempt |
| §6 Dapper runtime, EF Core schema | D1, D1a: EF owns migrations, Dapper stays on the hot paths | Decision 2026-09-04 | ADR-003 "as amended" kept both idioms open at runtime |
| §6 typed temporal parameters | F3, A17: untyped `DateTime` binds as 1/300 s and rounded `DATETIME2(3)` columns in five repositories | `SqlTemporal.AddUtc` / `AddDate` | `BuildingBlocks.Sql.SqlTemporal`: held |
| §6 `varchar` predicates | Commit `1148313`: "bind every string predicate as varchar, not nvarchar" | Landed | To verify per store |
| §6 `SqlBulkCopy`, no wide `VALUES` batching | The data-access benchmark: 52,434 rows/s against 466 | Measured here | Rule in `CLAUDE.md` rule 5 |
| §6 no startup DDL | D10, §3.1: startup DDL and in-code migrations replaced by migrations applied before the code rolls out | Landed 2026-09-07 | `Apply<Module>MigrationsAsync`: held |
| §7 options validated on start | A10: options bound and never validated; a missing mirror path fell back to a profile folder | Listed | `CaptureOptions` validates one property by lambda; no `ValidateOnStart` elsewhere |
| §8 minimal diff | CLAUDE.md rule 4: the 2026-07-28 fix that shipped extra logging, a query and four document edits, all reverted | Rule written | |
| §9 PascalCase outcome names | tdd skill `naming.md`: `Persists()`, `EmitsAnOrderSubmittedEvent()`; every POC test | House style | `.editorconfig` disables CA1707 "by design" to allow `The_file_is_archived_...` |
| §9 hand-written fakes | B7: "one mocking style (the hand-written fakes are the house style; NSubstitute is used in four projects)" | Partly landed; two POC projects still reference NSubstitute | `Fakes.cs`: held |
| §9 builders with defaults | `Tests.Shared/MarketMessageXmlBuilder`, `IntervalReadingBuilder`, `Personas` | Design from the start | No builders; XML inlined in tests |
| §9 frozen literals derived offline | tdd skill "Computed Expected Values"; CLAUDE.md "What a number should be" | House style | `WhenParsingARealEsbIntervalFile`: held, derivation in the header |
| §9 feature tests on the real composition root | `MarketMessage.IntegrationTests/WhenConsumingAMarketMessage`: `builder.AddIngestionWorker()` and `builder.AddMarketMessageConsumer()` on the shared stack, real file in, event read off `test-reader`, row polled | Design from the start | Integration tests construct stores directly (`CreateStore(storeKind)`); no bus in the shared stack |
| §9 one shared stack, per-class isolation | `Tests.Shared/SharedRailContainers` (byte-identical builder config, cross-process lock), `SuiteIsolation` (own database, prefixed topics and containers, real subscription names, entities created through the emulator's admin endpoint) | Commits `2f050c9` to `d252219` | `SharedPlatformContainers`: SQL and Azurite only; no emulator, no topic or container prefixing |
| §9 acceptance before push, no overrides | `MeteringRail.AppHost.IntegrationTests`; CLAUDE.md test tiers | Design from the start; the boot tier replaced by the local acceptance run on 2026-10-12 (ADR-007 amendment) | `MeteringConsumption.AcceptanceTests` against `aspire start` and dev: held |
| §9 tests change only when forced | Part 4 rule for every item: "Assertions, expected values, frozen literals, scenario coverage and test names do not change, and no test is deleted or skipped, without the owner's approval as a recorded decision" | D10 was that recorded decision | |
| §10 build gate | A15, D13: no gate existed; 57 warnings; a high advisory on a direct pin | Commits `07de955` to `06c5fe8`: CPM, `Directory.Build.props`, `.editorconfig`, analyzers, zero warnings, warnings as errors, format check in the pipeline | Held from the first commit |

## Reading the POC as the reference

When a question of style is not settled by the rules, read the POC after its refactor, not before, and the
oldest components first: `EsbFolderWatcher.Core`, `EsbFolderWatcher.Worker`, `MarketMessage.Application`,
`MarketMessage.Consumer` were at 10% to 22% comment ratio before the cleanup and are the density and structure
the rest was brought to. `ConsumptionLedger.Domain` is the model for a pure domain; `ConsumptionForecast.Application/CalculationProfile.cs`
is the model for a comment that earns its place (the rule, the source it was verified against, present tense,
on the member).
