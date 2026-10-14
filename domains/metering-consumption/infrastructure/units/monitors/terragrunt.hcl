include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/monitors"
}

dependency "resource_groups" {
  config_path = "../resource-groups"
  mock_outputs = {
    names = { main = "mock-rg" }
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

dependency "platform_log_analytics_workspaces" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/log-analytics-workspaces"
  mock_outputs = {
    ids = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.OperationalInsights/workspaces/mock" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "platform_monitor_action_groups" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/monitor-action-groups"
  mock_outputs = {
    ids = { ops = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Insights/actionGroups/mock" }
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
  resource_group_name        = dependency.resource_groups.outputs.names["main"]
  location                   = include.resources.locals.env.location
  tags                       = include.resources.locals.tags
  display_prefix             = "${include.resources.locals.layer.domain_full} (${include.resources.locals.environment})"
  service_bus_namespace_id   = dependency.platform_servicebus_namespaces.outputs.ids["shared"]
  action_group_id            = dependency.platform_monitor_action_groups.outputs.ids["ops"]
  app_insights_id            = dependency.platform_application_insights.outputs.ids["shared"]
  log_analytics_workspace_id = dependency.platform_log_analytics_workspaces.outputs.ids["shared"]
  monitors = {
    for k, o in include.resources.locals.instances : k => merge(o, {
      alert_name    = "${include.resources.locals.prefix}alrt${k}"
      workbook_seed = "${include.resources.locals.prefix}${o.workbook}"
    })
  }
}
