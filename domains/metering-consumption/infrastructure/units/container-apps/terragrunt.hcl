include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/container-apps"
}

# Pure ordering: a host must find its queue, topics, subscriptions and containers at first boot.
dependencies {
  paths = ["../servicebus-queues", "../servicebus-topics", "../servicebus-subscriptions", "../storage-containers"]
}

dependency "resource_groups" {
  config_path = "../resource-groups"
  mock_outputs = {
    names = { main = "mock-rg" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "managed_identities" {
  config_path = "../managed-identities"
  mock_outputs = {
    ids = {
      marketmsg = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mock"
      receiver  = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mockreceiver"
    }
    client_ids = {
      marketmsg = "00000000-0000-0000-0000-000000000000"
      receiver  = "00000000-0000-0000-0000-000000000000"
    }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

# A database and the setting that reads it land in one push, so the plan runs before the database is in state: a key
# missing from the real map is taken from the mock. The apply reads the real output, the database applied first.
dependency "sql_databases" {
  config_path = "../sql-databases"
  mock_outputs = {
    connection_strings = {
      "metering-consumption" = "Server=tcp:mock.database.windows.net,1433;Initial Catalog=mock;Authentication=Active Directory Default;Encrypt=True;"
      "reference-data"       = "Server=tcp:mock.database.windows.net,1433;Initial Catalog=mock;Authentication=Active Directory Default;Encrypt=True;"
    }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "deep_map_only"
}

dependency "storage_accounts" {
  config_path = "../storage-accounts"
  mock_outputs = {
    blob_endpoints = { archive = "https://mockaccount.blob.core.windows.net/", ledger = "https://mockledger.blob.core.windows.net/" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "platform_application_insights" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/application-insights"
  mock_outputs = {
    ids                = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Insights/components/mock" }
    connection_strings = { shared = "InstrumentationKey=00000000-0000-0000-0000-000000000000" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "platform_container_apps_environments" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/container-apps-environments"
  mock_outputs = {
    ids = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.App/managedEnvironments/mock" }
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
  resource_group_name           = dependency.resource_groups.outputs.names["main"]
  tags                          = include.resources.locals.tags
  container_apps_environment_id = dependency.platform_container_apps_environments.outputs.ids["shared"]
  registry_login_server         = dependency.platform_container_registries.outputs.login_servers["shared"]
  identity_ids                  = dependency.managed_identities.outputs.ids
  refs = merge(
    {
      "service_bus.fqdn"                          = dependency.platform_servicebus_namespaces.outputs.fqdns["shared"]
      "monitoring.app_insights_connection_string" = dependency.platform_application_insights.outputs.connection_strings["shared"]
    },
    { for k, v in dependency.storage_accounts.outputs.blob_endpoints : "storage_accounts.${k}.blob_endpoint" => v },
    { for k, v in dependency.sql_databases.outputs.connection_strings : "sql_databases.${k}.connection_string" => v },
    { for k, v in dependency.managed_identities.outputs.client_ids : "managed_identities.${k}.client_id" => v },
  )
  container_apps = {
    for k, a in include.resources.locals.instances : k => merge(a, { name = "${include.resources.locals.prefix}ca${k}" })
  }
}
