variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }
variable "workspace_ids" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "container_apps_environments" {
  type = any

  validation {
    condition = alltrue([
      for e in values(var.container_apps_environments) :
      length(setsubtract(["name", "log_analytics_workspace"], keys(e))) == 0 &&
      length(setsubtract(keys(e), ["name", "log_analytics_workspace"])) == 0
    ])
    error_message = "A Container Apps environment file sets log_analytics_workspace; nothing else."
  }
}

resource "azurerm_container_app_environment" "this" {
  for_each = var.container_apps_environments

  name                       = each.value.name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = var.workspace_ids[each.value.log_analytics_workspace]
  tags                       = var.tags

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
    minimum_count         = 0
    maximum_count         = 0
  }
}

output "ids" { value = { for k, e in azurerm_container_app_environment.this : k => e.id } }
output "names" { value = { for k, e in azurerm_container_app_environment.this : k => e.name } }
output "default_domains" { value = { for k, e in azurerm_container_app_environment.this : k => e.default_domain } }
