variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }
variable "workspace_ids" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "servicebus_namespaces" {
  type = any

  validation {
    condition = alltrue([
      for n in values(var.servicebus_namespaces) :
      length(setsubtract(["name", "sku", "capacity", "log_analytics_workspace"], keys(n))) == 0 &&
      length(setsubtract(keys(n), ["name", "sku", "capacity", "log_analytics_workspace"])) == 0
    ])
    error_message = "A namespace file sets sku, capacity and log_analytics_workspace; nothing else."
  }
}

resource "azurerm_servicebus_namespace" "this" {
  for_each = var.servicebus_namespaces

  name                = each.value.name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = each.value.sku
  capacity            = each.value.capacity
  # Entra ID only: no SAS keys anywhere in the platform (secretless configuration rule).
  local_auth_enabled = false
  tags               = var.tags
}

resource "azurerm_monitor_diagnostic_setting" "metrics" {
  for_each = var.servicebus_namespaces

  name                       = "metrics-to-log-analytics"
  target_resource_id         = azurerm_servicebus_namespace.this[each.key].id
  log_analytics_workspace_id = var.workspace_ids[each.value.log_analytics_workspace]

  enabled_metric {
    category = "AllMetrics"
  }
}

output "ids" { value = { for k, n in azurerm_servicebus_namespace.this : k => n.id } }
output "names" { value = { for k, n in azurerm_servicebus_namespace.this : k => n.name } }
output "fqdns" { value = { for k, n in azurerm_servicebus_namespace.this : k => "${n.name}.servicebus.windows.net" } }
