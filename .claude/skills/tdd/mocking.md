# When to mock — and the hard rule

## The hard rule

**Never stub a domain object.**

Aggregates, entities, and value objects are always real. If the design is forcing you to mock one, the design has leaked I/O into the domain — fix the design, not the test. See [classicist.md](classicist.md) for what's real vs what's a double in full.

## What to stub (the short version)

The full table lives in [classicist.md](classicist.md). The short version:

- **Stub** the things you own *only at boundaries* you don't want to cross in this test: repositories, unit-of-work, clock, message bus, transport, third-party SDK clients.
- **Don't stub** internal collaborators that are part of the domain or part of the same operation. If `Order.Submit()` calls `Order.RecalculateTotal()`, neither is a stub target — they're part of the same aggregate.
- **Don't stub** anything in `Domain.UnitTests`. That module exists precisely to verify aggregate behaviour with no doubles at all.

## Designing for mockability

At the boundaries that *are* legitimately stubbed, the production-code design has to cooperate. See also [architecture.md](architecture.md) for the full enabler list.

### 1. Pass dependencies in; don't construct them inside

```
// Easy to stub
class SubmitOrderHandler {
    constructor(IOrderRepository repository, IUnitOfWork uow, IClock clock) { ... }
    Handle(SubmitOrderCommand command) { ... }
}

// Hard to stub
class SubmitOrderHandler {
    Handle(SubmitOrderCommand command) {
        repository = new SqlOrderRepository(connectionString)   // ❌
        ...
    }
}
```

The constructor IS the test plan. If a dependency isn't in the constructor, it can't be stubbed.

### 2. Prefer specific interfaces over generic fetchers

```
// GOOD: each method is independently stubbable
interface IBillingApi {
    Subscription GetSubscription(CustomerId id)
    Invoice[] GetInvoices(CustomerId id)
    void RetryPayment(InvoiceId id)
}

// BAD: stubbing requires conditional logic inside the stub
interface IBillingApi {
    object Call(string endpoint, object body)
}
```

Specific interfaces mean each stub returns one specific shape, no conditional logic in test setup, and you can see at a glance which boundary calls a test exercises.

### 3. Inject time, never read the wall clock inline

```
// GOOD
class SubmitOrderHandler {
    constructor(IClock clock) { ... }
    Handle(...) { now = _clock.Now(); ... }
}

// BAD
class SubmitOrderHandler {
    Handle(...) { now = SystemClock.Now(); ... }   // ❌ non-deterministic
}
```

Injected time is the difference between deterministic, fast unit tests and flaky tests that fail at midnight.

### 4. Don't stub things you don't own — wrap them

If a third-party SDK exposes a sprawling surface, write a thin domain-shaped interface in your `Domain` layer (`IPaymentGateway`, `IEmailSender`) and have the `Infrastructure` implementation translate. Then stub *your* interface, not the SDK. This keeps the SDK out of `Domain` and out of unit tests.
