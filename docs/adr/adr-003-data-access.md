# ADR-003: Data Access

Status: Accepted (review of 2026-09-07); amended 2026-09-07: schema ownership moves to EF Core migrations (rail decision D1), and the runtime idiom selection is deferred: persistence sits behind ports with both a Dapper and an EF Core implementation built, chosen later from working code (owner direction, 2026-09-07)
Date: 2026-09-07
Evidence: the data-access benchmark of 2026-09-07; the proof of concept's production practice; the non-functional register's read and write targets

## Context

The platform needs one data-access idiom for its Azure SQL estate (projections, forecasts, workflow and configuration tables; the ledgers are ADLS Parquet and outside any ORM). The decision rule set on 2026-09-07: analyse the trade-offs; if every option meets the requirements, pick the best overall and prefer one idiom. The spike benchmarked EF Core 10 and Dapper on the rail's real 48-column projection table against the register targets.

The measured facts: both idioms meet the point-read target under write load (Dapper p50 0.40 ms, EF Core p50 0.51 ms against a single-digit-millisecond target) and the daily consumer-upsert volume with headroom (706 against 443 events/s single-threaded, well above the daily need). `SqlBulkCopy` is 24 to 55 times faster than any row-at-a-time method. EF Core's idiomatic upsert costs two round trips against Dapper's one, and closing that gap means raw SQL through EF, which bypasses the change tracking that justifies EF. Multi-row `VALUES` batching collapses on wide rows (466 rows/s at 2,040 parameters per command).

## Decision

**Dapper with `Microsoft.Data.SqlClient` is the single data-access idiom for the platform.** EF Core is not used. Specifically:

1. **Bulk paths use `SqlBulkCopy`** (or a table-valued parameter where a set must join a transaction): the history backfill, forecast commits, and any path writing more than a few hundred rows at once. Row-at-a-time code on a bulk path is a review finding.
2. **Wide-row batching is per-row commands inside an explicit transaction**, never multi-row `VALUES`: the benchmark showed per-row is 4.8 times faster on 48-column rows.
3. **Schema is owned by the component that creates it, on startup**: guarded, idempotent DDL, exactly the rail model. A migration that cannot be made safe automatically refuses to run and names its operator script in `sql-scripts/`. Heavyweight operations (clustered-index rebuilds) never run inside startup; they degrade to a warning and an operator script, the lesson the rail already paid for.
4. **Every temporal parameter is explicitly typed**: `DbType.Date` for dates, `DbType.DateTime2` for timestamps, via `DynamicParameters`. An untyped `DateTime` binds as legacy `DATETIME` and silently rounds milliseconds; at least one test per table pins a timestamp a `DATETIME` cannot represent.
5. **Unit of work is an explicit `SqlTransaction` owned by the message-handling scope**: all writes for one message (state change, processed-marker, outbox row per ADR-004) enlist in it; it commits before the message is acknowledged. No abstraction is introduced until a second implementation needs the seam; the reference codebase's `DapperUnitOfWork` is the model if that day comes.

**Named revisit trigger**: when the first workflow-heavy, relationally rich module (product catalogue, cases) enters build, reassess with a one-page addendum and the same benchmark re-run on that module's schema, deciding between staying as-is, adding DbUp migration journals, or a scoped EF Core exception for that module class.

## Consequences

- One idiom across the platform: every repository, test fixture and review checklist follows the same patterns; the four rules above go into the module recipe and `CLAUDE.md`.
- No EF migrations: schema evolution is startup DDL plus operator scripts, which fits the projection-dominated estate but puts real design weight on the guarded-DDL discipline; the rail's `EnsureSchemaAsync` patterns are the templates.
- The rail's data-access code migrates in Phase 5 unchanged, which removes a whole class of migration risk.
- The revisit trigger is written down, so the EF question reopens on evidence rather than preference.

## Amendment of 2026-09-07: EF Core owns the schema; the runtime idiom is chosen later from working code

**Provenance.** Two decisions supersede parts of the drafted position. First, the rail's production-readiness plan records D1, decided by its owner on 2026-09-04: EF Core migrations own the schema through a dedicated Database project, the pipeline runs the migrations bundle before code rolls out, and every host refuses to start on pending migrations; D10 (executed 2026-09-07) deleted the in-code migration branches, and the rail's `CLAUDE.md` now marks startup DDL as interim. Second, the owner directed on 2026-09-07 that the platform builds **both** idioms behind a decoupled persistence boundary and selects one later, with the code for both in hand.

