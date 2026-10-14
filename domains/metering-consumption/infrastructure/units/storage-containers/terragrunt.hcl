include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/storage-containers"
}

dependency "platform_onprem_workers" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/onprem-workers"
  mock_outputs = {
    host_principal_ids = { "v1-reader" = "00000000-0000-0000-0000-000000000000" }
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

inputs = {
  account_ids                 = dependency.storage_accounts.outputs.ids
  storage_containers          = include.resources.locals.instances
  onprem_writer_principal_ids = [dependency.platform_onprem_workers.outputs.host_principal_ids["v1-reader"]]
}
