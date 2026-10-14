# Acme Platform: Architecture

The high-level map: what the repository holds and which piece to open. Behaviour lives in code and
tests; decisions live in `docs/adr/`; this page stays a map.

## What this is

The V2 platform for the Acme energy business, built as a modular monolith on the six business domains
of the build baseline: Growth, Engage, Metering & Consumption,
Billing & Pay, Customer Experience, Platform & Data Insights. Domains are isolated module groups in
one solution; each domain deploys its own services through its own pipeline (ADR-002) onto Azure
Container Apps, with Azure Service Bus as the event backbone and two append-only books of record
(consumption and billing ledgers) at the centre of the design. Each domain folder holds everything the
domain owns (code, infrastructure, pipeline, solution, local run); `docs/repository-structure.md` is the
guide to the layout (ADR-010).

The Metering & Consumption domain is the first inhabitant. Running today, three modules:

- **MarketMessaging** (the market gateway: market registration, messaging and read acquisition). Its inbound features are Receive, Interpret and Audit. An operator
  file dropped in the drop container is sealed in the content-addressed archive and recorded as a receipt,
  whether or not it can be processed; the Interpret feature classifies each receipt from the operator's
  own header and dispatches it to the interpreter for its market and message type, which raises the business events (`IntervalReadingReceived` for ESB
  MM343; for SupplyPoints, one `MeterConfigurationChangeReceived` per file, MM331 included); the outcome is
  recorded on the receipt. No on-premises component: GoAnywhere writes to storage,
  Event Grid announces the blob, the platform reacts.
- **Consumption** (consumption and usage data). Its Profile feature keeps one row
  per supply point with the latest meter configuration codes and usage factors, each with the date it took
  effect and the sealed file it came from, fed by the meter configuration and usage factor events. Record,
  the consumption ledger, is a stub no host runs.
- **SupplyPoints** (the meter and device registry). Its Store feature reads the one event Market
  Messaging publishes for it per file, on its own topic (`MeterConfigurationChangeReceived`: the meter point codes,
  usage factors, meters and registers the file states), stores each file whole or not at all, and keeps, per supply
  point and kind of fact, a history of what each statement stated and a current table of what is in effect today and
  already known for later days: a statement that changes nothing writes nothing, and a backdated value reaches the
  later rows that only carried an earlier one. Its Reload feature replaces a supply point's
  meter point codes and usage factors with V1's when the V1 reader loads it (`SupplyPointLoadedFromV1`, on the
  platform's topic `plat.supply-point-loaded-from-v1`), keeping its meters and registers. Its Provide feature answers
  `IMeterPointConfiguration`, what was in effect on a day, for other modules in the host. Its tables live in the
  domain database's `supply` schema. No module calls it yet; the Profile is unchanged.

Next: the consumption ledger, then the
remaining operator message types and the head-end meter families.

## Metering & Consumption: what integrates with what

Only this domain speaks raw meter and market protocols. Two seams matter before reading the code:

- **Market Messaging is the gateway.** It owns the operator transport, the receipt and the dispatch:
  seal the file, record its arrival, classify it by the code the operator declares, hand it to the interpreter
  for that market and type, record what happened. The strategies that read operator payloads live in
  it today as `Application/Features/Interpret/MessageStrategies/<market>/`, one class per (market, type).
- **Meter Integration owns the head-end meters.** One adapter per meter family that does not arrive
  through a market operator. None is built yet; the first (a head-end system, HES) is the moment a `Reads` module
  returns with two members and a reason.

### Live today: ESB Networks, ROI electricity

