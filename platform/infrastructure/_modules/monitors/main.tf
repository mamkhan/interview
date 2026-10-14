variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" { type = map(string) }
variable "display_prefix" { type = string }
variable "service_bus_namespace_id" { type = string }
variable "action_group_id" { type = string }
variable "app_insights_id" { type = string }
variable "log_analytics_workspace_id" { type = string }

# Keyed by instance, each carrying the alert name and workbook seed the unit composed. The workbook's resource name
# is a hash of its seed, so changing the seed replaces the workbook. A misspelt key fails the plan instead of being
# dropped by type conversion.
variable "monitors" {
  type = any

  validation {
    condition = alltrue([
      for o in values(var.monitors) :
      length(setsubtract(["alert_name", "workbook_seed", "service", "topics"], keys(o))) == 0 &&
      length(setsubtract(keys(o), ["alert_name", "workbook_seed", "service", "workbook", "topics"])) == 0
    ])
    error_message = "A monitor file sets service, workbook and topics; nothing else."
  }
}

# Dead-letter arrivals are the operational work list: anything landing there must be seen, so the alert
# fires on any depth above zero.
resource "azurerm_monitor_metric_alert" "dead_letter" {
  for_each = var.monitors

  name                = each.value.alert_name
  resource_group_name = var.resource_group_name
  description         = "Dead-lettered messages present on ${join(", ", each.value.topics)}; the dead-letter queue is the operational work list and must be drained."
  scopes              = [var.service_bus_namespace_id]
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT5M"
  auto_mitigate       = true
  tags                = merge(var.tags, { service = each.value.service })

  criteria {
    metric_namespace = "Microsoft.ServiceBus/namespaces"
    metric_name      = "DeadletteredMessages"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 0

    dimension {
      name     = "EntityName"
      operator = "Include"
      values   = each.value.topics
    }
  }

  action {
    action_group_id = var.action_group_id
  }
}

resource "azurerm_application_insights_workbook" "operations" {
  for_each = var.monitors

  name                = uuidv5("oid", "workbook-${each.value.workbook_seed}")
  location            = var.location
  resource_group_name = var.resource_group_name
  display_name        = "${var.display_prefix}: health and dead-letter"
  tags                = merge(var.tags, { service = each.value.service })

  data_json = jsonencode({
    version = "Notebook/1.0"
    items = [
      {
        type = 1
        name = "header"
        content = {
          json = "## ${var.display_prefix}\nHost health (requests and failures from Application Insights) and dead-letter depth on ${join(", ", each.value.topics)} (Service Bus metrics routed to Log Analytics)."
        }
      },
      {
        type = 3
        name = "host-health"
        content = {
          version                 = "KqlItem/1.0"
          query                   = "requests | summarize Requests = count(), Failed = countif(success == false) by bin(timestamp, 5m), cloud_RoleName | order by timestamp asc"
          size                    = 0
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          timeContext             = { durationMs = 86400000 }
        }
      },
      {
        type = 3
        name = "dead-letter-depth"
        content = {
          version                 = "KqlItem/1.0"
          query                   = "AzureMetrics | where MetricName == \"DeadletteredMessages\" | summarize DeadLettered = max(Maximum) by bin(TimeGenerated, 5m) | order by TimeGenerated asc"
          size                    = 0
          queryType               = 0
          resourceType            = "microsoft.operationalinsights/workspaces"
          crossComponentResources = [var.log_analytics_workspace_id]
          visualization           = "timechart"
          timeContext             = { durationMs = 86400000 }
        }
      }
    ]
    "$schema" = "https://github.com/Microsoft/Application-Insights-Workbooks/blob/master/schema/workbook.json"
  })
}

output "alert_ids" { value = { for k, a in azurerm_monitor_metric_alert.dead_letter : k => a.id } }
output "workbook_ids" { value = { for k, w in azurerm_application_insights_workbook.operations : k => w.id } }
