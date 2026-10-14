include "root" {
  path = "${get_repo_root()}/platform/infrastructure/root.hcl"
}

include "resources" {
  path   = "${get_repo_root()}/platform/infrastructure/resources.hcl"
  expose = true
}

terraform {
  source = "${get_repo_root()}/platform/infrastructure/_modules/resource-groups"
}

inputs = {
  location = include.resources.locals.env.location
  tags     = include.resources.locals.tags
  resource_groups = {
    for k, g in include.resources.locals.instances : k => merge(g, { name = "${include.resources.locals.prefix}rg${k}" })
  }
}
