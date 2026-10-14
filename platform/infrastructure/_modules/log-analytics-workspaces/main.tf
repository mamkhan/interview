variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "log_analytics_workspaces" {
  type = any

  validation {
    condition = alltrue([
      for w in values(var.log_analytics_workspaces) :
      length(setsubtract(["name", "retention_days"], keys(w))) == 0 &&
      length(setsubtract(keys(w), ["name", "retention_days"])) == 0
    ])
    error_message = "A workspace file sets retention_days; nothing else."
  }
}

resource "azurerm_log_analytics_workspace" "this" {
  for_each = var.log_analytics_workspaces

  name                = each.value.name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = each.value.retention_days
  tags                = var.tags
}

output "ids" { value = { for k, w in azurerm_log_analytics_workspace.this : k => w.id } }
output "names" { value = { for k, w in azurerm_log_analytics_workspace.this : k => w.name } }
