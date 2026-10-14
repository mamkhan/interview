# ADR-004: Messaging and Consistency

Status: Accepted (review of 2026-09-07, amended at review: correlation, logging, observability and retry made explicit, §Observability below)
Date: 2026-09-07
Evidence: the messaging model of the proof of concept, validated at full daily ingest volume; the consistency rules of the non-functional register; the reference codebase's `IntegrationEventLogDapper`

## Context

Modules and domains interact without sharing databases (ADR-001 rule 2). The rail validated a complete messaging model in production conditions; the NFR register states the consistency rules. This ADR fixes the transport, the delivery semantics, and the one addition the modular monolith needs that the rail did not: an outbox for the places where a state write and a publish must not diverge.

## Decision

1. **Azure Service Bus topics are the backbone for events and commands between modules and between domains**, including modules co-hosted in the same process (ADR-002). Co-hosted modules do not shortcut through in-process calls: the bus is what makes consolidation and splitting a packaging choice rather than a rewrite, and it carries the durability, retry and dead-letter semantics the platform depends on.
2. **Within one module**, domain events raised by aggregates are handled in-process, inside the same unit of work (ADR-003 rule 5). An in-process domain event that must be visible outside the module becomes a published contract event through the outbox.
3. **Delivery is at-least-once; every consumer is idempotent; acknowledgement is last.** Idempotency comes from hash-named overwrite-identical writes, upserts by key, or a processed-marker row written in the same transaction as the work.
4. **Ordering only where a key requires it**: session-enabled subscriptions with the ordering key as session id (MPRN for consumption; account where billing requires it). Every publisher to a session-enabled topic stamps the key. Everything else runs unordered and parallel.
5. **Atomic state-plus-publish uses an outbox**: the event row is written in the same transaction as the state change and relayed to Service Bus afterwards; the relay marks rows through the published-state machine. The reference codebase's `IntegrationEventLogDapper` service is adapted into `building-blocks/` with its reflection-based type discovery replaced by explicit registration. Paths that are already idempotent-by-construction against re-publish (the rail's hash-named ledger writes) do not need it; the outbox is for read-modify-write state.
6. **Failures poison out, not loop**: bounded delivery attempts end in the subscription's dead-letter queue, which is a monitored operational work list (alerting per Phase 4). Nothing is dropped silently. This is the interim Exceptions & Workflow mechanism; the shared machinery is a later decision.
7. **The event envelope** carries an event identifier, the publishing context, a timestamp and a correlation identifier on every event; records crossing a data contract also carry the identity tags `brand`, `fuel`, `market`, `producer` as lowercase hyphenated values. The envelope is a base type in `contracts/`.
8. **Contracts are code, per pipeline, named from the event catalogue.** Additive evolution only: new optional fields from a cutover date, no breaking renames without a versioned sibling topic. A central schema registry stays deferred until more than one team consumes another team's events in anger.
9. **Blob-arrival and system triggers use Event Grid; data products leave the operational estate only through an Event Hubs relay** so a throttled consumer can never slow a pipeline.
10. **Consistency between modules is eventual**, per the register: local read models built from subscribed events, two calendars never mixed, append-only books with corrections as new entries, sagas (choreography for linear pipelines, orchestration with explicit state machines and compensation for long-running workflows; saga state has exactly one owner).

## Observability, correlation and retry (added at the review of 2026-09-07)

