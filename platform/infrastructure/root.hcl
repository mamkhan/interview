# Root Terragrunt configuration shared by both layers: generates the azurerm backend and provider
# for every unit. ARM_SUBSCRIPTION_ID must be set: locally from `az account show --query id`, in
# the pipeline from the service connection.
#
# Each environment keeps its own backend, named for that environment per ADR-009 and created by
# bootstrap/bootstrap-state.sh, so an environment's state cannot land in another's account whether
# or not the environments share a subscription. Within a backend, state is isolated per path
# (layer, environment, unit). Access is Entra ID (data-plane RBAC), never account keys.
#
# Every unit sits under `<layer>/infrastructure/units/<type>/`, serves every environment and takes it from
# ACME_ENVIRONMENT. The state key is the unit's repository path made layer-first: `<layer>/<env>/<type>`.
locals {
  environment = get_env("ACME_ENVIRONMENT")
}

remote_state {
  backend = "azurerm"
  generate = {
    path      = "backend.tf"
    if_exists = "overwrite_terragrunt"
  }
  config = {
    resource_group_name  = "acm${local.environment}platrgtfstate"
    storage_account_name = "acm${local.environment}platsttfstate"
    container_name       = "tfstate"
    key                  = "${replace(replace(get_path_from_repo_root(), "\\", "/"), "infrastructure/units/", "${local.environment}/")}/terraform.tfstate"
    subscription_id      = get_env("ARM_SUBSCRIPTION_ID")
    use_azuread_auth     = true
  }
}

generate "provider" {
  path      = "provider.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<PROVIDER
terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm", version = "~> 4.0" }
  }
}
provider "azurerm" {
  features {}
  subscription_id = "${get_env("ARM_SUBSCRIPTION_ID")}"
}
PROVIDER
}
