include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

locals {
  env_config                     = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  project_name                   = local.env_config.locals.project_name
  environment                    = local.env_config.locals.environment
  shipment_events_queue_name     = "${local.project_name}-${local.environment}-shipment-events-queue"
  notification_events_queue_name = "${local.project_name}-${local.environment}-notification-events-queue"
  analytics_events_queue_name    = "${local.project_name}-${local.environment}-analytics-events-queue"
}

terraform {
  source = "../../../modules/eventbridge"
}

dependency "sqs" {
  config_path = "../sqs"

  mock_outputs = {
    queue_arns = {
      "${local.shipment_events_queue_name}"     = "arn:aws:sqs:us-east-1:123456789012:${local.shipment_events_queue_name}"
      "${local.notification_events_queue_name}" = "arn:aws:sqs:us-east-1:123456789012:${local.notification_events_queue_name}"
      "${local.analytics_events_queue_name}"    = "arn:aws:sqs:us-east-1:123456789012:${local.analytics_events_queue_name}"
    }
    queue_urls = {
      "${local.shipment_events_queue_name}"     = "https://sqs.us-east-1.amazonaws.com/123456789012/${local.shipment_events_queue_name}"
      "${local.notification_events_queue_name}" = "https://sqs.us-east-1.amazonaws.com/123456789012/${local.notification_events_queue_name}"
      "${local.analytics_events_queue_name}"    = "https://sqs.us-east-1.amazonaws.com/123456789012/${local.analytics_events_queue_name}"
    }
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

inputs = {
  event_bus_name = "${local.project_name}-${local.environment}-bus"

  rules = {
    trk-upd-shipment = {
      description      = "Routes TrackingStatusUpdated events to shipment-service."
      event_pattern    = jsonencode({ source = ["tracking-service"], "detail-type" = ["TrackingStatusUpdated"] })
      target_queue_arn = dependency.sqs.outputs.queue_arns[local.shipment_events_queue_name]
      target_queue_url = dependency.sqs.outputs.queue_urls[local.shipment_events_queue_name]
      input_path       = "$.detail"
    }
    trk-upd-notification = {
      description      = "Routes TrackingStatusUpdated events to the notification Lambda queue."
      event_pattern    = jsonencode({ source = ["tracking-service"], "detail-type" = ["TrackingStatusUpdated"] })
      target_queue_arn = dependency.sqs.outputs.queue_arns[local.notification_events_queue_name]
      target_queue_url = dependency.sqs.outputs.queue_urls[local.notification_events_queue_name]
      input_path       = "$.detail"
    }
    trk-upd-analytics = {
      description      = "Routes TrackingStatusUpdated events to the analytics queue."
      event_pattern    = jsonencode({ source = ["tracking-service"], "detail-type" = ["TrackingStatusUpdated"] })
      target_queue_arn = dependency.sqs.outputs.queue_arns[local.analytics_events_queue_name]
      target_queue_url = dependency.sqs.outputs.queue_urls[local.analytics_events_queue_name]
      input_path       = "$.detail"
    }
  }
}
