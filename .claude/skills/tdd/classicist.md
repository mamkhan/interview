# Classicist mechanics — what's real, what's a double, how to assert

This is the discipline that separates this house's TDD from generic "I write tests first." Get this right and the rest follows; get it wrong and you'll drift into mockist tests that break on every refactor.

## What is real, what is a test double

| Collaborator | Real or double? |
|---|---|
| Aggregates / entities / value objects | **Real**, always |
| Result objects, validation summaries, exposed domain events | **Real** (asserted on directly) |
| Repositories | **Stubbed** via interface |
| Unit-of-work / transaction scope | **Stubbed** |
| Clock / time provider | **Stubbed** with a fixed instant |
| Auth context / current-user provider | **Built** with a Persona — real-shaped principal & claims |
| External services (cloud SDKs, third-party APIs) | **Stubbed** in unit tests, **real** in integration tests |
| Database | **Stubbed via repo interface** in unit tests; **real** in integration tests (no in-memory fakes) |

## The hard rule

**Never stub a domain object.**

If the design pushes you to mock an aggregate to write a test, the design has leaked I/O into the domain — fix the design, not the test. Common fixes:

- The aggregate is calling out to a repository → move that orchestration into the handler.
- The aggregate reads the wall clock → inject a clock into the operation.
- The aggregate dispatches an event eagerly → expose events on a collection; dispatch at the persistence boundary.

## State-based assertions dominate

Tests inspect:

- Aggregate state after the operation
- Returned result objects
- Exposed domain-event collections on the aggregate
- Validation summaries

Not:

- Whether method A on collaborator B was called.

```
// State on aggregate
order.Items.Should().HaveCount(3)
order.Status.Should().Be(Submitted)
order.DomainEvents.Should().Contain(e => e is OrderSubmitted)

// Result object
result.Success.Should().BeTrue()
result.Total.Should().Be(expectedTotal)

// Validation summary
result.Errors.Should().Contain(e => e.Code == "CART_EMPTY")
```

## Interaction verification is surgical

Used **only** at the persistence boundary — when the side effect *is* the contract — and **always** paired with a state predicate so the assertion is still about what the aggregate looks like:

```
repository.Received().Save(matchAggregate(o =>
    o.Id == expectedId &&
    o.Status == Submitted &&
    o.Total == expectedTotal))
```

The negative case uses `DidNotReceive()`:

```
repository.DidNotReceive().Save(any())
```

> **Rule of thumb to teach a new team:** *Assert on state. Use interaction verification only at the persistence boundary, and always pair it with a state predicate.*

If you find yourself writing `mock.Verify(x => x.Method(...))` anywhere except at a save/update/delete boundary, something is wrong — usually the production code has hidden a piece of behaviour behind a collaborator that should be exposed as state on a domain object instead.

## A worked example end to end

```
class WhenSubmittingAnOrder {
    SubmitOrderHandler handler
    IOrderRepository repository = stub()
    IUnitOfWork uow = stub()
    IClock clock = stub returning Instant("2026-04-27T12:00:00Z")

    constructor() {
        handler = new SubmitOrderHandler(repository, uow, clock)
    }

    test Persists() {
        // Arrange
        order = new OrderBuilder()
            .WithCustomer(new CustomerBuilder().Build())
            .WithItem(sku: "ABC-123", quantity: 2)
            .Build()
        repository.GetById(order.Id).returns(order)

        // Act
        result = handler.Handle(new SubmitOrderCommand(order.Id))

        // Assert (state)
        result.Success.Should().BeTrue()
        order.Status.Should().Be(Submitted)
        order.SubmittedAt.Should().Be(Instant("2026-04-27T12:00:00Z"))
        order.DomainEvents.Should().Contain(e => e is OrderSubmitted)

        // Assert (persistence boundary, paired with state predicate)
        repository.Received().Save(matchAggregate(o =>
            o.Id == order.Id &&
            o.Status == Submitted))
    }

    test FailsIfTheCartIsEmpty() {
        order = new OrderBuilder().WithNoItems().Build()
        repository.GetById(order.Id).returns(order)

        result = handler.Handle(new SubmitOrderCommand(order.Id))

        result.Success.Should().BeFalse()
        result.Errors.Should().Contain(e => e.Code == "CART_EMPTY")
        order.Status.Should().Be(Draft)
        repository.DidNotReceive().Save(any())
    }
}
```

Read the method names: `Persists`, `FailsIfTheCartIsEmpty`. The class reads as a feature spec.
