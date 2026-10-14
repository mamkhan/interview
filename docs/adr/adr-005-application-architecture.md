# ADR-005: Application Architecture Conventions

Status: Accepted (review of 2026-09-07)
Date: 2026-09-07
Evidence: the proof of concept's layering and the `tdd` skill (the house practice); the reference codebase's `SeedWork` and `AggregatesModel` conventions; the build baseline (aggregates and invariants per bounded context)

## Context

The platform is built with DDD and test-first development as working methods, and the user-stated principles for the foundation are industry standards, clean code, SOLID, KISS, DDD and CQRS. The rail already practises a layering that the house TDD skill depends on; the reference codebase contributes the seedwork. This ADR fixes the per-module conventions so every module reads the same way.

## Decision

**Layering per module** (enforced by architecture tests, ADR-001 rule 5):

- `<Module>.Domain`: aggregates, entities, value objects, domain events, domain services, repository interfaces where the idiom needs them. Pure: no I/O, no transport, no SQL, references only the seedwork. Aggregates guard the invariants the build baseline names for their context; the `AggregatesModel/<Name>Aggregate/` folder convention from the reference codebase applies.
- `<Module>.Application`: use-case handlers orchestrating domain operations, one class per use case; the interfaces the use cases need from the outside world (clock, bus, stores) defined here or in Domain; no framework types in signatures.
- `<Module>.Infrastructure`: repository implementations, Dapper SQL (ADR-003), transport adapters, third-party integrations.
- `<Module>.Host` (or a shared domain host per ADR-002): composition root only; wiring, configuration binding, hosted-service registration. No logic.

