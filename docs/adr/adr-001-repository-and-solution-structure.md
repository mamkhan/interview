# ADR-001: Repository and Solution Structure

Status: Accepted (review of 2026-09-07); solution layout superseded by ADR-010 (2026-09-10), isolation rules restated there
Date: 2026-09-07
Evidence: the build baseline; the technical direction of 2026-09-07; the inventory of the proof of concept

## Context

The V2 platform is built as a modular monolith: domains isolated so teams work in parallel, with deployment kept simple (technical direction, 2026-09-07). The build baseline defines six business domains and their sub-domains. A new repository was decided on 2026-09-07; the proof-of-concept rail migrates into it. The structure must let a domain team work without touching another domain's code, while keeping one build, one test run and one dependency baseline for the whole platform.

## Decision

One repository, one solution.

**Repository**: working name `acme-platform`; the real name is confirmed with Acme before creation (Phase 3 item 1). Top level: `src/`, `docs/adr/`, `infrastructure/`, `tools/`, plus the root build files (ADR-008).

**Solution layout** (`src/`, one `.slnx` solution):

```
src/
  AppHost/                          the application-graph description (local orchestration, ADR-006)
  building-blocks/                  seedwork, messaging, outbox, service defaults, sql helpers, tests-shared
  contracts/
    metering-consumption/           event contracts per pipeline, named from the event catalogue
    ...
  domains/
    metering-consumption/
      market-messaging/             one folder per sub-domain module (build baseline sub-domains)
        MarketMessaging.Domain/
        MarketMessaging.Application/
        MarketMessaging.Infrastructure/
        MarketMessaging.Host/       per ADR-002 packaging
        tests/
      consumption/
      meter-integration/
      transition/                   parity, V1 loads; retires with the parallel run
      acceptance-tests/
    billing-pay/                    created when its first module needs it
```

**Isolation rules**, enforced by architecture tests from Phase 3 onwards:

1. A project inside `domains/<X>/` may reference only projects inside `domains/<X>/`, `building-blocks/` and `contracts/`.
2. No project reference crosses from one domain folder into another. Cross-domain interaction happens through published contracts and the bus (ADR-004) or a read-only query API, never through code.
3. `contracts/` projects reference nothing except other contracts and the base envelope type; they contain event and message types only, no behaviour.
4. `building-blocks/` projects reference no domain and no contract.
5. Domain-layer projects (`*.Domain`) reference only `building-blocks` seedwork; no infrastructure packages (ADR-005).

## Consequences

- One `dotnet build` and one `dotnet test` cover the platform; a domain team's daily loop can still scope to its own module folders.
- The architecture-test project is a required Phase 3 deliverable and a pipeline gate; a violated rule fails the build, so the isolation holds without review vigilance.
- Adding a domain or module is a folder plus the module recipe (Phase 3), not a structural discussion.
- The single solution grows; if solution load or build time becomes a measured drag, solution filters (`.slnf`) per domain are the first lever, splitting the repository is not.
