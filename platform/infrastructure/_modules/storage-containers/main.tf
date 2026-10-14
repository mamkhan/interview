variable "account_ids" { type = map(string) }

# The principals that upload into each container that accepts uploads: the pipeline, which publishes on-premises
# packages there. Empty by default.
variable "uploader_principal_ids" {
  type    = list(string)
  default = []
}

# The on-premises workers that write into each container that accepts them: the V1 reader, while it loads V1's
# history into Metering's ledger. A container file sets the flag only for the load, in the environment's override.
variable "onprem_writer_principal_ids" {
  type    = list(string)
  default = []
}

# Keyed by container name. A misspelt key fails the plan instead of being dropped by type conversion.
variable "storage_containers" {
  type = any

  validation {
    condition = alltrue([
      for c in values(var.storage_containers) :
      length(setsubtract(["account", "retention_days"], keys(c))) == 0 &&
      length(setsubtract(keys(c), ["account", "retention_days", "accepts_uploads", "accepts_onprem_writers"])) == 0
    ])
    error_message = "A container file sets account and retention_days and may set accepts_uploads and accepts_onprem_writers; nothing else."
  }
}

locals {
  uploaders = {
    for pair in setproduct([for k, c in var.storage_containers : k if try(c.accepts_uploads, false)], var.uploader_principal_ids) :
    "${pair[0]}/${pair[1]}" => { container = pair[0], principal_id = pair[1] }
  }

  onprem_writers = {
    for pair in setproduct([for k, c in var.storage_containers : k if try(c.accepts_onprem_writers, false)], var.onprem_writer_principal_ids) :
    "${pair[0]}/${pair[1]}" => { container = pair[0], principal_id = pair[1] }
  }
}

resource "azurerm_storage_container" "this" {
  for_each = var.storage_containers

  name               = each.key
  storage_account_id = var.account_ids[each.value.account]
}

# Unlocked time-based WORM: writes are immutable for the retention window, and the policy itself
# stays deletable. Locking it (irreversible) is an operations decision taken per environment. The
# window is not a deletion schedule; nothing here ever deletes a blob. Zero days means no policy,
# which is what a container of replaceable build artefacts wants.
resource "azurerm_storage_container_immutability_policy" "this" {
  for_each = { for k, c in var.storage_containers : k => c if c.retention_days > 0 }

  storage_container_resource_manager_id = azurerm_storage_container.this[each.key].resource_manager_id
  immutability_period_in_days           = each.value.retention_days
}

resource "azurerm_role_assignment" "uploader" {
  for_each = local.uploaders

  scope                = azurerm_storage_container.this[each.value.container].id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = each.value.principal_id
}

resource "azurerm_role_assignment" "onprem_writer" {
  for_each = local.onprem_writers

  scope                = azurerm_storage_container.this[each.value.container].id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = each.value.principal_id
}

output "names" { value = { for k, c in azurerm_storage_container.this : k => c.name } }
output "ids" { value = { for k, c in azurerm_storage_container.this : k => c.id } }
