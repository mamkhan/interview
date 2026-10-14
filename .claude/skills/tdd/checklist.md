# Verification checklist + drift signals

## Verification — has the model transferred?

After replication, run these checks on the target project. If all seven hold, the model has transferred:

1. **Unit suite is fast.** Runs in seconds, not minutes. If it isn't, I/O has leaked into a unit test.
2. **Test classes are short.** Open any `When*` test class — the constructor wires the SUT and stubs in roughly 30 lines, and every test method is roughly 30 lines.
3. **Wall-clock reads are zero in production code.** Grep production code for direct calls to "now" / "today" / system time — every match should go through the injected clock abstraction.
4. **No test doubles for domain objects.** Grep test code for stubs/mocks — none of the targets should be aggregates or value objects, only interfaces (repositories, unit-of-work, clock, transport).
5. **Interaction verification is rare and bounded.** Grep test code for verification calls — usage should be limited to repository save/update/delete boundaries, and each should pair with a state predicate.
6. **Builders look right.** Open any builder under `Tests.Shared` — it should have a `Build()` returning a real domain object, with sensible defaults plus `WithX(...)` overrides.
7. **Domain is dependency-free.** Pick any aggregate file — it should have **zero** imports from HTTP, persistence, cloud SDKs, or web frameworks.

## Drift signals — anti-patterns to watch for

If these appear, the team is sliding away from the model. Treat each as a **design signal first, test signal second** — the test smell is usually pointing at a production-code problem.

- A test class named after a class instead of a scenario (`UserServiceTests` instead of `WhenPromotingAUser`).
- A test method whose name describes the test instead of the outcome (`TestPromote1`, `Should_Pass_When_Valid`).
- Stubbing a domain entity to write a test.
- An aggregate that imports an HTTP client, a database driver, or a cloud SDK.
- An integration test using an in-memory database substitute.
- A handler reading the wall clock inline.
- A handler throwing an exception for an expected business-rule failure.
- A test that asserts only on `mock.Verify(x => x.Method(...))` with no state predicate.
- An expected value computed in-test (or via an `Expected*` helper) with arithmetic that mirrors the implementation — same formula, same rounding, same branching. Expected values must be frozen literals derived offline; a test that recomputes the answer with the production algorithm passes even when the algorithm is wrong.
- Test data constructed via raw constructors with positional arguments repeated across many tests (no Builder).
- Tests that fail intermittently — almost always a sign that real time, real I/O, or shared state has leaked into a unit test.

## Replication checklist for a new project

1. **Module layout**: per bounded context, create `Domain` / `Application` / `Infrastructure` / `Domain.UnitTests` / `UnitTests` / `IntegrationTests` / `Tests.Shared`.
2. **Pick the test stack**: a framework with per-test setup via class constructor; a fluent assertion library; a test-double library that does both stubbing and (sparingly) verification; logging routed to the test runner; a parameterised-test mechanism.
3. **Create the unit-test base class** that routes log output to the test runner's output channel from day one.
4. **Define an aggregate-root base type** with an exposed domain-events collection populated by a protected `RaiseEvent(...)` (or equivalent).
5. **Inject time as an abstraction** in every handler from the first commit. Never read the wall clock inline.
6. **Repository interfaces in `Domain`, implementations in `Infrastructure`.** Add as a CONTRIBUTING.md rule.
7. **Adopt a result type** for domain operations; ban exceptions for expected failure modes.
8. **Build one Builder per aggregate** before writing the second test for that aggregate.
9. **Build an auth-context builder + at least one Persona** before the first transport-level test.
10. **Test-class naming policy**: `When<scenario>` class, outcome-named methods. Add as a CONTRIBUTING.md rule.
11. **Assertion policy**: state-based by default; interaction verification only at the persistence boundary, always paired with a state predicate.
12. **Folder structure inside test modules mirrors production folders.**
13. **Integration tests as a separate test module**, runnable via its own CLI command/glob.
