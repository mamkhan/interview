# Good and bad tests — examples

The point of every example below: **state-based assertions on real domain objects, with cross-boundary collaborators stubbed**. See [classicist.md](classicist.md) for the rules; this page shows them in practice.

## Good test — state-based, scenario-named

```
class WhenCheckingOutACart {
    CheckoutHandler handler
    ICartRepository carts = stub()
    IPaymentGateway payments = stub()
    IUnitOfWork uow = stub()
    IClock clock = stub returning Instant("2026-04-27T12:00:00Z")

    constructor() {
        handler = new CheckoutHandler(carts, payments, uow, clock)
    }

    test ConfirmsTheOrder() {
        cart = new CartBuilder()
            .WithItem(sku: "ABC-123", quantity: 1)
            .Build()
        carts.GetById(cart.Id).returns(cart)
        payments.Charge(any()).returns(PaymentResult.Approved("auth-1"))

        result = handler.Handle(new CheckoutCommand(cart.Id, validPaymentMethod))

        result.Success.Should().BeTrue()
        result.Status.Should().Be(Confirmed)
        cart.DomainEvents.Should().Contain(e => e is CartCheckedOut)
    }
}
```

Characteristics:

- Class name describes the scenario (`WhenCheckingOutACart`).
- Method name describes the outcome (`ConfirmsTheOrder`).
- Domain objects (`Cart`) are real — built via `CartBuilder`.
- Cross-boundary collaborators (`ICartRepository`, `IPaymentGateway`, `IUnitOfWork`, `IClock`) are stubbed.
- Assertions inspect the result object, the aggregate, and the exposed event collection.
- Survives an internal refactor that doesn't change observable behaviour.

## Bad test — interaction-coupled to internal collaborator

```
test CheckoutCallsPaymentServiceProcess() {
    payments = mock()

    handler.Handle(new CheckoutCommand(cart.Id, payment))

    payments.Verify(x => x.Process(cart.Total))   // ❌ wrong
}
```

Red flags:

- Asserts a method was called — that's an implementation detail.
- No state predicate.
- The test name describes *how* the code works, not *what* it does.
- Rename `Process` to `Charge` and the test breaks even though behaviour didn't.

## Bad test — bypassing the interface to verify

```
test CreateUserSavesToDatabase() {
    handler.Handle(new CreateUserCommand(name: "Alice"))

    row = db.Query("SELECT * FROM users WHERE name = $1", "Alice")  // ❌ in a unit test
    row.Should().NotBeNull()
}
```

Red flags:

- Reaches around the public surface to check the database directly.
- Mixes layers: the unit test is now coupled to schema and SQL.
- If this is meaningful behaviour to verify, write it as an **integration test** in `IntegrationTests` — there, querying the real DB is correct because the DB *is* the system being tested. See [unit-vs-integration.md](unit-vs-integration.md).

## Bad test — expected value computed with the implementation's own arithmetic

```
test ComputesTheDailyForecast() {
    result = calculator.Compute(meter)

    expected = ExpectedHelper.Day(weights, factor, profile)   // ❌ replicates the calc
    result.Days[0].Kwh.Should().Be(expected)
}
```

Red flags:

- `ExpectedHelper.Day` applies the same rounding, accumulation order, and profile branching as the
  production code — it's a second copy of the implementation, not an independent check.
- Implement the rounding wrong and BOTH sides compute the same wrong number: the test passes.
- The expectation was derived FROM the code under test instead of driving it.

Fix: derive the value once, offline (hand-calc, throwaway script, or a trusted external source such
as the legacy system's real output), and freeze it as a literal:

```
test ComputesTheDailyForecast() {
    result = calculator.Compute(meter)

    // Frozen literal: LP1 anchor-day weights x PUF 4454, 4dp/hour — derived offline 2026-07-08.
    result.Days[0].Kwh.Should().Be(11.0372)
}
```

## Good test — at the persistence boundary

The persistence boundary is the one place interaction verification belongs, and even there it's paired with a state predicate:

```
test Persists() {
    order = new OrderBuilder().WithItem(sku: "ABC-123", quantity: 2).Build()
    repository.GetById(order.Id).returns(order)

    result = handler.Handle(new SubmitOrderCommand(order.Id))

    // State assertions
    result.Success.Should().BeTrue()
    order.Status.Should().Be(Submitted)
    order.DomainEvents.Should().Contain(e => e is OrderSubmitted)

    // Persistence boundary: paired with a state predicate
    repository.Received().Save(matchAggregate(o =>
        o.Id == order.Id &&
        o.Status == Submitted))
}
```

The negative case uses `DidNotReceive()`:

```
test FailsIfTheCartIsEmpty() {
    order = new OrderBuilder().WithNoItems().Build()
    repository.GetById(order.Id).returns(order)

    result = handler.Handle(new SubmitOrderCommand(order.Id))

    result.Success.Should().BeFalse()
    result.Errors.Should().Contain(e => e.Code == "CART_EMPTY")
    repository.DidNotReceive().Save(any())
}
```

## A note on assertion shape

One logical assertion per test, but "logical" can include several `.Should()` calls that together describe **one outcome**. The `Persists()` test above is one logical assertion: *the submitted order ends up persisted in the right shape*. Splitting it into `PersistsTheOrder()` / `MarksItSubmitted()` / `RaisesTheEvent()` would fragment the outcome and force the same setup three times. Keep them together when they form one observation.
