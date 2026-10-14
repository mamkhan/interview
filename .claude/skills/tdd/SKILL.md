---
name: tdd
description: Test-driven development the house way — classicist (Detroit/Chicago school) red-green-refactor with When<scenario> test classes, state-based assertions, builders, and a Domain/Application/Infrastructure layered architecture. Use when the user wants to build features or fix bugs test-first, mentions "TDD" / "red-green-refactor" / "classicist", asks for integration tests, or is working in this codebase.
---

# Test-Driven Development — house practice

## Philosophy

This is **classicist (Detroit/Chicago school) TDD**, not the mockist (London school) flavour. The crucial insight: classicist TDD is not just a testing technique — it's a forcing function on how production code is shaped. The conventions in this skill should be adopted together; cherry-picking the test-naming convention without the architectural enablers will frustrate a team and they'll drift back to mock-heavy interaction tests.

**Tests act as a living specification, not a safety net bolted on after the fact.** Teams shipping under this model go faster *and* get fewer regressions. Tests describe *what* the system does, not *how* — they survive refactors because they don't care about internal structure.

## The model in one paragraph

Production code is split into clearly layered modules: a pure **Domain** layer (entities, aggregates, value objects, repository *interfaces*, domain events, no I/O), an **Application** layer (use-case handlers that orchestrate domain operations), and an **Infrastructure** layer (repository implementations, transport adapters, third-party integrations). Tests live in dedicated test modules that mirror production modules, split into **unit** (no I/O) and **integration** (real database, real external services) suites. Tests are organised by **scenario**: one scenario maps to one test class whose name reads `When<scenario>` and contains multiple focused test methods, each named after the **observable outcome**. The test class constructor wires up the system under test; **domain objects are always real**, while cross-boundary collaborators (repositories, clocks, message buses, transport) are stubbed via test doubles. Test data is constructed via fluent **Builders** kept in a shared test module. Assertions are **state-based**: tests inspect aggregate state, returned result objects, and exposed domain-event collections. Interaction verification is reserved for the persistence boundary, and even there it is paired with a state predicate. Integration tests use the real database and real external services — never in-memory substitutes.

## Anti-Pattern: Horizontal Slices

**DO NOT write all tests first, then all implementation.** This is "horizontal slicing" — treating RED as "write all tests" and GREEN as "write all code."

This produces **crap tests**:

- Tests written in bulk test _imagined_ behavior, not _actual_ behavior
- You end up testing the _shape_ of things (data structures, function signatures) rather than user-facing behavior
- Tests become insensitive to real changes — they pass when behavior breaks, fail when behavior is fine
- You outrun your headlights, committing to test structure before understanding the implementation

**Correct approach**: Vertical slices via tracer bullets. One test → one implementation → repeat. Each test responds to what you learned from the previous cycle. Because you just wrote the code, you know exactly what behavior matters and how to verify it.

```
WRONG (horizontal):
  RED:   test1, test2, test3, test4, test5
  GREEN: impl1, impl2, impl3, impl4, impl5

RIGHT (vertical):
  RED→GREEN: test1→impl1
  RED→GREEN: test2→impl2
  RED→GREEN: test3→impl3
  ...
```

## Anti-Pattern: Computed Expected Values

**Assertions must be hard-coded literals, not arithmetic that replicates the implementation.**

A test that computes its expected value with the same formula the production code uses (or with helper
methods that mirror the production pipeline — same rounding, same accumulation order, same branching
on the same flags) proves nothing: implement the formula wrong and the test computes the same wrong
number and passes. The test has stopped being an independent check and become a second copy of the
implementation.

Red flags:

- An `Expected*` helper class in the test project that takes the same inputs as the calc and applies
  rounding/branching/ordering rules matching the production code.
- Asserts like `result.Should().Be(weight * factor / divisor)` where that expression IS the
  implementation.
- Test helpers that branch on the same configuration/policy flags the production code branches on to
  decide how to compute the expectation.

Correct approach:

- Work out the expected value ONCE, outside the test run — by hand, on paper, with a throwaway
  script, or from a trusted external source (a legacy system's real output, a spec's worked example)
  — and paste it into the test as a **frozen literal**.
- In TDD this is automatic: you write the assert BEFORE the implementation exists, so there is
  nothing to replicate. The literal drives the implementation; the implementation never drives the
  literal.
