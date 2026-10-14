variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }
variable "workspace_ids" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "application_insights" {
  type = any

  validation {
    condition = alltrue([
      for a in values(var.application_insights) :
      length(setsubtract(["name", "workspace"], keys(a))) == 0 &&
      length(setsubtract(keys(a), ["name", "workspace"])) == 0
    ])
    error_message = "An Application Insights file sets workspace; nothing else."
  }
}

resource "azurerm_application_insights" "this" {
  for_each = var.application_insights

  name                = each.value.name
  location            = var.location
  resource_group_name = var.resource_group_name
  workspace_id        = var.workspace_ids[each.value.workspace]
  application_type    = "web"
  tags                = var.tags
}

output "ids" { value = { for k, a in azurerm_application_insights.this : k => a.id } }
output "names" { value = { for k, a in azurerm_application_insights.this : k => a.name } }
output "connection_strings" {
  value     = { for k, a in azurerm_application_insights.this : k => a.connection_string }
  sensitive = true
}
