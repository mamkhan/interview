include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/managed-identities"
}

dependency "resource_groups" {
  config_path = "../resource-groups"
  mock_outputs = {
    names = { main = "mock-rg" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "storage_accounts" {
  config_path = "../storage-accounts"
  mock_outputs = {
    ids = {
      archive = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Storage/storageAccounts/mockaccount"
      ledger  = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Storage/storageAccounts/mockledger"
    }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "platform_container_registries" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/container-registries"
  mock_outputs = {
    ids           = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.ContainerRegistry/registries/mock" }
    login_servers = { shared = "mock.azurecr.io" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "platform_key_vaults" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/key-vaults"
  mock_outputs = {
    ids = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.KeyVault/vaults/mock" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "platform_servicebus_namespaces" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/servicebus-namespaces"
  mock_outputs = {
    ids   = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.ServiceBus/namespaces/mock" }
    fqdns = { shared = "mock.servicebus.windows.net" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

inputs = {
  resource_group_name = dependency.resource_groups.outputs.names["main"]
  location            = include.resources.locals.env.location
  tags                = include.resources.locals.tags
  scopes = merge(
    {
      service_bus        = dependency.platform_servicebus_namespaces.outputs.ids["shared"]
      container_registry = dependency.platform_container_registries.outputs.ids["shared"]
      key_vault          = dependency.platform_key_vaults.outputs.ids["shared"]
    },
    { for k, id in dependency.storage_accounts.outputs.ids : "storage_accounts.${k}" => id },
  )
  managed_identities = {
    for k, i in include.resources.locals.instances : k => merge(i, { name = "${include.resources.locals.prefix}id${k}" })
  }
}
