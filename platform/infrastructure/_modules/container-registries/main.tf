variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "container_registries" {
  type = any

  validation {
    condition = alltrue([
      for r in values(var.container_registries) :
      length(setsubtract(["name", "sku"], keys(r))) == 0 &&
      length(setsubtract(keys(r), ["name", "sku"])) == 0
    ])
    error_message = "A registry file sets sku; nothing else."
  }
}

resource "azurerm_container_registry" "this" {
  for_each = var.container_registries

  name                = each.value.name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = each.value.sku
  admin_enabled       = false
  tags                = var.tags
}

output "ids" { value = { for k, r in azurerm_container_registry.this : k => r.id } }
output "names" { value = { for k, r in azurerm_container_registry.this : k => r.name } }
output "login_servers" { value = { for k, r in azurerm_container_registry.this : k => r.login_server } }
