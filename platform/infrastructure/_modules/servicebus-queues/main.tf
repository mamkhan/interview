variable "namespace_id" { type = string }
variable "max_delivery_count" { type = number }

# Keyed by entity name. A misspelt key fails the plan instead of being dropped by type conversion.
variable "queues" {
  type = any

  validation {
    condition     = alltrue([for q in values(var.queues) : length(setsubtract(keys(q), ["requires_session"])) == 0])
    error_message = "A queue file may set requires_session; nothing else."
  }
}

resource "azurerm_servicebus_queue" "this" {
  for_each = var.queues

  name               = each.key
  namespace_id       = var.namespace_id
  max_delivery_count = var.max_delivery_count
  requires_session   = try(each.value.requires_session, false)

  # A queue an external publisher writes to keeps its poison messages rather than dropping them: the
  # dead-letter queue is where an operator file that no delivery could handle is found again.
  dead_lettering_on_message_expiration = true
}

output "ids" { value = { for k, q in azurerm_servicebus_queue.this : k => q.id } }
output "names" { value = { for k, q in azurerm_servicebus_queue.this : k => q.name } }