**CQRS as command and query separation, not as a framework.** Commands and queries are separate handler classes in Application; reads go to projections and read models, writes go through aggregates; projections are rebuilt from the ledger or from events, never written by anything except their projector (the rail's single-SQL-writer rule). Event sourcing applies exactly where the baseline says it does (the two books of record); nothing else adopts it by default.

**Handlers are plain classes.** No mediator library: the rail delivers with direct handler invocation, dependencies flow through constructor injection, and a mediator adds indirection with no current consumer. The named trigger to reconsider: a genuine cross-cutting pipeline need (validation, authorisation, transaction behaviours) appearing in three or more modules; the reconsideration is an addendum here, and the reference codebase's MediatR wiring is the model if adopted.

**Domain events** are raised by aggregates and dispatched in-process within the unit of work before commit (ADR-003 rule 5); an event with consumers outside the module is published through the outbox as a contract event (ADR-004 rules 2 and 5).

**Seedwork** lives in `building-blocks/`: `Entity`, `ValueObject`, `AggregateRoot` (with domain-event collection), `IDomainEvent`. Repository and unit-of-work abstractions enter only when a second implementation needs the seam, per ADR-003.

**Design style**: deep modules behind small interfaces (the `tdd` skill's interface-design guidance is the reference); KISS as the tie-breaker between designs; a class or interface introduced without a current consumer is a review finding. Composition surfaces (viewers, future portals) hold no source-of-truth state.

## Consequences

- Every module looks the same: a new engineer or agent orients by convention, and the Phase 3 module recipe generates this exact structure.
- The classicist TDD workflow (ADR-007) maps directly onto the layering: Domain unit tests with real objects, Application unit tests with stubbed boundaries, Infrastructure covered by integration tests.
- Choosing plain handlers over a mediator is reversible by the written trigger, and choosing it now keeps stack traces and navigation direct while the team forms.
- Refusing default event sourcing outside the ledgers keeps most modules simple state-plus-events; a module believing it needs an event store argues from its invariants at review.

## Addendum, 2026-09-11: the pipeline trigger fired; the command runner

The trigger written above (a cross-cutting pipeline need in three or more modules) is now certain: the
consumption ledger, rating, the billing ledger and payments each need validation, a transaction,
idempotency and domain-event dispatch around every state-changing handler. The choice, taken with the
platform owner's guide ("the module starter kit") and the reference codebase's `TransactionBehavior`,
`ValidatorBehavior` and `IdentifiedCommandHandler` as the reference for what the pipeline must do:

1. **An explicit runner, not MediatR.** `BuildingBlocks.Commands.CommandRunner` does, in one readable
   method and a fixed order, what the reference codebase's behaviours do implicitly: validate, begin the scoped unit
   of work, guard idempotency, run `ICommandHandler<TCommand, TOutcome>`, dispatch domain events, commit.
   The reasons this ADR gave for plain handlers stand (direct stack traces and navigation while the team
   forms), vertical slices favour explicit composition, and MediatR moved to a commercial licence in 2025,
   which made adopting it a purchasing decision as well. The reference codebase's wiring remains the model for
   behaviour, not for shape.
2. **FluentValidation** (Apache 2.0) for command validators, one class per command in its slice, executed
   by the runner before the transaction begins. A failure is `CommandValidationException`, deterministic.
3. **Outcomes, not exceptions, for expected business results.** A handler returns a typed outcome
   (`IndexOutcome.AlreadyIndexed`); it throws `DomainException` only for an invariant the domain refuses.
   Both deterministic exceptions dead-letter on first delivery and map to 400 and 422 through
   `PlatformExceptionHandler` in ServiceDefaults; anything else is a bug and propagates.
4. **Repository as a store per aggregate on the unit of work.** The seam ADR-003 as amended already
   requires (a Dapper and an EF Core implementation of every store port) is the repository; it lives in
   the module's `Persistence/` when two slices share it, and takes `IUnitOfWork` by injection so both
   idioms commit in the same transaction. `LayeringTests` forbids SQL outside `Persistence/`.
5. **Domain events dispatched by the unit of work before commit** to `IDomainEventSubscriber<TEvent>`
   implementations the module registers explicitly (no assembly scanning), looping until no subscriber
   raises a further event; a contract event is produced by such a subscriber as an outbox row.
6. **Transport separated from reaction.** A slice implements `IEventSubscriber<TEvent>`;
   `MessageConsumer<TEvent>` is the Service Bus adapter that opens a DI scope per delivery and resolves it,
   so a future in-process transport calls the same subscriber (ADR-004 addendum).

Names: the interfaces are `IEventSubscriber` and `IDomainEventSubscriber` rather than `...Handler`
because the .NET analyser reserves the `EventHandler` suffix for delegates (CA1711).

## Addendum, 2026-09-23: event handlers, event consumers and repositories

1. **The names say the role.** A slice's reaction to an event is `<Action>EventHandler : IEventHandler<TEvent>`
   and its Service Bus adapter is `<Action>EventConsumer : EventConsumer<TEvent>`; the domain-event
   interface is `IDomainEventHandler<TEvent>`. They replace `IEventSubscriber`, `MessageConsumer` and
   `IDomainEventSubscriber`. CA1711 still reserves the `EventHandler` suffix for delegates; the repository
   declares no event delegates, so `.editorconfig` allows the suffix (`dotnet_code_quality.CA1711.allowed_suffixes`).
   A subscription is named after the handler that reads it (`metr.record-receipt`).
2. **A repository, not a store.** The type that owns an aggregate's or a row's SQL is `<Name>Repository` and
   lives in the slice that uses it. `LayeringTests` allows SQL only in a `...Repository` type (and the
   compiler-generated types nested in it) rather than in a `Persistence/` namespace, and forbids a slice to
   depend on another slice.

## Addendum, 2026-09-23: handlers named for feature and event

An event handler is `<Feature><Event>EventHandler` and its consumer `<Feature><Event>EventConsumer`
(`AuditMarketMessageReceivedEventHandler`), replacing the `<Action>EventHandler` names of the addendum
above. The event alone is not enough: two features of one module can handle the same event, and Service
Bus refuses two subscriptions with one name on one topic. A handler handles one event, so a reaction to two
events is two handlers sharing a class; the subscription or queue is `{domain}.{feature}-{event}`.
