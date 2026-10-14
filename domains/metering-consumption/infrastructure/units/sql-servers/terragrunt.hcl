include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/sql-servers"
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
  resource_group_name = dependency.resource_groups.outputs.names["main"]
  tags                = include.resources.locals.tags
  aad_admin_login     = include.resources.locals.env.sql_aad_admin_login
  aad_admin_object_id = include.resources.locals.env.deployer_object_id
  sql_servers = {
    for k, s in include.resources.locals.instances : k => merge(
      { location = include.resources.locals.env.location },
      s,
      { name = "${include.resources.locals.prefix}sql${k}" },
    )
  }
}
