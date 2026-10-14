variable "tags" { type = map(string) }
variable "server_ids" { type = map(string) }
variable "server_fqdns" { type = map(string) }

# Keyed by database name. The SKU and size differ per environment, so every file states them, and a misspelt key
# fails the plan instead of being dropped by type conversion.
variable "sql_databases" {
  type = any

  validation {
    condition = alltrue([
      for d in values(var.sql_databases) :
      length(setsubtract(["server", "sku", "max_size_gb"], keys(d))) == 0 &&
      length(setsubtract(keys(d), ["server", "sku", "max_size_gb"])) == 0
    ])
    error_message = "A database file sets server, sku and max_size_gb; nothing else."
  }
}

resource "azurerm_mssql_database" "this" {
  for_each = var.sql_databases

  name           = each.key
  server_id      = var.server_ids[each.value.server]
  sku_name       = each.value.sku
  max_size_gb    = each.value.max_size_gb
  zone_redundant = false
  tags           = var.tags
}

output "ids" { value = { for k, d in azurerm_mssql_database.this : k => d.id } }
output "names" { value = { for k, d in azurerm_mssql_database.this : k => d.name } }

# Entra-only: a host signs in as its own identity, so the string carries no secret.
output "connection_strings" {
  value = {
    for k, d in var.sql_databases :
    k => "Server=tcp:${var.server_fqdns[d.server]},1433;Initial Catalog=${k};Authentication=Active Directory Default;Encrypt=True;"
  }
}
