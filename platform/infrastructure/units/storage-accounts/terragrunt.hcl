include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/storage-accounts"

  # An Entra-only account refuses keys, and the provider reads its queue and website settings from the data plane
  # on every refresh, so this unit's provider signs in to storage with Entra ID.
  extra_arguments "storage_entra_id" {
    commands = get_terraform_commands_that_need_vars()
    env_vars = {
      ARM_STORAGE_USE_AZUREAD = "true"
    }
  }
}

dependency "resource_groups" {
  config_path = "../resource-groups"
  mock_outputs = {
    names = { main = "mock-rg" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

inputs = {
  resource_group_name       = dependency.resource_groups.outputs.names["main"]
  location                  = include.resources.locals.env.location
  tags                      = include.resources.locals.tags
  test_writer_principal_ids = []
  storage_accounts = {
    for k, a in include.resources.locals.instances : k => merge(a, { name = "${include.resources.locals.prefix}st${k}" })
  }
}
