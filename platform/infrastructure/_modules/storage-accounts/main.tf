variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }

# Principals that write directly into an account rather than through a host identity: the acceptance suite
# drops its operator file as the pipeline's own principal. Granted on each account that accepts test writers.
variable "test_writer_principal_ids" { type = list(string) }

# Keyed by instance, each carrying the name the unit composed. Replication and the hierarchical namespace
# cannot be left to a default that differs between accounts, so every file states them, and a misspelt key fails
# the plan instead of being dropped by type conversion.
variable "storage_accounts" {
  type = any

  validation {
    condition = alltrue([
      for a in values(var.storage_accounts) :
      contains(keys(a), "replication") && contains(keys(a), "hierarchical_namespace") &&
      length(setsubtract(keys(a), ["name", "replication", "hierarchical_namespace", "tier_container", "accepts_test_writers", "entra_only"])) == 0
    ])
    error_message = "A storage account file sets replication and hierarchical_namespace and may set tier_container, accepts_test_writers and entra_only; nothing else."
  }
}

locals {
  accounts = {
    for k, a in var.storage_accounts : k => {
      name                   = a.name
      replication            = a.replication
      hierarchical_namespace = a.hierarchical_namespace
      tier_container         = try(a.tier_container, null)
      accepts_test_writers   = try(a.accepts_test_writers, false)
      entra_only             = try(a.entra_only, false)
    }
  }

  test_writers = {
    for pair in setproduct([for k, a in local.accounts : k if a.accepts_test_writers], var.test_writer_principal_ids) :
    "${pair[0]}/${pair[1]}" => { account = pair[0], principal_id = pair[1] }
  }
}

resource "azurerm_storage_account" "this" {
  for_each = local.accounts

  name                            = each.value.name
  location                        = var.location
  resource_group_name             = var.resource_group_name
  account_tier                    = "Standard"
  account_replication_type        = each.value.replication
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  is_hns_enabled                  = each.value.hierarchical_namespace
  tags                            = var.tags

  # An entra_only account refuses keys and SAS, so every caller signs in with Entra ID. Null keeps the provider's
  # defaults for every other account.
  shared_access_key_enabled       = each.value.entra_only ? false : null
  default_to_oauth_authentication = each.value.entra_only ? true : null
}

resource "azurerm_role_assignment" "test_writer" {
  for_each = local.test_writers

  scope                = azurerm_storage_account.this[each.value.account].id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = each.value.principal_id
}

# Sealed market messages are kept forever, so the cost lever is tiering rather than deletion and no
# rule here ever deletes a blob. Cold rather than archive at the far end: archive needs rehydration
# measured in hours, which would turn a replay of old data into an overnight operation.
resource "azurerm_storage_management_policy" "tiering" {
  for_each = { for k, a in local.accounts : k => a if a.tier_container != null }

  storage_account_id = azurerm_storage_account.this[each.key].id

  rule {
    name    = "seal-tiering"
    enabled = true

    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["${each.value.tier_container}/"]
    }

    actions {
      base_blob {
        tier_to_cool_after_days_since_modification_greater_than = 30
        tier_to_cold_after_days_since_modification_greater_than = 180
      }
    }
  }
}

output "ids" { value = { for k, a in azurerm_storage_account.this : k => a.id } }
output "names" { value = { for k, a in azurerm_storage_account.this : k => a.name } }
output "blob_endpoints" { value = { for k, a in azurerm_storage_account.this : k => a.primary_blob_endpoint } }
output "dfs_endpoints" { value = { for k, a in azurerm_storage_account.this : k => a.primary_dfs_endpoint } }
