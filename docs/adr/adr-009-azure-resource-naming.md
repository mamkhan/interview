# ADR-009: Azure Resource Naming

Status: Accepted (2026-09-08)
Date: 2026-09-08
Evidence: the team's naming standard, adopted for this platform; Azure resource name rules

## Context

Every Azure resource in the platform needs a name that is valid on the strictest resource type, readable in a resource group holding dozens of resources, and composed by Terragrunt rather than typed by hand. This ADR adopts the team's naming standard in full; this file is the standard's registration point for the platform, so a new domain code, resource type abbreviation or service code is added here in the same commit as the Terraform module that first uses it.

## Decision

Names follow `[Org][Environment][Domain][ResourceType][Instance]`, flat lowercase alphanumeric, org fixed `acm`. The full rules follow.

### Adoption notes for this repository

- Names are composed in the Terragrunt units from `platform/infrastructure/common.hcl` (org), `layer.hcl` (domain code), `env.hcl` (environment code) and the unit's instance segment; modules receive finished names and never compose them.
- Environment folders under `platform/infrastructure/` and `domains/<domain>/infrastructure/` follow the environment codes (`dev/`, `tst/`, `prd/`), which supersedes the `test/` folder name in ADR-006 rule 2.
- Application Insights workbooks are an §8-style exception: the resource name must be a GUID by the provider contract, so the workbook takes a deterministic `uuidv5` name and the display name carries the description.
- Child resources keep the names from the contracts library (blob containers `esb-inbound`, `market-message-archive`, database `metering-consumption`): §7 permits full-word child names with hyphens where the character set allows. Service Bus entities follow §7.1.

## 1. Naming Structure

All resource names follow a flat, contiguous structure with no delimiters.

**Format:** `[Org][Environment][Domain][ResourceType][Instance]`

| Section | Width | Description | Example |
|---|---|---|---|
| **1. Org** | 3, fixed | The platform. Always `acm`. | `acm` |
| **2. Environment** | 3, fixed | Deployment environment (§3). | `dev` |
| **3. Domain** | 4, fixed | Owning domain (§4). | `metr` |
| **4. Resource Type** | 2 to 6 | CAF-aligned service abbreviation (§5). | `ca` |
| **5. Instance** | 1 to 12 | The service or purpose (§6). | `ledger` |

The first ten characters form a fixed prefix that is identical for every resource in a given domain and environment:

```
acm + dev + metr + ca + ledger  =  acmdevmetrcaledger
└──────── prefix ───────┘
```

`acmdevmetrcaledger` is the Container App running the consumption ledger consumer in the dev environment of the Metering and Consumption domain.

### The 24-Character Rule

The prefix is 10 characters and the instance is capped at 12, so the longest name any resource type can produce depends only on its abbreviation:

```
maximum name length = 22 + length of resource type abbreviation
```

The three resource types that cap at 24 characters (storage account, Key Vault, Batch account) all use a two-character abbreviation, so their maximum is exactly 24. Every other resource type has headroom. The per-type figures are in §5.

## 2. Global Constraints

These rules apply to every resource name without exception.

1. **Maximum length:** 24 characters for resources capped at 24; otherwise as given in §5.
2. **Casing:** strictly **lowercase**.
3. **Characters:** **alphanumeric only** (a to z, 0 to 9). No hyphens, underscores, spaces or periods.
4. **Generation:** names are composed by Terragrunt from `common.hcl`, never typed by hand.
5. **Registration:** a new domain code, resource type abbreviation or service code is added to this document in the same commit as the Terraform module that first uses it.

## 3. Environment Codes

| Environment | Code |
|---|---|
| Development | `dev` |
| Test | `tst` |
| Production | `prd` |
| User acceptance (reserved) | `uat` |

Region is not part of the name, and a resource that sits in a different region from the rest of its environment is not renamed for it: one service keeps one spelling across resource types, environments and regions. Which region an environment uses is ADR-006 rule 9.

## 4. Domain Codes

