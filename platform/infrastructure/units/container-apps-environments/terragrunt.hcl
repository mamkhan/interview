include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/container-apps-environments"
}

dependency "resource_groups" {
  config_path = "../resource-groups"
  mock_outputs = {
    names = { main = "mock-rg" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "log_analytics_workspaces" {
  config_path = "../log-analytics-workspaces"
  mock_outputs = {
    ids = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.OperationalInsights/workspaces/mock" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

inputs = {
  resource_group_name = dependency.resource_groups.outputs.names["main"]
  location            = include.resources.locals.env.location
  tags                = include.resources.locals.tags
  workspace_ids       = dependency.log_analytics_workspaces.outputs.ids
  container_apps_environments = {
    for k, e in include.resources.locals.instances : k => merge(e, { name = "${include.resources.locals.prefix}cae${k}" })
  }
}
