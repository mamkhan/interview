variable "namespace_id" { type = string }
variable "max_delivery_count" { type = number }

# Keyed by entity name, on the domain's own topics or on a topic another domain publishes: the topic is named,
# not created. A session setting cannot change once a subscription exists, so every file states it, and a
# misspelt key fails the plan instead of being dropped by type conversion.
variable "subscriptions" {
  type = any

  validation {
    condition = alltrue([
      for s in values(var.subscriptions) :
      length(setsubtract(["topic", "requires_session"], keys(s))) == 0 &&
      length(setsubtract(keys(s), ["topic", "requires_session"])) == 0
    ])
    error_message = "A subscription file sets topic and requires_session; nothing else."
  }
}

resource "azurerm_servicebus_subscription" "this" {
  for_each = var.subscriptions

  name                                 = each.key
  topic_id                             = "${var.namespace_id}/topics/${each.value.topic}"
  max_delivery_count                   = var.max_delivery_count
  dead_lettering_on_message_expiration = true
  requires_session                     = each.value.requires_session
}