**Schema.** Rule 3 (startup-owned schema) is replaced: **schema is owned by a per-domain Database project through EF Core migrations.** Each domain carries one `<Domain>.Database` project holding the `DbContext` model of that domain's tables and their migrations; partition functions, schemes and grants are raw SQL inside migrations. The domain's pipeline runs the migrations bundle against an environment before that environment's code rolls out, under a deployer identity (Phase 4). A host checks for pending migrations at startup: in the Development environment it applies them; anywhere else it refuses to start and names the bundle. Test fixtures build every test database with `MigrateAsync()`, so a Dapper query naming a column the model lacks fails in CI, which is the managed cost of the model being a second description of the tables.

**Runtime data access.** Persistence is decoupled by construction: the Application layer depends only on store ports (ADR-005), and **each store port receives two implementations, one Dapper and one EF Core, while the selection is open.** Both implementations pass the same port contract tests (the integration tier runs each contract against both), the host selects by dependency-injection registration, and the eventual selection is recorded as an addendum here once enough module code exists to compare them fairly. The walking skeleton's consumed-message store is the first port built this way.

**What stands regardless of the selection**: `SqlBulkCopy` (or a table-valued parameter) owns bulk paths; multi-row `VALUES` batching stays banned on wide rows; every temporal parameter is explicitly typed, through one `SqlTemporal` helper (the rail's A17 form) on the Dapper side and typed column mappings on the EF side; the unit of work is one transaction per message scope with the outbox insert joining it; and the benchmark of 2026-09-07 remains the standing performance evidence (Dapper 1.6 times faster on the consumer upsert path, both meeting targets).

## Addendum, 2026-09-16: one store idiom

The dual-implementation rule is withdrawn. Every store has one implementation, on Dapper over
`Microsoft.Data.SqlClient`; EF Core keeps the schema model and generates the migrations and does nothing at
runtime. The first module written both ways (Market Messaging's inbound index) showed the second
implementation carrying no information the first did not, at twice the test surface. A store opens a
connection per call unless a unit of work owns the transaction; identifiers bind as `varchar` so indexes
are used; every temporal parameter is typed through `BuildingBlocks.Sql.SqlTemporal`.


## Addendum, 2026-09-22: DbUp owns the schema; EF Core leaves the repository

The schema is one set of SQL scripts, embedded in the project that owns the tables and applied by DbUp
through a `SchemaVersions` journal. EF Core is removed from every project: no `DbContext`, no model
snapshot, no migrations, no design-time factory, no tool manifest. The platform tables are one script in
`BuildingBlocks.Persistence` that every module's schema run applies ahead of the module's own scripts,
which replaces `modelBuilder.ApplyPlatformTables()`. A deployed environment gets the schema from a
per-domain migrator executable the pipeline runs before the hosts roll; a host still applies pending
scripts in Development and still refuses to start elsewhere while any script is unapplied. Test
fixtures build their databases with the same runner, so no suite describes a table for itself.

The revisit trigger named in the original decision fired early, on two findings rather than on the
workflow-heavy module it anticipated. First, the model owned one of the four tables in the estate; the
other three were hand-written `migrationBuilder.CreateTable` calls, so the guard the model was kept for
covered a quarter of the schema. Second, the migrations as they stood built a database with one table:
the `Up` of `ReplaceInboundMessageWithInboundMarketMessage` dropped the outbox, the inbox and the
client-request log and only its `Down` recreated them, and no suite noticed because the building-blocks
suites described those tables themselves in a test-only context. The estate was four tables, no
environment held the schema, and nothing outside the repository read it, so the move cost one script per
owner and no reconciliation.

What this does not change: Dapper over `Microsoft.Data.SqlClient` remains the single store idiom,
`SqlBulkCopy` owns bulk paths, wide-row multi-row `VALUES` batching stays banned, every temporal
parameter is typed through `BuildingBlocks.Sql.SqlTemporal`, and the unit of work is one transaction per
message scope with the outbox insert joining it.

## Addendum, 2026-09-23: scripts are journalled by file name

DbUp decides a script has run by its journal name alone, and an embedded script's own name is its resource
path, folders included, so moving a script folder made every database run its scripts again.
`SchemaMigrator` now journals each script by its file name, and every file carries its owner's prefix,
the domain and the module for a module's scripts (`Metr-MarketMessaging-00001 - Inbound market message.sql`,
`Platform-00001 - Platform tables.sql`). The prefix keeps
modules apart in the one journal a domain's database shares; a module passes it to the migrator, which
refuses a script without it before connecting. A script's file name is now part of what never changes.
