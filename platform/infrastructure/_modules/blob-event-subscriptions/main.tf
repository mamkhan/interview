variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }
variable "service_bus_namespace_id" { type = string }
variable "account_ids" { type = map(string) }
variable "queue_ids" { type = map(string) }
variable "max_delivery_attempts" { type = number }

# Keyed by instance, each carrying the system topic name the unit composed. A misspelt key fails the plan instead
# of being dropped by type conversion.
variable "blob_event_subscriptions" {
  type = any

  validation {
    condition = alltrue([
      for s in values(var.blob_event_subscriptions) :
      length(setsubtract(["name", "service", "account", "container", "queue"], keys(s))) == 0 &&
      length(setsubtract(keys(s), ["name", "service", "account", "container", "queue"])) == 0
    ])
    error_message = "A blob event subscription file sets service, account, container and queue; nothing else."
  }
}

resource "azurerm_eventgrid_system_topic" "this" {
  for_each = var.blob_event_subscriptions

  name                   = each.value.name
  resource_group_name    = var.resource_group_name
  location               = var.location
  source_arm_resource_id = var.account_ids[each.value.account]
  topic_type             = "Microsoft.Storage.StorageAccounts"
  tags                   = merge(var.tags, { service = each.value.service })

  identity {
    type = "SystemAssigned"
  }
}

# Delivery authenticates as the topic's own identity, so no SAS token or key exists for the queue. The
# subscription below cannot deliver until this assignment exists, hence depends_on.
resource "azurerm_role_assignment" "queue_sender" {
  for_each = var.blob_event_subscriptions

  scope                = var.service_bus_namespace_id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azurerm_eventgrid_system_topic.this[each.key].identity[0].principal_id
}

resource "azurerm_eventgrid_system_topic_event_subscription" "this" {
  for_each = var.blob_event_subscriptions

  name                = each.value.container
  system_topic        = azurerm_eventgrid_system_topic.this[each.key].name
  resource_group_name = var.resource_group_name

  # CloudEvents 1.0 because the consumer deserialises the CloudEvents body; the Event Grid schema
  # would put the same fields under a different shape and the handler would read nulls.
  event_delivery_schema         = "CloudEventSchemaV1_0"
  included_event_types          = ["Microsoft.Storage.BlobCreated"]
  service_bus_queue_endpoint_id = var.queue_ids[each.value.queue]

  # One account carries the landing container beside the archive, so the filter is what keeps a sealed
  # blob from being announced back into the path that sealed it.
  subject_filter {
    subject_begins_with = "/blobServices/default/containers/${each.value.container}/"
  }

  delivery_identity {
    type = "SystemAssigned"
  }

  retry_policy {
    max_delivery_attempts = var.max_delivery_attempts
    event_time_to_live    = 1440
  }

  depends_on = [azurerm_role_assignment.queue_sender]
}

output "system_topic_names" { value = { for k, t in azurerm_eventgrid_system_topic.this : k => t.name } }
output "principal_ids" { value = { for k, t in azurerm_eventgrid_system_topic.this : k => t.identity[0].principal_id } }
