# Shared naming and tagging. Names are composed by Terragrunt per the naming standard (ADR-009):
# org + environment + domain + resource type + instance, flat lowercase alphanumeric. Nothing is
# typed by hand in a module.
locals {
  org = "acm"
  base_tags = {
    managed-by = "terragrunt"
  }
}
