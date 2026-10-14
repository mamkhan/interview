# Included, exposed, by every unit under `infrastructure/units/<type>/`. One unit serves every environment,
# chosen by ACME_ENVIRONMENT. Each instance is one YAML file under `base/variables/<type>/`, named for the
# instance; a file of the same name under `<env>/variables/<type>/` replaces top-level keys there, and a file
# only under an environment adds the instance there alone. The instance list lives in the type's state, so
# deleting a file destroys the instance and renaming one replaces it.
locals {
  environment = get_env("ACME_ENVIRONMENT")
  type        = basename(get_terragrunt_dir())
  layer_dir   = dirname(dirname(get_terragrunt_dir()))
  env         = read_terragrunt_config("${local.layer_dir}/${local.environment}/env.hcl").locals
  common      = read_terragrunt_config("${get_repo_root()}/platform/infrastructure/common.hcl").locals
  layer       = read_terragrunt_config("${local.layer_dir}/layer.hcl").locals

  # A unit appends its type code and the instance key to compose a resource name.
  prefix = "${local.common.org}${local.environment}${local.layer.domain_code}"
  tags = merge(local.common.base_tags, {
    domain      = local.layer.domain_full
    environment = local.environment
    owner       = local.layer.owner
    layer       = local.layer.layer
  })

  base_dir     = "${local.layer_dir}/base/variables/${local.type}"
  override_dir = "${local.layer_dir}/${local.environment}/variables/${local.type}"
  base         = { for f in fileset(local.base_dir, "*.yaml") : trimsuffix(f, ".yaml") => yamldecode(file("${local.base_dir}/${f}")) }
  overrides    = { for f in fileset(local.override_dir, "*.yaml") : trimsuffix(f, ".yaml") => yamldecode(file("${local.override_dir}/${f}")) }

  instances = {
    for name in distinct(concat(keys(local.base), keys(local.overrides))) :
    name => merge(lookup(local.base, name, {}), lookup(local.overrides, name, {}))
  }
}

# A working copy records the backend it was initialised against, so a copy shared between environments would
# point one environment's configuration at another's state.
download_dir = "${get_terragrunt_dir()}/.terragrunt-cache/${local.environment}"
