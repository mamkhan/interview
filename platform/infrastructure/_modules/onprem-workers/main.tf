variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }

# The resources a worker file names, by the instance name it uses for them.
variable "account_blob_endpoints" { type = map(string) }
variable "container_ids" { type = map(string) }
variable "key_vault_ids" { type = map(string) }
variable "key_vault_uris" { type = map(string) }
variable "service_bus_fqdns" { type = map(string) }
variable "app_insights_ids" { type = map(string) }
variable "action_group_ids" { type = map(string) }
variable "app_insights_connection_strings" {
  type      = map(string)
  sensitive = true
}

# Keyed by worker, each carrying the alert names the unit composed for its scheduled jobs and, for a worker that
# names a ledger account, that account's blob endpoint. A worker counts one on
# `runs_metric_name` after every scheduled run, with a `job` dimension and an `outcome` dimension of `succeeded` or
# `failed`. A misspelt key fails the plan instead of being dropped by type conversion.
variable "onprem_workers" {
  type = any

  validation {
    condition = alltrue(flatten([
      for w in values(var.onprem_workers) : [
        for required in [[
          "display_name", "arc_machine", "arc_resource_group", "packages_account", "packages_container", "key_vault",
          "key_vault_secret_names", "service_bus", "app_insights", "action_group", "runs_metric_name", "scheduled_jobs",
        ]] : length(setsubtract(required, keys(w))) == 0 && length(setsubtract(keys(w), concat(required, ["ledger_blob_endpoint"]))) == 0
      ]
    ]))
    error_message = "A worker file sets display_name, arc_machine, arc_resource_group, packages_account, packages_container, key_vault, key_vault_secret_names, service_bus, app_insights, action_group, runs_metric_name and scheduled_jobs, and may name a ledger account; nothing else."
  }
}

# A worker signs in to Azure with its machine's Arc identity. Every service on the machine can use that identity,
# the proof of concept's included, so each grant is scoped to one container or one secret.
data "azurerm_arc_machine" "this" {
  for_each = var.onprem_workers

  name                = each.value.arc_machine
  resource_group_name = each.value.arc_resource_group
}

locals {
  principal_ids = { for k, m in data.azurerm_arc_machine.this : k => m.identity[0].principal_id }

  secrets = merge([
    for k, w in var.onprem_workers : {
      for s in w.key_vault_secret_names : "${k}/${s}" => { worker = k, secret = s, key_vault = w.key_vault }
    }
  ]...)

  jobs = merge([
    for k, w in var.onprem_workers : {
      for j, alerts in w.scheduled_jobs : "${k}/${j}" => merge(alerts, { worker = k, job = j })
    }
  ]...)
}

resource "azurerm_role_assignment" "packages_reader" {
  for_each = var.onprem_workers

  scope                = var.container_ids[each.value.packages_container]
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = local.principal_ids[each.key]
}

resource "azurerm_role_assignment" "secret_user" {
  for_each = local.secrets

  scope                = "${var.key_vault_ids[each.value.key_vault]}/secrets/${each.value.secret}"
  role_definition_name = "Key Vault Secrets User"
  principal_id         = local.principal_ids[each.value.worker]
}

resource "azurerm_monitor_scheduled_query_rules_alert_v2" "run_failed" {
  for_each = local.jobs

  name                    = each.value.run_failed_alert_name
  resource_group_name     = var.resource_group_name
  location                = var.location
  scopes                  = [var.app_insights_ids[var.onprem_workers[each.value.worker].app_insights]]
  description             = "${var.onprem_workers[each.value.worker].display_name} recorded a failed ${each.value.job} run. Its log in Application Insights holds the error."
  severity                = 1
  evaluation_frequency    = "PT15M"
  window_duration         = "PT15M"
  auto_mitigation_enabled = true
  tags                    = merge(var.tags, { service = each.value.worker })

  criteria {
    query                   = "customMetrics | where name == \"${var.onprem_workers[each.value.worker].runs_metric_name}\" and tostring(customDimensions.job) == \"${each.value.job}\" and tostring(customDimensions.outcome) == \"failed\""
    time_aggregation_method = "Count"
    operator                = "GreaterThan"
    threshold               = 0
  }

  action {
    action_groups = [var.action_group_ids[var.onprem_workers[each.value.worker].action_group]]
  }
}

# Alerting on the absence of a success also covers a stopped service, a machine that is down and telemetry
# that never arrives, none of which records a failure.
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "no_run" {
  for_each = local.jobs

  name                    = each.value.no_run_alert_name
  resource_group_name     = var.resource_group_name
  location                = var.location
  scopes                  = [var.app_insights_ids[var.onprem_workers[each.value.worker].app_insights]]
  description             = "${var.onprem_workers[each.value.worker].display_name} recorded no successful ${each.value.job} run in the last 24 hours."
  severity                = 1
  evaluation_frequency    = "PT1H"
  window_duration         = "P1D"
  auto_mitigation_enabled = true
  tags                    = merge(var.tags, { service = each.value.worker })

  criteria {
    query                   = "customMetrics | where name == \"${var.onprem_workers[each.value.worker].runs_metric_name}\" and tostring(customDimensions.job) == \"${each.value.job}\" and tostring(customDimensions.outcome) == \"succeeded\""
    time_aggregation_method = "Count"
    operator                = "LessThan"
    threshold               = 1
  }

  action {
    action_groups = [var.action_group_ids[var.onprem_workers[each.value.worker].action_group]]
  }
}

output "host_principal_ids" { value = local.principal_ids }

# Everything a worker's install job needs, so the job reads one output instead of several units' state.
output "deployments" {
  sensitive = true
  value = {
    for k, w in var.onprem_workers : k => {
      arc_machine_name               = data.azurerm_arc_machine.this[k].name
      arc_resource_group_name        = w.arc_resource_group
      arc_location                   = data.azurerm_arc_machine.this[k].location
      package_container_url          = "${var.account_blob_endpoints[w.packages_account]}${w.packages_container}"
      service_bus_fqdn               = var.service_bus_fqdns[w.service_bus]
      key_vault_uri                  = var.key_vault_uris[w.key_vault]
      app_insights_connection_string = var.app_insights_connection_strings[w.app_insights]
      ledger_blob_endpoint           = try(w.ledger_blob_endpoint, "")
    }
  }
}