11. **Correlation is end to end.** A workflow's entry point (inbound market file, user command, scheduled request) mints the correlation identifier; every consumer copies the inbound correlation identifier onto everything it writes and every event it publishes, so one identifier traces a file from capture to projection to any downstream effect. W3C trace context (`traceparent`) is propagated across Service Bus hops in message application properties (the Azure SDK's `Diagnostic-Id` convention), so distributed traces stitch across processes without custom code.
12. **Logging is structured, with correlation in scope.** Hosts log through `ILogger` with the correlation identifier and the key business identifiers (MPRN, account, message type) as scope properties, never interpolated into message text; logs, traces and metrics flow through OpenTelemetry to Application Insights and Log Analytics, wired once in `ServiceDefaults` (`building-blocks/`). Every host exposes health checks.
13. **Per-message telemetry is standard.** Each consumer records a handling span per message (queue wait and handling duration, as the rail's per-hop timing columns do today), and dead-letter depth, delivery-attempt counts and handler failure rates are first-class metrics on the Phase 4 dashboard with the dead-letter alert.
14. **Retry has a taxonomy, not a habit.**
    - **Transient dependency faults** (SQL, storage, HTTP): bounded in-process retries with exponential backoff and jitter through the standard resilience pipelines in `building-blocks/` (`Microsoft.Extensions.Http.Resilience` for HTTP; the SqlClient configurable retry provider for SQL); never hand-rolled retry loops.
    - **Message-handling failure**: the handler does not catch-and-swallow; the message is abandoned and Service Bus redelivery is the retry, bounded by the subscription's `MaxDeliveryCount`.
    - **Deterministic failures** (deserialisation, schema violation, validation that can never pass): dead-letter immediately with the reason recorded, rather than burning redeliveries.
    - **Exhausted redelivery** lands in the dead-letter queue per rule 6.
    - Retries at every level are safe because consumers are idempotent (rule 3), and scheduled work cannot drift on retry because handlers never read their own clock (NFR register rule 9).

## Consequences

- Consolidating or splitting hosts (ADR-002) never changes messaging semantics, which is what keeps the "domains choose their deployment" guideline cheap to exercise.
- The bus on intra-domain hops costs latency measured in milliseconds against the register targets and buys replay, audit and back-pressure; the rail already runs this way at full volume.
- The outbox is new code for the platform (adapted, not invented) and becomes part of the module recipe wherever a handler mutates read-modify-write state and publishes.
- Dead-letter depth is an operational surface from day one: Phase 4 wires the dashboard and one alert rule before the first real workload lands.

## Addendum, 2026-09-11: the inbox, client requests and the transport seam

1. **Rule 3 gains a general mechanism.** Where a consumer has no natural idempotency key (an upsert by
   content hash, a session per key), the inbox is the guard: `dbo.ProcessedMessage(Consumer, MessageId)`
   written in the same transaction as the work by `CommandRunner.RunOnceAsync`. A redelivery after commit
   is a no-op; a redelivery after a failure runs again because the marker rolled back with the work.
   `Consumer` is the handler type, so two slices consuming the same event keep separate markers.
2. **HTTP commands carry `x-requestid`.** `dbo.ClientRequest(Id, Name, ResultJson)` under
   `CommandRunner.RunIdempotentAsync`: the first call records the request and its outcome in the
   transaction; a repeat returns the recorded outcome without running the handler; a concurrent duplicate
   waits on the row lock until the first commits or rolls back. The reference codebase's `IdentifiedCommand` pattern,
   with the result recorded rather than a canned answer.
3. **Deterministic failures are two named types.** `CommandValidationException` and `DomainException`
   dead-letter on first delivery (the same bytes will fail the same way); everything else is abandoned
   for bounded redelivery as before.
4. **The transport is an adapter.** `MessageConsumer<TEvent>` opens a DI scope per delivery and resolves
   the slice's `IEventSubscriber<TEvent>`; the scope is where the unit of work, the stores and the
   runner live for exactly one message. Intra-domain hops stay on Service Bus (the consequences above
   still hold); an in-process transport, should a measured hop ever justify it, would resolve the same
   subscriber and change no slice.
5. **The three platform tables travel with every module.** Outbox, inbox and client requests come from
   one script in `BuildingBlocks.Persistence`, applied in the domain's database ahead of the module's own
   scripts every time the module's schema is brought up (amended 2026-09-22 by ADR-003's third addendum;
   they were an EF model fragment before).

## Addendum, 2026-09-16: the consumer's clock and deterministic processing outcomes

1. **A consumer's clock is the message's `AsOf` when the envelope carries one and the system clock
   otherwise.** The transport adapter stamps the scoped clock before the subscriber is resolved; handlers
   inject `TimeProvider` and never read the wall clock (a build gate bans `DateTime.UtcNow`,
   `DateTimeOffset.UtcNow`, `DateTime.Now`, `DateTimeOffset.Now` and `Guid.NewGuid()` under `Modules/`).
   Nothing sets `AsOf` today; the field and the gate are what make a future replay a tool rather than a
   refactor.
2. **Deterministic processing failures are published outcomes, not dead letters.** A file that cannot be
   classified, or that its handler refuses, is recorded as `failed` with the reason, the failure is
   published for other subscribers, and the message completes. The dead-letter queue holds transient
   exhaustion only, so its depth is an alert on infrastructure, not on data. Rule 14's "deterministic
   failures dead-letter immediately" still applies to a message that cannot be deserialised or that fails
   validation before any work starts.

