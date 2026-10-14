include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/servicebus-queues"
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
  namespace_id       = dependency.platform_servicebus_namespaces.outputs.ids["shared"]
  max_delivery_count = include.resources.locals.env.max_delivery_count
  queues             = include.resources.locals.instances
}
