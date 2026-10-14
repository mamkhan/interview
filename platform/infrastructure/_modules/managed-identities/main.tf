variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }

# The resource ids a role may be granted on, by the name an identity file uses for them.
variable "scopes" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion, and a scope missing from var.scopes fails it too.
variable "managed_identities" {
  type = any

  validation {
    condition = alltrue([
      for i in values(var.managed_identities) :
      length(setsubtract(["name", "service", "roles"], keys(i))) == 0 &&
      length(setsubtract(keys(i), ["name", "service", "roles"])) == 0 &&
      alltrue([for r in i.roles : length(setsubtract(keys(r), ["role", "scope"])) == 0 && length(keys(r)) == 2])
    ])
    error_message = "An identity file sets service and roles, each role a role and a scope; nothing else."
  }
}

locals {
  grants = merge([
    for k, i in var.managed_identities : {
      for r in i.roles : "${k}/${r.scope}/${r.role}" => { identity = k, scope = var.scopes[r.scope], role = r.role }
    }
  ]...)
}

resource "azurerm_user_assigned_identity" "this" {
  for_each = var.managed_identities

  name                = each.value.name
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = merge(var.tags, { service = each.value.service })
}

resource "azurerm_role_assignment" "this" {
  for_each = local.grants

  scope                = each.value.scope
  role_definition_name = each.value.role
  principal_id         = azurerm_user_assigned_identity.this[each.value.identity].principal_id
}

output "ids" { value = { for k, i in azurerm_user_assigned_identity.this : k => i.id } }
output "client_ids" { value = { for k, i in azurerm_user_assigned_identity.this : k => i.client_id } }
output "principal_ids" { value = { for k, i in azurerm_user_assigned_identity.this : k => i.principal_id } }
output "names" { value = { for k, i in azurerm_user_assigned_identity.this : k => i.name } }
