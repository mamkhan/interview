variable "resource_group_name" { type = string }
variable "tags" { type = map(string) }
variable "container_apps_environment_id" { type = string }
variable "registry_login_server" { type = string }

# The identities an app may run as, by the name an app file uses for them.
variable "identity_ids" { type = map(string) }

# The values a setting may read with `{ from: <ref> }`, by ref. A ref missing here fails the plan.
variable "refs" { type = map(string) }

# Keyed by instance, each carrying the name the unit composed. A misspelt key fails the plan instead of being
# dropped by type conversion, and so does a setting that is neither a literal nor a single `from`.
variable "container_apps" {
  type = any

  validation {
    condition = alltrue([
      for a in values(var.container_apps) :
      length(setsubtract(["name", "identity", "service", "cpu", "memory", "min_replicas", "max_replicas", "settings"], keys(a))) == 0 &&
      length(setsubtract(keys(a), ["name", "identity", "service", "cpu", "memory", "min_replicas", "max_replicas", "settings"])) == 0 &&
      alltrue([for v in values(a.settings) : can(tostring(v)) || try(length(keys(v)) == 1 && contains(keys(v), "from"), false)])
    ])
    error_message = "An app file sets identity, service, cpu, memory, min_replicas, max_replicas and settings; a setting is a literal or { from: <ref> }."
  }
}

# First apply runs a public sample that listens on 8080 like the real hosts; the pipeline is the
# only thing that ever changes the image (ignore_changes below keeps re-plans clean afterwards).
variable "image" {
  type    = string
  default = "mcr.microsoft.com/dotnet/samples:aspnetapp"
}
variable "target_port" {
  type    = number
  default = 8080
}

resource "azurerm_container_app" "this" {
  for_each = var.container_apps

  name                         = each.value.name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_apps_environment_id
  revision_mode                = "Single"
  tags                         = merge(var.tags, { service = each.value.service })

  workload_profile_name = "Consumption"

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identity_ids[each.value.identity]]
  }

  registry {
    server   = var.registry_login_server
    identity = var.identity_ids[each.value.identity]
  }

  template {
    min_replicas = each.value.min_replicas
    max_replicas = each.value.max_replicas

    container {
      name   = each.value.name
      image  = var.image
      cpu    = each.value.cpu
      memory = each.value.memory

      dynamic "env" {
        for_each = { for n, v in each.value.settings : n => can(v.from) ? var.refs[v.from] : tostring(v) }
        content {
          name  = env.key
          value = env.value
        }
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = var.target_port
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }
}

output "fqdns" { value = { for k, a in azurerm_container_app.this : k => a.ingress[0].fqdn } }
output "names" { value = { for k, a in azurerm_container_app.this : k => a.name } }
output "ids" { value = { for k, a in azurerm_container_app.this : k => a.id } }
