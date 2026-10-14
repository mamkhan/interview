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

dependency "storage_accounts" {
  config_path = "../storage-accounts"
  mock_outputs = {
    ids = { packages = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Storage/storageAccounts/mock" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

inputs = {
  account_ids            = dependency.storage_accounts.outputs.ids
  uploader_principal_ids = [include.resources.locals.env.deployer_object_id]
  storage_containers     = include.resources.locals.instances
}
