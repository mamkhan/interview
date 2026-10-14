# ADR-002: Packaging and Deployment Granularity

Status: Accepted (review of 2026-09-07, amended at review)
Date: 2026-09-07
Evidence: the technical direction of 2026-09-07; Container Apps validated in the build baseline; the proof of concept as built (seven deployable hosts); the non-functional requirements

## Context

The technical direction of 2026-09-07: simplify deployment; use Container Apps; keep domains independent so teams work in parallel; rules stay guidelines, never rigid constraints. The proof-of-concept rail deploys seven hosts (four Azure consumers and a viewer, two on-premises workers). The drafted default was one Container App host per domain with splits on measured need; the review of 2026-09-07 amended it: deployment granularity belongs to the domain, and so does the pipeline that delivers it.

## Decision

**Deployment granularity is each domain's own concern.** As decided at the review of 2026-09-07:

1. **Modules map to the build baseline's sub-domains** (unchanged): the module structure of ADR-001 is the code-level cut regardless of how the domain packages it for deployment.
2. **Each domain decides how many deployable units it runs**, sized by its own scaling requirements. Metering & Consumption is the highest-scale domain and may run several units (as the rail's host cut does today); simpler domains may run everything as a single host. No platform-level default is imposed; the guideline remains "deploy as much together as your scaling allows" and Container Apps is the compute standard.
3. **Each domain carries its own build and deployment pipeline and deploys its services autonomously.** In the one repository (ADR-001), domain pipelines are path-scoped: a domain's pipeline builds its projects, runs its test tiers with the acceptance gate (ADR-007), applies its own infrastructure stack (ADR-006), and deploys its units on its own cadence. A platform pipeline owns `building-blocks/`, `contracts/` and the shared infrastructure; a change there triggers the dependent domain pipelines through path filters. Pipeline mechanics are Phase 4 deliverables.
4. **On-premises workers stay separate deployables** (capture worker, parity worker, V1 data loads): they run beside the V1 estate through Azure Arc run-command, delivered by the owning domain's pipeline.
5. **The AppHost describes the whole application graph** for local orchestration regardless of the per-environment packaging (ADR-006).

For the foundation phase concretely: Metering & Consumption keeps a multi-unit deployment (the rail's session-scaled consumers justify it as the domain that scales hardest); its exact unit cut is confirmed by the domain team during the Phase 5 migration, not imposed by this ADR. Billing & Pay, when it starts, chooses its own cut and may well begin as one host.

## Consequences

- Domains deploy independently: separate cadence, separate blast radius, separate revision history, which is what the parallel-team requirement needs. The cost is per-domain pipeline upkeep; the mitigation is one shared pipeline template (Phase 4) so a domain pipeline is configuration, not invention.
- Cross-domain coupling moves entirely to the contracts: with independent deploys there is no lockstep release, so additive contract evolution (ADR-004 rule 8) is what keeps consumers safe while a publisher deploys.
- Messaging semantics never depend on packaging (ADR-004 rule 1), so a domain re-cutting its units is a deployment change, not a design change.
- The Phase 5 migration does not force a consolidation: Metering & Consumption confirms its own unit cut, and the AppHost graph is updated to match whatever it decides.

## Addendum, 2026-09-16: the two-host cut for Metering & Consumption

The domain deploys two Container Apps from one module: `MarketMessaging.Inbound` (role `Inbound`), which
seals operator files arriving in blob storage and announces the receipt, and `MeteringConsumption.Host`
(roles `Process`, `Persist`), which records receipts, classifies and dispatches, and records outcomes.
The cut is where the security boundary is: the inbound host holds the only identity that reads and deletes
in the inbound container, and it holds no database. The on-premises Windows service, its Arc onboarding
and its run-command deploy are retired; GoAnywhere writes to storage and Event Grid announces the blob.
Merging the two hosts is a `Program.cs` naming all three roles; no module changes.


## Addendum, 2026-09-23: the archiver host

The inbound host is renamed for what it does. `MarketMessaging.Archiver` runs role `Archive`: it archives
each file an operator drops in the `esb-drop` container and announces the receipt. `MeteringConsumption.Host`
runs roles `Process` and `Track`. The cut and its security boundary are unchanged: the archiver holds the
only identity that reads and deletes in the drop container, and it holds no database. Every Azure name
follows the host (`acm<env>metrcaarchiver`, its identity, its Event Grid system topic), so the change is a
new app and identity, not an edit of the old ones.

## Addendum, 2026-09-23: the receiver host

The archiver host is renamed after the feature it runs. `MarketMessaging.Receiver` runs role `Receive`;
`MeteringConsumption.Host` runs `Interpret` and `Audit`. The cut and its security boundary are unchanged.
Every Azure name follows the host (`acm<env>metrcareceiver`, its identity, its Event Grid system topic,
the `market-messaging-receiver` image), replacing the archiver names of the addendum above.

## Addendum, 2026-10-06: the V1 reader

The V1 reader (not in this repository) is an on-premises worker under rule 4. It is a platform service, not a domain's: it reads V1's
live database for more than one domain and publishes what it reads as each owning domain's events. It runs as
a Windows service on the proof of concept's Arc-enabled VM, inside V1's network, which already reaches V1's live
database and Azure; the platform pipeline delivers it through Arc run-command. One VM serves every environment
for now, with one service per environment. Reading V1 from Azure over a private link is postponed, not
rejected. The reader retires when V1 does.

## Addendum, 2026-10-12: the V1 reader writes the consumption ledger

The V1 reader also runs a load, started by hand once per environment: the last 35 days of every meter's consumption,
read from V1's live server and written straight into Metering & Consumption's consumption ledger, without Service
Bus. It writes create-only, through the ledger's published file format, and never replaces a file. Decided by the
user on 2026-10-12, against a message per day through the ledger's normal path, for its cost and duration. The user
moved the read from V1's reporting copy to its live server on 2026-10-13, because the reader's account has no login
on the copy.
