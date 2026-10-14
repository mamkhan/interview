variable "resource_group_name" { type = string }
variable "tags" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "monitor_action_groups" {
  type = any

  validation {
    condition = alltrue([
      for g in values(var.monitor_action_groups) :
      length(setsubtract(["name", "short_name", "email"], keys(g))) == 0 &&
      length(setsubtract(keys(g), ["name", "short_name", "email"])) == 0
    ])
    error_message = "An action group file sets short_name and email; nothing else."
  }
}

resource "azurerm_monitor_action_group" "this" {
  for_each = var.monitor_action_groups

  name                = each.value.name
  resource_group_name = var.resource_group_name
  short_name          = each.value.short_name
  tags                = var.tags

  email_receiver {
    name                    = each.key
    email_address           = each.value.email
    use_common_alert_schema = true
  }
}

output "ids" { value = { for k, g in azurerm_monitor_action_group.this : k => g.id } }
output "names" { value = { for k, g in azurerm_monitor_action_group.this : k => g.name } }
