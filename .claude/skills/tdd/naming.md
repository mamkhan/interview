# Naming — `When<scenario>` classes and outcome-named methods

This is the single most copyable artifact of the house TDD model. It forces tests to read as specifications and lets new joiners navigate the suite within a day.

## Class = scenario, named `When<scenario>`

The class name reads as the situation being exercised. Examples:

- `WhenAddingAnItemToTheCart`
- `WhenSubmittingAnExpiredCoupon`
- `WhenPromotingAUser`
- `WhenReconcilingAFailedPayment`
- `WhenSubmittingAnEmptyCart`

A class corresponds to **one scenario**, not one production class. `WhenSubmittingAnOrder` may exercise `SubmitOrderHandler`, `Order`, `OrderRepository` — whatever the scenario touches.

## Constructor wires the SUT once

Every collaborator is created and connected in the test class constructor. The constructor IS the scenario's setup.

```
class WhenSubmittingAnOrder {
    SUT _handler
    IRepository _repository = stub()
    IClock _clock = stub returning fixed now
    IUnitOfWork _uow = stub()

    constructor() {
        _handler = new SubmitOrderHandler(_repository, _uow, _clock)
    }

    test SubmitsTheOrder() { ... }
    test EmitsAnOrderSubmittedEvent() { ... }
    test FailsIfTheCartIsEmpty() { ... }
}
```

If the constructor is doing arrangement that's specific to one test (not the whole scenario), that test belongs in a different `When*` class.

## Setup the constructor cannot hold goes into a scenario class

Some setup does not fit the constructor: it must run once for the whole class rather than before each test (a message published, a host started, a row polled), or several `When*` classes share it. That setup goes into a **scenario class**. Asynchronous setup that runs before each test stays in the `When*` class (xUnit: the test class implements `IAsyncLifetime`).

A scenario class:

- Is named `<Situation>Scenario`, the `When*` clause in the past tense: `WhenMeterConfigurationIsPublished` uses `MeterConfigurationPublishedScenario`.
- Lives in its own file, in a `Scenarios/` folder beside the `When*` classes that use it. Its namespace follows the folder.
- Does the arrange and the act, and exposes what happened as state: the row read back, the outcome recorded. It asserts nothing; every assertion stays in the `When*` class.
- Reaches the `When*` class once per class (xUnit: `IClassFixture<TScenario>`, the scenario implementing `IAsyncLifetime`), or is created in the `When*` class's constructor when the shared setup is synchronous.

```
class MeterConfigurationPublishedScenario : runs once per class {
    Row

    initialise() {
        publish a meter configuration for a new supply point
        Row = wait for that supply point's row
    }
}

class WhenMeterConfigurationIsPublished(MeterConfigurationPublishedScenario published) {
    test StoresTheFourCodesAsSent() { published.Row.MeterConfigurationCode is "MCC01" ... }
    test RecordsTheSealedFileItCameFrom() { ... }
}
```

## Method names describe the outcome, not the test

Use present-tense verbs of what the SUT *does*:

- `Persists…`
- `Returns…`
- `Raises…`
- `Creates…`
- `Allows…`
- `Rejects…`
- `Fails…`

The reader should be able to scan a class and read the method names like a feature spec:

```
class WhenSubmittingAnOrder {
    Persists()
    EmitsAnOrderSubmittedEvent()
    ReturnsTheOrderId()
    FailsIfTheCartIsEmpty()
    RejectsAnOrderForAnInactiveCustomer()
}
```

## Forbidden patterns

- `Test_X_When_Y` — repeats `Test_` for no reason; reverses scenario/outcome.
- `Should_X_Given_Y_When_Z` — Gherkin-in-a-method-name; the class name already supplies the *when*.
- `MyMethodTest1` — describes the test, not the outcome; numbered tests are a code smell.
- `<ClassName>Tests` (e.g., `OrderHandlerTests`, `UserServiceTests`) — class-per-class instead of class-per-scenario; aggregates unrelated behaviours.

## Folder layout mirrors production

A test module's folder tree should be a structural mirror of its production module. Finding the test for a feature should never require a search.

```
src/<Feature>/<File>           ↔   tests/<Feature>/When<Scenario>
src/Web/Controllers/X          ↔   tests/Web/Controllers/When<Scenario>
src/Domain/Orders/Order        ↔   tests/Domain/Orders/When<Scenario>
```

Same path under `src/` and under `tests/<Module>/`. Scenario classes sit one level down, in `tests/<Feature>/Scenarios/<Situation>Scenario`.

## When you have many input variants

If the scenario covers many input variants with one assertion, use a parameterised test (data-driven test) inside the `When*` class. Don't make a `When*` class per row.

```
class WhenValidatingAnEmailAddress {
    [parameterised(
        "alice@example.com" -> Valid,
        "@example.com"      -> Invalid,
        "alice@"            -> Invalid,
        ""                  -> Invalid,
    )]
    test Validates(input, expected) { ... }
}
```
