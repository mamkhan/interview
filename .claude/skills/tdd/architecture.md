# Architecture — module layout and enablers

The classicist style is cheap because the architecture cooperates. Adopting the test conventions without these enablers will make tests fight you, and the team will drift back to mock-heavy interaction tests.

## Module layout — per bounded context

| Module | Role |
|---|---|
| `Domain` | Aggregates, entities, value objects, repository **interfaces**, domain events. Pure code, no I/O, no framework dependencies. |
| `Application` | Use-case / command / query handlers. Hand-rolled (no magical mediator); explicit dependencies in constructors. |
| `Infrastructure` | Repository implementations, transport (HTTP/queue/etc.) adapters, third-party clients, dependency-injection wiring. |
| `Domain.UnitTests` | Aggregate-level tests using **only real domain objects** (no stubs at all). |
| `UnitTests` | Application + Infrastructure unit tests. Cross-boundary collaborators stubbed; domain objects real. |
| `IntegrationTests` | Real DB, real external services, exercising a vertical slice. |
| `Tests.Shared` | Builders, personas, base classes, helpers. Referenced by every test module of the bounded context. |

Cross-cutting test infrastructure (auth personas, transport-context builders, base classes for logging) lives in a **repository-wide** shared test module so multiple bounded contexts reuse it without duplication.

> **Replication takeaway:** create the `Tests.Shared` module on day one, even if empty. Without it, builders get duplicated, then drift, then become a maintenance tax.

## The eight architectural enablers

1. **Domain objects are pure.** No I/O, no framework dependencies, no async, no DI containers reaching into them. An aggregate is just data and behaviour.
2. **Repository interfaces live in `Domain`; implementations live in `Infrastructure`.** Non-negotiable. This is what lets unit tests stub the interface without spinning up the database.
3. **Handlers list every dependency in the constructor.** No service locator, no static state, no thread-locals. The constructor IS the test plan.
4. **Time is an injected abstraction** (a function, a clock interface — whatever fits the language). Never read the wall clock inline inside a handler or aggregate.
5. **Domain operations return a result type**, not throw. Tests assert on `.Success` and content; control flow stays explicit; failure paths are first-class.
6. **Transactions are an injected abstraction** (`unitOfWork.BeginScope() → SaveChanges()`, or equivalent). Trivial to stub; explicit in production.
7. **Domain events are exposed on the aggregate**, not dispatched eagerly. Tests assert on the events collection directly. Dispatch happens at the persistence boundary.
8. **DI registration is co-located with the bounded context** so test bootstrappers can compose registrations selectively (real DB for one slice, stubs for another).

> **Replication takeaway:** if you're starting a greenfield project, *invest in items 1, 3, 4, 5 first*. They are the foundation. Items 2, 6, 7, 8 follow naturally.

## Why these eight together

Each enabler removes a specific reason teams reach for mocks:

| Without the enabler | The test pressure that pushes toward mocks |
|---|---|
| Domain not pure | Have to mock a database/HTTP client inside an aggregate test |
| Repo interfaces in Infrastructure | Can't stub persistence without circular references |
| Hidden dependencies | Have to set up a DI container or service locator in tests |
| Wall-clock reads | Tests are non-deterministic, so people freeze time with magic |
| Exceptions for expected failures | Tests need try/catch noise, can't assert on a result object |
| Implicit transactions | Tests boot the ORM just to verify a save happened |
| Eagerly dispatched events | Have to mock the event bus to verify a domain event |
| Global DI registration | Can't compose a "real DB + stubbed transport" test rig |

When all eight are in place, the natural test shape is: real domain object + stubbed cross-boundary collaborators + state-based assertion. No fight.