- Record next to the literal where it came from (the inputs and the derivation), so it can be
  re-derived offline when a legitimate behaviour change moves it. Never "fix" a failing value test by
  regenerating the expectation from the code under test.
- Structural/derived-consistency asserts (e.g. "the daily total equals the sum of its hourly rows")
  are fine — they assert a relationship between OUTPUTS, not a re-computation from inputs. But at
  least one test in the scenario must pin the outputs themselves to literals, or the whole suite can
  drift together.

## Workflow

### 1. Planning

Before writing any code:

- [ ] Confirm with user what interface changes are needed
- [ ] Confirm with user which behaviors to test (prioritize)
- [ ] Identify which test module the work belongs in: `Domain.UnitTests`, `UnitTests`, or `IntegrationTests` — see [unit-vs-integration.md](unit-vs-integration.md)
- [ ] Decide the `When<scenario>` test class name(s) — see [naming.md](naming.md)
- [ ] Identify opportunities for [deep modules](deep-modules.md) (small interface, deep implementation)
- [ ] Design interfaces for [testability](interface-design.md)
- [ ] List the behaviors to test (not implementation steps)
- [ ] **Get user approval on the plan**

Ask: *"What should the public interface look like? Which behaviors are most important to test? Which scenario class will hold them?"*

**You can't test everything.** Confirm with the user exactly which behaviors matter most. Focus testing effort on critical paths and complex logic, not every possible edge case.

### 2. Tracer Bullet

Write ONE test that confirms ONE thing about the system:

```
RED:   Write test for first behavior → test fails
GREEN: Write minimal code to pass → test passes
```

This is your tracer bullet — proves the path works end-to-end.

### 3. Incremental Loop

For each remaining behavior:

```
RED:   Write next test → fails
GREEN: Minimal code to pass → passes
```

Rules:

- One test at a time
- Only enough code to pass current test
- Don't anticipate future tests
- Keep tests focused on observable behavior, asserting on state (see [classicist.md](classicist.md))

### 4. Refactor

After all tests pass, look for [refactor candidates](refactoring.md):

- [ ] Extract duplication
- [ ] Deepen modules (move complexity behind simple interfaces)
- [ ] Apply SOLID principles where natural
- [ ] Consider what new code reveals about existing code
- [ ] Run tests after each refactor step

**Never refactor while RED.** Get to GREEN first.

## Per-Cycle Checklist

```
[ ] Test class is named When<scenario>, not <ClassName>Tests
[ ] Setup the constructor cannot hold is a <Situation>Scenario class in Scenarios/, and it asserts nothing
[ ] Method name describes the outcome (Persists…, Returns…, Raises…, Rejects…), not the test
[ ] Domain objects (aggregates, entities, value objects) are real — no stubs
[ ] Cross-boundary collaborators (repos, clock, unit-of-work, transport) are stubbed
[ ] Assertions are state-based unless this is the persistence boundary
[ ] Expected values are hard-coded literals (derived offline), never computed in-test with the implementation's own arithmetic
[ ] If at the persistence boundary: interaction verification is paired with a state predicate
[ ] Test data comes from a Builder in Tests.Shared, not raw constructors
[ ] Test would survive an internal refactor that didn't change behavior
[ ] Code is minimal for this test; no speculative features added
```

## Where to read next

In reading order:

- [architecture.md](architecture.md) — module layout (Domain / Application / Infrastructure / test modules) and the architectural enablers that make classicist tests cheap.
- [naming.md](naming.md) — `When<scenario>` class + outcome-named methods, and the `<Situation>Scenario` classes that hold setup the constructor cannot. The single most copyable convention.
- [classicist.md](classicist.md) — what's real vs what's a double; state-based assertions; the persistence-boundary exception.
- [builders.md](builders.md) — fluent builders + personas in `Tests.Shared`.
- [unit-vs-integration.md](unit-vs-integration.md) — the principled split. In this repository the `testing-strategy` skill says which of the unit, integration and acceptance tiers owns each behaviour.
- [interface-design.md](interface-design.md) — designing for testability.
- [deep-modules.md](deep-modules.md) — small interface + deep implementation (Ousterhout).
- [tests.md](tests.md) — good vs bad test examples.
- [mocking.md](mocking.md) — sharpened "what to stub" guidance.
- [refactoring.md](refactoring.md) — refactor candidates after GREEN.
- [checklist.md](checklist.md) — verification checks + drift signals.
