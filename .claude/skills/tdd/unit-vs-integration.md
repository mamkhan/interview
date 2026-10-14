# Unit vs integration

The split is **principled, not arbitrary**: a unit test exercises the system with no I/O, an integration test exercises the real persistence stack and real external services.

| | Unit | Integration |
|---|---|---|
| Database | Repository stubbed via interface | **Real database**; real persistence stack |
| External services | Stubbed | Real (or sandbox accounts for cloud services) |
| Speed | Milliseconds | Seconds |
| Asserts on | Aggregate state, result objects, repo interaction with state predicate | Direct DB / system queries: row counts, row contents, side-effect artefacts |
| Setup | Test class constructor | Async lifecycle hook (initialise / dispose) |

## Disciplines

- **Separate test modules**, not tags inside one module — so unit and integration suites are filterable from a single CLI flag and routable separately in CI.
- **No in-memory fake databases** in integration tests. The whole point of an integration test is to verify the real persistence stack. SQLite-instead-of-Postgres, in-memory EF, "test mode" toggles — all of these defeat the purpose. If the integration test passes against a fake but the production migration breaks, the test was lying.
- **Integration tests still follow the `When<scenario>` + outcome-name conventions.** They differ in *what is real*, not in *how they read*.
- **Domain unit tests are stub-free.** `Domain.UnitTests` exercises aggregates with only real domain objects — no stubs at all. If you find yourself reaching for a stub there, the aggregate has a hidden dependency that should be moved to the handler.

## What asserts on what

In a unit test, you assert through the same surface the production caller uses: result objects, aggregate state, exposed events. The repository is a stub — its only job is to return preset data and (at the persistence boundary) be checked with `Received().Save(matchAggregate(...))`.

In an integration test, you've executed against a real DB, so you can — and should — query that DB directly to confirm what landed:

```
// Unit (UnitTests module): assert through the in-memory aggregate
order.Status.Should().Be(Submitted)
repository.Received().Save(matchAggregate(o => o.Status == Submitted))

// Integration (IntegrationTests module): assert through the real DB
row = db.Query("SELECT status FROM orders WHERE id = $1", order.Id).Single()
row.Status.Should().Be("Submitted")
```

Note this is the *one place* "verifying through external means" is correct — because the external means *is* the system being tested.

## Where each kind of test belongs

| Kind of behaviour | Test module |
|---|---|
| Aggregate invariants, value-object validation, pure domain rules | `Domain.UnitTests` |
| Handler orchestration; what gets called, what gets saved, what events surface | `UnitTests` |
| Wiring across handler + real repository + real DB; HTTP/queue adapters end-to-end | `IntegrationTests` |
| Repository implementation against real DB | `IntegrationTests` |

If you're tempted to put a "unit test" of a repository implementation into `UnitTests`, stop — the implementation's whole job is to talk to the database. Test it for real.
