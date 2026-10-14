include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/blob-event-subscriptions"
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
    ids = { archive = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Storage/storageAccounts/mockaccount" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

dependency "servicebus_queues" {
  config_path = "../servicebus-queues"
  mock_outputs = {
    ids = { "metr.receive-blob-created" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.ServiceBus/namespaces/mock/queues/mock" }
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

# The container must exist before a subject filter can match anything written to it.
dependencies {
  paths = ["../storage-containers"]
}

inputs = {
  resource_group_name      = dependency.resource_groups.outputs.names["main"]
  location                 = include.resources.locals.env.location
  tags                     = include.resources.locals.tags
  service_bus_namespace_id = dependency.platform_servicebus_namespaces.outputs.ids["shared"]
  account_ids              = dependency.storage_accounts.outputs.ids
  queue_ids                = dependency.servicebus_queues.outputs.ids
  max_delivery_attempts    = include.resources.locals.env.max_delivery_count
  blob_event_subscriptions = {
    for k, s in include.resources.locals.instances : k => merge(s, { name = "${include.resources.locals.prefix}egst${k}" })
  }
}
