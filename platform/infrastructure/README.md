# Infrastructure

Two Terragrunt layers per ADR-006, each inside its owner's folder (ADR-010):

- `platform/infrastructure/`: the shared fixtures every domain lands on (resource group, Log Analytics,
  Application Insights, the operations action group, Service Bus namespace, container registry, Key Vault,
  Container Apps environment, the on-premises packages account and the on-premises workers' grants and alerts).
  Owned by Platform Engineering, applied by `platform/pipelines/platform.yml`.
- `domains/<domain>/infrastructure/`: everything only that domain uses (its resource group,
  identity and role assignments, topics and subscriptions, databases, storage containers, container
  apps, alerts). Owned by the domain, applied by the domain's pipeline. Cross-layer wiring goes through
  Terragrunt `dependency` outputs on `${get_repo_root()}/platform/infrastructure/units/<type>`, never
  hard-coded names.

This folder also holds what both layers share: `root.hcl` (backend and provider), `common.hcl` (org
segment and base tags), `resources.hcl` (the units' YAML loader), `_modules/` (one Terraform module per
resource type) and `bootstrap/`. Every unit includes `${get_repo_root()}/platform/infrastructure/root.hcl` and
sources its module from `${get_repo_root()}/platform/infrastructure/_modules/<type>`. Every resource name is
composed in the Terragrunt units per the naming standard (ADR-009): `acm` + environment + domain code +
type + instance.

Each layer has one unit per resource type under `<layer>/infrastructure/units/<type>/`, serving every
environment, chosen by `ACME_ENVIRONMENT`, with one state per environment (`platform/<env>/<type>`,
`domains/<domain>/<env>/<type>`). It includes `resources.hcl`, which loads one YAML file per instance from
`base/variables/<type>/` and lets a same-named file under `<env>/variables/<type>/` replace top-level keys,
so a tier change is a one-line reviewable diff. The environment folders hold only `env.hcl` (environment-wide
facts) and `variables/`. The instance list lives in the type's state, so deleting a file destroys the instance
on the next apply and renaming one replaces it. Deleting the unit folder itself strands its state: remove the
instances first. A setting or role scope that comes from another resource is `{ from: <ref> }` in the file; the
unit builds the refs from its dependencies' outputs, and an unknown ref fails the plan. A pipeline runs one
Terragrunt pass over a layer's `units/`.

`dev` is the first environment to be applied, through the pipelines; `tst`
and `prd` are prepared but not applied.

Run from a layer's `units/` (or one unit) with `ARM_SUBSCRIPTION_ID` and `ACME_ENVIRONMENT` set:

    export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv) ACME_ENVIRONMENT=dev
    cd platform/infrastructure/units && terragrunt run --all plan
    cd domains/metering-consumption/infrastructure/units && terragrunt run --all plan
    cd domains/growth/infrastructure/units && terragrunt run --all plan

One-off per subscription before anything else: `bootstrap/bootstrap-state.sh` creates the state
backend named in `root.hcl` and grants blob-data access to the humans and the pipeline principal.