| Domain | Code |
|---|---|
| 01 Growth | `grow` |
| 02 Engage | `enga` |
| 03 Metering & Consumption | `metr` |
| 04 Billing & Pay | `bill` |
| 05 Customer Experience | `cust` |
| Platform | `plat` |
| Data | `data` |

Resources shared across domains or fixed per environment (the Container Apps environment, the Service Bus namespace, the container registry, Log Analytics, the network baseline, the Terraform state backend) take `plat`. Everything else takes its owning domain's code.

## 5. Resource Type Abbreviations

Abbreviations follow the Microsoft Cloud Adoption Framework unmodified. **Max name** is the longest name the convention can produce for that type (`22 + abbreviation length`). **Azure limit** is the published constraint from [Microsoft's resource name rules](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules).

Child resources take the instance segment only (§7), so their maximum is 12.

### 5.1 Compute and Containers

| Resource | Code | Max name | Azure limit |
|---|---|---|---|
| Resource group | `rg` | 24 | 90 |
| Container app | `ca` | 24 | 32 |
| Container apps job | `caj` | 25 | 32 |
| Container apps environment | `cae` | 25 | 32 |
| Container registry | `cr` | 24 | 50 |
| App Service plan | `asp` | 25 | 60 |
| App Service (web app or API) | `app` | 25 | 60 |
| Function app | `func` | 26 | 60 |
| Static Web App | `stapp` | 27 | 40 |
| AKS cluster | `aks` | 25 | 63 |
| Arc-enabled server | `arcs` | 26 | 54 |

### 5.2 Data and Storage

| Resource | Code | Max name | Azure limit |
|---|---|---|---|
| Storage account | `st` | 24 | 24 |
| Blob container (child) | `blob` | 12 | 63 |
| ADLS Gen2 filesystem (child) | `adls` | 12 | 63 |
| Batch account | `ba` | 24 | 24 |
| SQL logical server | `sql` | 25 | 63 |
| SQL database (child) | `sqldb` | 12 | 128 |
| SQL managed instance | `sqlmi` | 27 | 63 |
| Cosmos DB account | `cosmos` | 28 | 44 |
| Azure Cache for Redis | `redis` | 27 | 63 |
| Data Factory | `adf` | 25 | 63 |
| Databricks workspace | `dbw` | 25 | 64 |
| Synapse workspace | `syn` | 25 | 50 |

### 5.3 Messaging and Events

| Resource | Code | Max name | Azure limit |
|---|---|---|---|
| Service Bus namespace | `sbns` | 26 | 50 |
| Event Hubs namespace | `evhns` | 27 | 50 |
| Event hub (child) | `evh` | 12 | 256 |
| Event Grid topic | `evgt` | 26 | 50 |
| Event Grid system topic | `egst` | 26 | 128 |

### 5.4 Security and Identity

| Resource | Code | Max name | Azure limit |
|---|---|---|---|
| Key Vault | `kv` | 24 | 24 |
| User-assigned managed identity | `id` | 24 | 128 |
| App registration or service principal | `spn` | 25 | n/a |

### 5.5 Networking

| Resource | Code | Max name | Azure limit |
|---|---|---|---|
| Virtual network | `vnet` | 26 | 64 |
| Subnet (child) | `snet` | 12 | 80 |
| Network security group | `nsg` | 25 | 80 |
| Private endpoint | `pep` | 25 | 64 |
| Public IP address | `pip` | 25 | 80 |
| Application Gateway | `agw` | 25 | 80 |
| Front Door profile | `afd` | 25 | 260 |
| Load balancer, internal | `lbi` | 25 | 80 |
| Load balancer, external | `lbe` | 25 | 80 |
| ExpressRoute circuit | `erc` | 25 | 80 |
| VPN gateway | `vgw` | 25 | 80 |
| NAT gateway | `ng` | 24 | 80 |

### 5.6 Observability

| Resource | Code | Max name | Azure limit |
|---|---|---|---|
| Log Analytics workspace | `log` | 25 | 63 |
| Application Insights | `appi` | 26 | 260 |
| Action group | `ag` | 24 | 260 |
| Portal dashboard | `dash` | 26 | 160 |
| Alert rule | `alrt` | 26 | 260 |

### 5.7 Adding a New Abbreviation

1. Take the Cloud Adoption Framework abbreviation unmodified.
2. Compute the maximum name as `22 + abbreviation length`.
3. Confirm it is within the resource's published Azure limit. If it is not, the resource belongs in §8 with an explicit instance cap.
4. Add the row to §5 in the same commit as the module that uses it.

## 6. Instance Segment

Up to 12 characters, lowercase alphanumeric. This segment identifies which resource it is, and it is what makes a name readable in a resource group holding dozens of resources.

| Case | Rule | Example |
|---|---|---|
| One resource per service | The registered service code. | `acmdevmetrcaledger` |
| Sole resource of its type in the domain | `main` | `acmdevmetrsqlmain` |
| Serves the whole domain or environment | `shared` | `acmdevmetrstshared` |
| A distinct named purpose | The purpose. | `acmdevplatsttfstate` |
| Several genuinely identical resources | `01`, `02` | `acmdevmetrst01` |

An ordinal is used only for the last case, where the resources are interchangeable. Resources that differ in function are always distinguished by name, never by number.

### 6.1 Service Codes

One service has one spelling everywhere, across resource types and across environments. Register new codes here.

| Service | Code |
|---|---|
| Consumption ledger consumer | `ledger` |
| Consumption projection consumer | `projection` |
| Consumption viewer | `viewer` |
| Forecast day-commit handler | `dayhandler` |
| Forecast day-commit issuer | `dayissuer` |
| Market message consumer | `marketmsg` |
| Market messaging inbound host | `inbound` |
| ESB folder watcher | `esbwatcher` |
| Shadow comparison | `shadowcomp` |
| V1 reader | `v1reader` |
| SQL Entra administrator | `sqladmin` |
| Terraform state | `tfstate` |

## 7. Child Resources

A child resource is already scoped by its parent, whose name carries the org, environment and domain. Child resources therefore take the **instance segment only**, and because their limits are generous they are spelled out in full.

| Parent | Child | Child name |
|---|---|---|
| `acmdevmetrsqlmain` | SQL database | `consumption` |
| `acmdevmetrstshared` | Blob container | `marketmessages` |
| `acmdevmetrsbnsshared` | Topic | `consumptionrecorded` |
| `acmdevmetrsbnsshared` | Subscription | `ledger` |
| `acmdevplatvnetmain` | Subnet | `containerapps` |

Hyphens are permitted on child resources whose character set allows them.

### 7.1 Service Bus entities (2026-09-16)

Topics, subscriptions and queues are named by the code that publishes and consumes them, identical in every
environment, and are not composed from the org, environment and domain segments.

| Rule | Detail |
|---|---|
| Segments | Separated by `.`; words inside a segment by `-`; lowercase. |
| Topic | `{domain}.{event}`: the §4 domain code and the event name in kebab case, past tense. The sub-domain is omitted because a topic name is permanent and module names are not. |
| Subscription | `{domain}.{handler}`: the consuming domain's code and a handler name that names the object and the action (`market-message-processor`, never `processor`). Azure allows 50 characters, so 45 for the handler. |
| Queue | `{domain}.{purpose}`, the owner's code. |
| Suffixes and versions | No entity-type suffixes. A breaking change is a new topic with a trailing version segment (`metr.consumption-recorded.v2`); additive changes need nothing. |
| Properties and rules | Application properties in lowerCamel (`market`). Filter rules are named after what they select, only where filters exist. |
| Where constants live | Topic, queue and container names in the publishing domain's `Contracts` topology type. Subscription names in the consuming module, never in the publisher's contracts. Terragrunt mirrors both in the domain's `domain.hcl`. |

Applied today: queue `metr.esb-inbound-blob-created`; topics `metr.market-message-received`,
`metr.market-message-processed`, `metr.market-message-failed`, `metr.interval-reading-received`; subscriptions
`metr.inbound-market-message-writer`, `metr.market-message-processor`, `metr.inbound-market-message-outcome-writer`,
`metr.consumption-ledger`.

## 8. Exceptions

Four resource types cannot carry this convention. Do not attempt to force them.

| Resource | Limit | Rule |
|---|---|---|
| Virtual machine (Windows guest name) | 15 | Outside the platform architecture. If ever needed, use `acm` + environment initial + purpose and record it here. |
| Virtual machine scale set | 15 | As above. |
| Data Explorer cluster | 22 | Cap the instance segment at 9 characters. |
| AKS node pool | 12 | Child resource, so the instance segment applies, but with no spare characters. |

**Private DNS zones** must be the DNS name being resolved, for example `privatelink.blob.core.windows.net`. **Container Apps revisions** take an Azure-generated suffix. Neither is named by us.

## 9. Tags

A 24-character name identifies a resource; the tags describe it. Terragrunt applies these from `common.hcl`, so no tag is set by hand.

| Tag | Value | Purpose |
|---|---|---|
| `domain` | Full domain name, for example `Metering & Consumption` | Expands the domain code. |
| `environment` | `dev`, `tst`, `prd` | Cost reporting and policy scope. |
| `service` | Full service name, for example `consumption-projection-consumer` | Expands the service code. |
| `owner` | Owning team | Escalation. |
| `managed-by` | `terragrunt` or `bootstrap` | Separates provisioned resources from the state backend. |
| `layer` | `platform` or `domain` | Ownership layer. |

Where a name is abbreviated, the tag carries the full value. Search and report on tags; read names for identification.

## 10. Examples

Metering and Consumption, dev environment:

* **Resource group:** `acm` + `dev` + `metr` + `rg` + `main` → **`acmdevmetrrgmain`** (16)
* **Container app:** `acm` + `dev` + `metr` + `ca` + `projection` → **`acmdevmetrcaprojection`** (22)
* **Container apps job:** `acm` + `dev` + `metr` + `caj` + `dayissuer` → **`acmdevmetrcajdayissuer`** (22)
* **Managed identity:** `acm` + `dev` + `metr` + `id` + `marketmsg` → **`acmdevmetridmarketmsg`** (21)
* **Storage account:** `acm` + `dev` + `metr` + `st` + `shared` → **`acmdevmetrstshared`** (18)
* **Key Vault:** `acm` + `dev` + `metr` + `kv` + `shadow` → **`acmdevmetrkvshadow`** (18)
* **SQL logical server:** `acm` + `dev` + `metr` + `sql` + `main` → **`acmdevmetrsqlmain`** (17)
* **Service Bus namespace:** `acm` + `dev` + `metr` + `sbns` + `shared` → **`acmdevmetrsbnsshared`** (20)
* **Arc-enabled server:** `acm` + `dev` + `metr` + `arcs` + `v1reader` → **`acmdevmetrarcsv1reader`** (22)

Platform, production environment:

* **Terraform state resource group:** `acm` + `prd` + `plat` + `rg` + `tfstate` → **`acmprdplatrgtfstate`** (19)
* **Terraform state storage account:** `acm` + `prd` + `plat` + `st` + `tfstate` → **`acmprdplatsttfstate`** (19)
* **Container registry:** `acm` + `prd` + `plat` + `cr` + `shared` → **`acmprdplatcrshared`** (18)
* **Log Analytics workspace:** `acm` + `prd` + `plat` + `log` + `shared` → **`acmprdplatlogshared`** (19)
* **On-premises package storage account:** `acm` + `prd` + `plat` + `st` + `packages` → **`acmprdplatstpackages`** (20)

Billing and Pay, test environment:

* **Container app:** `acm` + `tst` + `bill` + `ca` + `notification` → **`acmtstbillcanotification`** (24)
* **Cosmos DB account:** `acm` + `tst` + `bill` + `cosmos` + `entitlement` → **`acmtstbillcosmosentitlement`** (27)