| Step | Where | Code |
| --- | --- | --- |
| GoAnywhere puts the ESB file in the `esb-drop` container; Event Grid delivers `BlobCreated` to the `metr.receive-blob-created` queue (locally, `LocalBlobEvents` polls Azurite) | storage and Event Grid, ops-owned; `platform/src/LocalGraph/LocalBlobEvents/` locally | `MarketMessagingTopology` names the container and the queue |
| The receiver seals the bytes at `market-message-archive/roi-esb/<sha256>.xml`, publishes `MarketMessageReceived` with the event id as receipt id, deletes the dropped file last | `Hosts/MarketMessaging.Receiver` (role `Receive`) | `Modules/MarketMessaging/Application/Features/Receive/` |
| The receipt is written to `MarketMessage`, one row per arrival | `Hosts/MeteringConsumption.Host` (role `Audit`) | `Modules/MarketMessaging/Application/Features/Audit/` |
| The Interpret feature classifies from `MessageHeader/@MessageTypeCode`, dispatches to the (market, type) strategy, publishes `MarketMessageProcessed` or `MarketMessageFailed` | `Hosts/MeteringConsumption.Host` (role `Interpret`) | `Modules/MarketMessaging/Application/Features/Interpret/`, classifier under `Classifiers/RoiEsb/` |
| MM343 becomes one `IntervalReadingReceived` per meter-day, session-keyed by MPRN, identified by hash, MPRN, date and version | the strategy | `Modules/MarketMessaging/Application/Features/Interpret/MessageStrategies/ESB/`, parsing through `Common/ESBMessage/` |
| The outcome (processed, no-interpreter, failed with reason) lands on the receipt row | `Hosts/MeteringConsumption.Host` (role `Audit`) | `Modules/MarketMessaging/Application/Features/Audit/` |
| Record keeps each meter-day version in the `consumption-ledger` container of the ledger account, one Parquet file per supply point, read date and version, a replacement cancelling the version it replaces, and publishes `ConsumptionRecorded` on `metr.consumption-recorded` | `Hosts/MeteringConsumption.Host` (role `Record`) | `Modules/Consumption/Application/Features/Record/` |

Built: the whole chain above for ESB MM343, into the consumption ledger. Planned: the other ESB types (MM010,
MM105, MM3xx) as interpreters, GNI, NIE and NI gas as a classifier plus interpreters each, and the consumers
of `ConsumptionRecorded`. Anything operator-specific lives under
`Application/Features/Interpret/Classifiers/<market>/` or `Application/Features/Interpret/MessageStrategies/<market>/`; the Receive feature knows only the
market and fuel it is configured with.

### Live today: what only V1 holds

| Step | Where | Code |
| --- | --- | --- |
| Each night at 05:00 Dublin time the V1 reader reads V1's live database for every meter V1's forecast serves, and publishes `TariffResolved` on `grow.tariff-resolved` and a `MeterConfigurationReceived` stating only `IsNsmp` on `metr.meter-configuration-received` for each meter whose values changed since it last published them; every Sunday at 21:00 it publishes every meter | The legacy V1 platform's reader, an on-premises Windows service (not in this repository) | `Growth.Contracts/Events/`, `MeteringConsumption.Contracts/Events/` |
| The Profile keeps each supply point's latest tariff values and smart meter flag | `Hosts/MeteringConsumption.Host` (role `Profile`) | `Modules/Consumption/Application/Features/Profile/` |
| Once per environment, started by hand, the V1 reader's reading-history job reads 35 days of ESB's half-hourly readings and V1's own estimates from V1's live database and writes each meter-day straight into the consumption ledger as a first entry marked as V1's, never replacing a file the ledger holds | The V1 reader (not in this repository), writing Metering's `consumption-ledger` container | `MeteringConsumption.Contracts.Ledger` |
| Started by hand, for every forecast meter or a list of MPRNs, the V1 reader's meter point sync job reads V1's meter configuration codes and estimated usage factors, and its actual usage factors, from V1's live database, and publishes one `SupplyPointLoadedFromV1` per supply point on the platform's `plat.supply-point-loaded-from-v1` | The V1 reader (not in this repository) | `platform/src/Contracts/` |
| SupplyPoints replaces a supply point's meter point codes and usage factors with what V1 holds, each time the V1 reader loads it | `Hosts/MeteringConsumption.Host` (role `Reload`) | `Modules/SupplyPoints/Application/Features/Reload/` |

### The adapters still to build

Each is a Meter Integration adapter. The "via gateway" column is why payload interpretation is an interpreter
beside the gateway rather than a second module today.

| Adapter | Upstream | Via the market gateway | Reading contract |
| --- | --- | --- | --- |
| ROI electricity (L1e / L100 / NSMP) | ESB EMMA files; a head-end system (HES); SFE/SMSO WSE | MM343, MM3xx only | interval, register |
| NI electricity | NIE Networks filesystem drop; NIEEMPAPI | the drop only | interval, register |
| ROI gas, thick legacy | GNI / GasMap SOAP and filesystem; card readers | the file half only | register |
| ROI gas, thin (Smart PAYG) | GNI MDMS, NB-IoT | no | daily |
| NI gas | the NI gas network operator | no | to be defined |
| Heat | a heat metering provider | no | to be defined |
| Oil tank telemetry | a telemetry vendor's REST API | no | tank readings |
| Broadband | a wholesale broadband provider | no | daily accrual |

The gateway's own remaining work is separate from all of the above: outbound messages with licence
stamping, registration and switching state machines, and the per-operator lifecycle handlers (ESB
MM105/MM010, GNI G205N/G206N).

