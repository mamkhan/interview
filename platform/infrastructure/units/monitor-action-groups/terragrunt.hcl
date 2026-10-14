include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/monitor-action-groups"
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
  monitor_action_groups = {
    for k, g in include.resources.locals.instances : k => merge(g, { name = "${include.resources.locals.prefix}ag${k}" })
  }
}
