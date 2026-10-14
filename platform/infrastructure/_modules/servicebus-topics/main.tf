variable "namespace_id" { type = string }

# Keyed by entity name. Duplicate detection cannot change once a topic exists, so every file states it, and a
# misspelt key fails the plan instead of being dropped by type conversion.
variable "topics" {
  type = any

  validation {
    condition = alltrue([
      for t in values(var.topics) :
      contains(keys(t), "requires_duplicate_detection") &&
      length(setsubtract(keys(t), ["requires_duplicate_detection", "accepts_test_senders", "accepts_onprem_senders"])) == 0
    ])
    error_message = "A topic file sets requires_duplicate_detection and may set accepts_test_senders and accepts_onprem_senders; nothing else."
  }
}

# The principals an acceptance suite publishes as, granted send on each topic that accepts test senders. The
# hosts' own rights cover the whole namespace.
variable "test_sender_principal_ids" { type = list(string) }

# The on-premises workers that publish this domain's events from beside V1, granted send on each topic that
# accepts them.
variable "onprem_sender_principal_ids" {
  type    = list(string)
  default = []
}

locals {
  topics = {
    for name, t in var.topics : name => {
      requires_duplicate_detection = t.requires_duplicate_detection
      accepts_test_senders         = try(t.accepts_test_senders, false)
      accepts_onprem_senders       = try(t.accepts_onprem_senders, false)
    }
  }

  test_senders = {
    for pair in setproduct([for name, t in local.topics : name if t.accepts_test_senders], var.test_sender_principal_ids) :
    "${pair[0]}/${pair[1]}" => { topic = pair[0], principal_id = pair[1] }
  }

  onprem_senders = {
    for pair in setproduct([for name, t in local.topics : name if t.accepts_onprem_senders], var.onprem_sender_principal_ids) :
    "${pair[0]}/${pair[1]}" => { topic = pair[0], principal_id = pair[1] }
  }
}

resource "azurerm_servicebus_topic" "this" {
  for_each = local.topics

  name                         = each.key
  namespace_id                 = var.namespace_id
  requires_duplicate_detection = each.value.requires_duplicate_detection
}

resource "azurerm_role_assignment" "test_sender" {
  for_each = local.test_senders

  scope                = azurerm_servicebus_topic.this[each.value.topic].id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = each.value.principal_id
}

resource "azurerm_role_assignment" "onprem_sender" {
  for_each = local.onprem_senders

  scope                = azurerm_servicebus_topic.this[each.value.topic].id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = each.value.principal_id
}