## Layout

The full guide is `docs/repository-structure.md`; this is the short form.

| Path | What it is |
| --- | --- |
| `AcmePlatform.slnx`, `Directory.*.props`, `global.json` | The full solution and the one build baseline. Every command runs from the repository root. |
| `platform/src/building-blocks/` | Seedwork, messaging (publisher, consumer base, additive topic map), outbox, SQL helpers, ServiceDefaults, shared test fixtures. Reference no domain and no contract. |
| `platform/src/LocalGraph/`, `platform/src/AppHost/` | The shared local fixtures as Aspire extension methods, `LocalBlobEvents` standing in for Event Grid locally, and the AppHost that composes every domain's graph on them. |
| `platform/src/Contracts/` | The platform's own events, which no domain owns (`SupplyPointLoadedFromV1`, published by the legacy V1 platform's reader, not in this repository). |
| `platform/src/architecture-tests/` | The ADR-010 isolation rules from the csproj files; per module, Domain depends on nothing outward, no slice depends on another, and only a repository touches SQL; checked on the compiled types. |
| `platform/src/tests/` | `BuildingBlocks.UnitTests` (no store) and `BuildingBlocks.IntegrationTests` on the shared container stack. |
| `platform/infrastructure/` | `root.hcl`, `common.hcl`, `_modules/`, `bootstrap/`, and `<env>/` units for the shared fixtures every domain lands on. Applied by `platform/pipelines/platform.yml`. |
| `platform/pipelines/` | The platform pipeline and the templates every domain pipeline is built from. |
| `domains/<domain>/<Domain>.slnx` | The domain's own solution: its projects plus the building blocks. |
| `domains/<domain>/src/Modules/<Module>/` | One project per module: `Module.md` (fifteen lines, no type names), `Domain/` (pure rules), `Application/Features/` for the features (`Receive/`, `Interpret/`, `Audit/`) with their handlers, interfaces and outcomes, `Application/Extensions/` and `Infrastructure/Extensions/` (one registration pair per role), `Common/` (what several slices parse), and `Infrastructure/` (consumers, implementations, options, and `Schema/` with the scripts and the policy for applying them). |
| `domains/<domain>/src/Hosts/` | The deployable units; each adds the registration pairs of the module roles it runs. The only runnable projects in a domain. |
| `domains/<domain>/src/Contracts/` | The domain's event types in `Events/`, those other modules consume and those one module's slices pass to each other, and the topology names another module needs. |
| `domains/<domain>/src/AppHost/`, `src/LocalGraph/` | This domain alone under `aspire run`, and its graph as extension methods the platform AppHost reuses. |
| `domains/<domain>/tests/` | One project per module per tier: `<Domain>.<Module>.UnitTests` (unit folders mirroring the module, plus `Composition/`) and `<Domain>.<Module>.IntegrationTests` on the shared stack, so the unit tier cannot reach a container. Plus `<Domain>.TestsShared` for builders and fakes, and `<Domain>.AcceptanceTests`, which drives every feature end to end on a running environment: the local AppHost before every push, dev after every deploy. |
| `domains/<domain>/infrastructure/` | Terragrunt for what only that domain uses: identity, SQL server and databases, storage, topics and subscriptions, container apps, alerts. Applied by the domain's pipeline. |
| `domains/<domain>/pipelines/` | The domain's Azure DevOps pipeline, path-scoped to the domain and the platform assets it depends on. |
| `domains/<domain>/tools/` | Recipes and scripts that belong to the domain. |
| `docs/adr/` | The accepted architecture decision records. |
| `tools/new-module.md`, `tools/new-domain.md` | The module and domain recipes. |

## Commands

```bash
dotnet build AcmePlatform.slnx                                        # everything, zero warnings
dotnet test  domains/metering-consumption/MeteringConsumption.slnx --filter "Category!=AcceptanceTests"   # the domain's unit and integration (Docker) tiers
dotnet test  platform/src/architecture-tests/ArchitectureTests        # the isolation and layering rules alone
aspire run --project domains/metering-consumption/src/AppHost         # this domain: SQL, Service Bus emulator, Azurite, hosts
aspire run --project platform/src/AppHost                             # every domain together
```

```bash
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv) ACME_ENVIRONMENT=dev
cd platform/infrastructure/units && terragrunt run --all plan                      # shared layer, one unit per type
cd domains/metering-consumption/infrastructure/units && terragrunt run --all plan  # a domain layer, one unit per type
```
