include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/sql-databases"
}

dependency "sql_servers" {
  config_path = "../sql-servers"
  mock_outputs = {
    ids   = { main = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/mock/providers/Microsoft.Sql/servers/mock" }
    fqdns = { main = "mock.database.windows.net" }
  }
  mock_outputs_allowed_terraform_commands = ["validate", "plan"]
  mock_outputs_merge_strategy_with_state  = "shallow"
}

inputs = {
  tags          = include.resources.locals.tags
  server_ids    = dependency.sql_servers.outputs.ids
  server_fqdns  = dependency.sql_servers.outputs.fqdns
  sql_databases = include.resources.locals.instances
}
