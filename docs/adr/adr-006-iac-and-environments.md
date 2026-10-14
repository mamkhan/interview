# ADR-006: Infrastructure as Code and Environments

Status: Accepted (review of 2026-09-07, amended at review: two-layer model, domain autonomy over its own infrastructure)
Date: 2026-09-07
Evidence: the infrastructure spike (proof run of 2026-09-07 on the proof-of-concept subscription); the reference codebase's infrastructure layout; the review of 2026-09-07

## Context

The platform needs dev, test and production environments with different resource tiers, provisioned repeatably, with tier differences reviewable as data. The spike proved the Terragrunt model end to end on Azure (apply in approximately 2.5 minutes, skeleton app serving, no drift, per-environment tiers from `env.hcl`, clean destroy). The review of 2026-09-07 added the ownership dimension: each domain has autonomy to deploy its own services and infrastructure, and a platform layer creates the shared resources.

## Decision

**Terragrunt owns infrastructure in two layers; domain pipelines apply their own layer; Aspire owns local orchestration.**

1. **Two layers with clear ownership.**
   - **Platform layer** (`platform/infrastructure/` (ADR-010 layout), owned by Platform Engineering, applied by the platform pipeline): the shared, per-environment fixtures every domain lands on: state bootstrap, the Container Apps environment, the Service Bus namespace, the container registry, Log Analytics and Application Insights, Key Vault, the network baseline, and the environment's resource organisation. The platform layer exposes its identifiers as outputs for domain stacks to consume.
   - **Domain layer** (`domains/<domain>/infrastructure/` (ADR-010 layout), owned by that domain, applied by that domain's pipeline per ADR-002): everything only that domain uses: its SQL databases, its storage and ADLS containers, its Container Apps, its Container Apps Jobs, its managed identities and role assignments. Messaging ownership follows publication: the publishing domain owns a topic; each consuming domain owns its subscriptions; the namespace is platform.
   - The ownership rule: a resource is platform-owned when more than one domain depends on it or it is an environment fixture; domain-owned otherwise. Disputes resolve at the ADR review, not in pipelines.
2. **Layout** (proven by the spike scaffold): a root `root.hcl` generating provider and backend, `common.hcl` for naming and tags, `_modules/` per resource concern, and per-environment folders (`dev/`, `test/`, `prd/`) inside both layers carrying only `env.hcl` tier variables and thin includes. State is isolated per layer, domain, environment and module by path; domain stacks read platform outputs through Terragrunt dependencies or data sources, never by hard-coded names.
3. **State backend**: `azurerm` (one bootstrap storage account per subscription, created by a documented one-off script in `infrastructure/bootstrap/`); provider pinned `azurerm ~> 4.x` with `subscription_id` supplied explicitly.
4. **Tiers are data**: `env.hcl` per environment and layer carries SKUs, replica ranges and retention values (proven: dev Service Bus Basic and scale-to-zero against prd Premium with 2 to 10 replicas, from one module source).
5. **Pipelines deploy applications; Terraform never does.** Each domain's pipeline builds and pushes its images and updates its Container Apps revisions against the provisioned environment; infrastructure changes and app deploys are separate stages with separate approvals. The pipeline-deploy leg is proven as Phase 4's verification, using the shared pipeline template so a domain pipeline is configuration rather than invention.
6. **Aspire stays the local orchestrator and the AppHost stays the description of the whole application graph** for development and the local acceptance run. The configuration-key contract is explicit: applications read the same keys locally (injected by the AppHost) and deployed (injected from IaC outputs into Container Apps environment variables); `ServiceDefaults` owns the key names and the acceptance suite, run against the AppHost before every push, guards the local half (amended 2026-10-12 with ADR-007: the boot test is removed).
7. **On-premises workers** keep the Arc run-command path from their domain's pipeline, outside Terragrunt scope.
8. **Engine and versions**: Terraform 1.16.x from the HashiCorp tap and Terragrunt 1.1.x (Homebrew core dropped Terraform at its licence change); OpenTofu is the recorded drop-in fallback; Terragrunt supports both.
9. **Regions**: every environment runs in North Europe, with one exception: dev's SQL server sits in France Central, because SQL provisioning is restricted in North Europe on the poc subscription and the apply fails with `ProvisioningDisabled`. The exception is `sql_location` in dev's `env.hcl` and it retires when a quota exception is granted; the server's name is unchanged by it. The network baseline (ExpressRoute against site-to-site VPN) is a later decision.

## Consequences

- Domains provision and deploy without waiting on each other; the platform layer is the only ordering dependency (it must exist per environment before domain stacks apply), expressed as Terragrunt dependencies rather than process.
- Shared-resource changes concentrate in one reviewed place with platform ownership, so a domain cannot reshape a fixture other domains stand on; the trade is that new shared needs go through the platform backlog.
- Two layers mean two drift surfaces per environment; the Phase 4 pipeline runs `terragrunt plan` drift checks per stack.
- The rail's current `aspire do provision-azure-bicep-resources` path retires when the migrated pipeline lands (Phase 5); until then the rail deploys as it does today.
- Phase 4 carries the follow-through: the bootstrap state account, the platform stack, the Metering & Consumption domain stack, the pipeline template, and the dev environment applied through both layers.

## Build note, 2026-09-08 (foundation Phase 4)

Three points settled while the two layers were built, none of them changing the decision above.

- **Environment folders take the naming codes** of ADR-009: `dev/`, `tst/`, `prd/`. This supersedes the `test/` spelling in rule 2.
- **The SQL logical server is domain-owned**, alongside its databases, and so are the archive and ledger storage accounts. Rule 1 already assigned the databases and containers to the domain; the server and the accounts follow them, which keeps a domain's data estate inside one pipeline's blast radius. The platform layer holds the Container Apps environment, the Service Bus namespace, the container registry, monitoring and Key Vault.
- **The container image is not Terraform's** (rule 5 in practice): the container-app module ignores changes to the image so the first apply can stand up an app with a public sample image and every subsequent image change belongs to the pipeline. A `terragrunt plan` therefore stays clean after a deploy, which is what makes the drift check in both pipelines meaningful.

## Addendum, 2026-10-06: the V1 reader's access

1. **The one exception to Entra-only access.** The V1 reader (not in this repository) signs in to V1 the way the proof of concept does:
   through Windows sign-in as the proof of concept's domain account, under which its Windows service runs. Its
   connection string, which holds no credential, is a secret in the platform Key Vault, read by name at startup
   through the VM's Arc system-assigned identity. The account's password is a secret variable of the
   `acme-platform-<env>` variable group, handed to the VM as a protected run-command parameter and kept by the
   Windows service manager. The exception retires when V1's DBAs grant the reader its own domain account or the
   reader moves to Azure with Entra sign-in. Decided by the user on 2026-10-06; the account replaced a SQL login,
   which the proof of concept turned out not to use, on 2026-10-08.
2. **Everything else is Entra.** The reader reaches Service Bus, Key Vault, storage and Application Insights as
   the VM's identity. Every service on the VM shares that identity, so each grant is scoped to one topic, one
   container or one secret.
3. **On-premises packages live in the platform layer**: one storage account per environment
   (`acm<env>platstpackages`) from which the VM downloads a build. The install on the VM stays outside
   Terragrunt (rule 7).

## Addendum, 2026-10-07: one unit per resource type

1. **Rule 2's per-environment unit folders give way, one resource type at a time, to one unit per type**
   under `<layer>/infrastructure/units/<type>/`. The unit serves every environment, chosen by
   `ACME_ENVIRONMENT`, with one state per layer, domain, environment and type. Its instances are YAML files
   under `base/variables/<type>/`, named for the instance; a same-named file under `<env>/variables/<type>/`
   replaces top-level keys, and a file only there adds the instance to that environment alone. This follows
   the reference codebase's layout.
2. **Why.** A unit per instance kept the instance list in the folder tree, so a renamed or deleted folder left
   its resources running with nothing to plan their removal: on 2026-10-07 dev held seven such states, two of
   them running Container Apps. Every unit was also copied into each environment folder, and the copies had
   drifted. With the list inside the state, deleting a file is a planned destroy, and an instance is defined
   once.
3. **What stays.** Tiers are `env.hcl` data (rule 4), read by the shared loader. State keys stay layer-first.
   Platform outputs still arrive through Terragrunt dependencies. Each environment gets its own Terragrunt
   working copy, because a copy records the backend it was initialised against.
4. **Order.** Metering & Consumption's Service Bus topics and subscriptions moved first. Until every type has
   moved, a domain pipeline applies `units/` before the environment folder. Decided by the user on
   2026-10-07.
5. **Metering & Consumption complete, 2026-10-08.** Every resource of the domain is on a per-type unit, twelve
   types in all, adopted by import without an add, change or destroy. Its environment folders hold only
   `env.hcl` and `variables/`, and its pipeline runs one Terragrunt pass over `units/`. For a domain on this
   model:
   - Tiers move from `env.hcl` into the instance files (rule 4): the base file carries the value most
     environments share and `<env>/variables/<type>/` the exception. `env.hcl` keeps environment-wide facts
     (location, the retry budget, the deployer and test principals). Rule 9's France Central exception is
     dev's `variables/sql-servers/main.yaml`.
   - A value one resource reads from another's output is `{ from: <ref> }` in the file, resolved by the unit
     from its dependencies; the configuration-key contract of rule 6 is unchanged.
   - Entity names are the instance file names, so the domain has no `domain.hcl`. This replaces, for such a
     domain, ADR-009's `domain.hcl` mirror and the `domain.hcl` and environment-unit entries of ADR-010's layout.
   Decided by the user on 2026-10-08.
6. **Platform layer and Growth, 2026-10-08.** Both moved the same way, so rule 2's per-environment unit folders
   are gone from the repository. The platform layer is eleven types; the monitoring fixture became three
   (`log-analytics-workspaces`, `application-insights`, `monitor-action-groups`), and an on-premises worker's
   grants and alerts are one `onprem-workers` instance. Growth's
   topic is an instance of the same `servicebus-topics` type Metering & Consumption uses. Domain units read
   platform outputs from `platform/infrastructure/units/<type>`. Decided by the user on 2026-10-08.

## Addendum, 2026-10-12: the V1 reader's write on the consumption ledger

The VM's identity holds Storage Blob Data Contributor on Metering's `consumption-ledger` container only while that
environment's history load runs: a flag on the container's instance file grants it, and removing the flag after the
load removes it. Every service on the VM shares the identity, the proof of concept's included, so a standing grant
would leave the permanent record writable from that machine.
