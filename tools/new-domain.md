# Adding a domain

A domain is one of the six business domains, and one top-level folder under `domains/`
holding everything it owns (ADR-010). `domains/metering-consumption/` is the reference copy source.
Paths below are relative to the repository root.

1. Register the domain's naming code (ADR-009 §6) and create `domains/<domain>/` with the folder set:
   `src/`, `infrastructure/`, `pipelines/`, `tools/` and `<Domain>.slnx`.
2. **Infrastructure.** Copy `infrastructure/layer.hcl` (domain code, layer, owner), the `dev/` `tst/`
   `prd/` folders with their `env.hcl` (environment-wide facts), and the units under `units/` the domain
   needs, one per resource type. Write one file per instance under `base/variables/<type>/`, named for the
   instance (a topic, subscription, queue or container file is named for the entity), and put what differs
   per environment in a same-named file under `<env>/variables/<type>/`. Each unit serves every environment
   from `ACME_ENVIRONMENT`, with state at `domains/<domain>/<env>/<type>`.
3. **Source.** Create `src/Contracts/<Domain>.Contracts` (the topology type and the event types on the
   event catalogue), the first module under `src/Modules/` per `tools/new-module.md` (it
   owns its tables and migrations), and the first host under `src/Hosts/` naming the module roles it runs.
4. **Local run.** Create `src/LocalGraph/<Domain>.LocalGraph` with `Add<Domain>Fixtures(this
   IDistributedApplicationBuilder, PlatformFixtures)` (topics, subscriptions, containers, database on the
   shared fixtures) and one `Add<Host><TProject>` method per host. Create `src/AppHost/<Domain>.AppHost`
   calling `AddPlatformFixtures()` then the domain's methods, with its own `launchSettings.json` ports
   and an `aspire.config.json`. Add the same calls to `platform/src/AppHost/AppHost.cs` and the hosts and
   LocalGraph to `Platform.AppHost.csproj`. If a domain consumes another domain's events, its
   `Add<Domain>Fixtures` takes that domain's fixtures record as a second parameter and subscribes to its
   topics there; it never declares another domain's topic.
5. **Tests.** `tests/<Domain>.AcceptanceTests` drives the first slice end to end against a running
   environment, copied from `MeteringConsumption.AcceptanceTests` with its `TargetEnvironment`: the local
   AppHost before every push, dev after every deploy (the `testing-strategy` skill).
6. **Pipeline.** Copy `domains/metering-consumption/pipelines/metering-consumption.yml` to
   `pipelines/<domain>.yml`: set the path filter to the domain folder plus the platform assets it is built
   and deployed from, pass the domain's solution and `domains/<domain>/tests` to
   `/platform/pipelines/templates/test-jobs.yml`, keep the acceptance stage after the dev deploy,
   point `unitsPath` at `domains/<domain>/infrastructure/units`, and set the ADR-009 names in
   `variables`. Register it in Azure DevOps against that YAML path with its own environment and approval
   check.
7. **Solutions.** `<Domain>.slnx` lists the domain's projects plus `platform/src/building-blocks/*` and
   `Platform.LocalGraph`; add every project to `AcmePlatform.slnx` as well. Both must build at zero
   warnings and the architecture tests must stay green.
8. Add the domain's `README.md` only if it has a decision to record that the ADRs do not (a second
   database, a host split); otherwise the layout is the documentation.
