include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/onprem-workers"
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
    blob_endpoints = { packages = "https://mock.blob.core.windows.net/" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "storage_containers" {
  config_path = "../storage-containers"
  mock_outputs = {
    ids = { onprem = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Storage/storageAccounts/mock/blobServices/default/containers/onprem" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "key_vaults" {
  config_path = "../key-vaults"
  mock_outputs = {
    ids        = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.KeyVault/vaults/mock" }
    vault_uris = { shared = "https://mock.vault.azure.net/" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "servicebus_namespaces" {
  config_path = "../servicebus-namespaces"
  mock_outputs = {
    fqdns = { shared = "mock.servicebus.windows.net" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "application_insights" {
  config_path = "../application-insights"
  mock_outputs = {
    ids                = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Insights/components/mock" }
    connection_strings = { shared = "InstrumentationKey=00000000-0000-0000-0000-000000000000" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "monitor_action_groups" {
  config_path = "../monitor-action-groups"
  mock_outputs = {
    ids = { ops = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Insights/actionGroups/mock" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

inputs = {
  resource_group_name             = dependency.resource_groups.outputs.names["main"]
  location                        = include.resources.locals.env.location
  tags                            = include.resources.locals.tags
  account_blob_endpoints          = dependency.storage_accounts.outputs.blob_endpoints
  container_ids                   = dependency.storage_containers.outputs.ids
  key_vault_ids                   = dependency.key_vaults.outputs.ids
  key_vault_uris                  = dependency.key_vaults.outputs.vault_uris
  service_bus_fqdns               = dependency.servicebus_namespaces.outputs.fqdns
  app_insights_ids                = dependency.application_insights.outputs.ids
  app_insights_connection_strings = dependency.application_insights.outputs.connection_strings
  action_group_ids                = dependency.monitor_action_groups.outputs.ids
  # A ledger account is another domain's storage account, named from that domain's code.
  onprem_workers = {
    for k, w in include.resources.locals.instances : k => merge({ for key, value in w : key => value if key != "ledger_account" }, {
      scheduled_jobs = {
        for j in w.scheduled_jobs : j => {
          run_failed_alert_name = "${include.resources.locals.prefix}alrt${replace(k, "-", "")}${replace(j, "-", "")}failed"
          no_run_alert_name     = "${include.resources.locals.prefix}alrt${replace(k, "-", "")}${replace(j, "-", "")}norun"
        }
      }
      }, try({
        ledger_blob_endpoint = "https://${include.resources.locals.common.org}${include.resources.locals.environment}${read_terragrunt_config("${get_repo_root()}/domains/${w.ledger_account.domain}/infrastructure/layer.hcl").locals.domain_code}st${w.ledger_account.instance}.blob.core.windows.net/"
    }, {}))
  }
}
