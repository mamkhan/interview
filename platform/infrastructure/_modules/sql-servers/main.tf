variable "resource_group_name" { type = string }
variable "tags" { type = map(string) }
variable "aad_admin_login" { type = string }
variable "aad_admin_object_id" { type = string }

# Keyed by instance, each carrying the name and location the unit resolved. A misspelt key fails the plan
# instead of being dropped by type conversion.
variable "sql_servers" {
  type = any

  validation {
    condition = alltrue([
      for s in values(var.sql_servers) :
      length(setsubtract(keys(s), ["name", "location"])) == 0
    ])
    error_message = "A SQL server file may set location; nothing else."
  }
}

data "azurerm_client_config" "current" {}

# Entra-only authentication: no SQL logins exist anywhere. The admin is the pipeline's deployer
# principal, which runs the migrations bundle and creates the domain's database users.
resource "azurerm_mssql_server" "this" {
  for_each = var.sql_servers

  name                = each.value.name
  location            = each.value.location
  resource_group_name = var.resource_group_name
  version             = "12.0"
  minimum_tls_version = "1.2"
  tags                = var.tags

  azuread_administrator {
    login_username              = var.aad_admin_login
    object_id                   = var.aad_admin_object_id
    tenant_id                   = data.azurerm_client_config.current.tenant_id
    azuread_authentication_only = true
  }
}

# Container Apps consumption egress has no static IP; hosted build agents are Azure IPs too.
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  for_each = var.sql_servers

  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.this[each.key].id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

output "ids" { value = { for k, s in azurerm_mssql_server.this : k => s.id } }
output "names" { value = { for k, s in azurerm_mssql_server.this : k => s.name } }
output "fqdns" { value = { for k, s in azurerm_mssql_server.this : k => s.fully_qualified_domain_name } }
