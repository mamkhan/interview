variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "key_vaults" {
  type = any

  validation {
    condition = alltrue([
      for v in values(var.key_vaults) :
      length(setsubtract(["name", "purge_protection"], keys(v))) == 0 &&
      length(setsubtract(keys(v), ["name", "purge_protection"])) == 0
    ])
    error_message = "A key vault file sets purge_protection; nothing else."
  }
}

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  for_each = var.key_vaults

  name                       = each.value.name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  purge_protection_enabled   = each.value.purge_protection
  soft_delete_retention_days = 7
  tags                       = var.tags
}

output "ids" { value = { for k, v in azurerm_key_vault.this : k => v.id } }
output "names" { value = { for k, v in azurerm_key_vault.this : k => v.name } }
output "vault_uris" { value = { for k, v in azurerm_key_vault.this : k => v.vault_uri } }
