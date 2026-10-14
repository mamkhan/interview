variable "location" { type = string }
variable "tags" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "resource_groups" {
  type = any

  validation {
    condition     = alltrue([for g in values(var.resource_groups) : length(setsubtract(keys(g), ["name"])) == 0])
    error_message = "A resource group file sets nothing: the name comes from the file name."
  }
}

resource "azurerm_resource_group" "this" {
  for_each = var.resource_groups

  name     = each.value.name
  location = var.location
  tags     = var.tags
}

output "names" { value = { for k, g in azurerm_resource_group.this : k => g.name } }
output "ids" { value = { for k, g in azurerm_resource_group.this : k => g.id } }
output "locations" { value = { for k, g in azurerm_resource_group.this : k => g.location } }
