include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/servicebus-topics"
}

dependency "platform_servicebus_namespaces" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/servicebus-namespaces"
  mock_outputs = {
    ids = { shared = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.ServiceBus/namespaces/mock" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "platform_onprem_workers" {
  config_path = "${get_repo_root()}/platform/infrastructure/units/onprem-workers"
  mock_outputs = {
    host_principal_ids = { "v1-reader" = "00000000-0000-0000-0000-000000000000" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

inputs = {
  namespace_id                = dependency.platform_servicebus_namespaces.outputs.ids["shared"]
  topics                      = include.resources.locals.instances
  test_sender_principal_ids   = []
  onprem_sender_principal_ids = [dependency.platform_onprem_workers.outputs.host_principal_ids["v1-reader"]]
}
