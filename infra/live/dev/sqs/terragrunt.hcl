include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

locals {
  env_config   = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  project_name = local.env_config.locals.project_name
  environment  = local.env_config.locals.environment
}

terraform {
  source = "../../../modules/sqs"
}

inputs = {
  queues = {
    "${local.project_name}-${local.environment}-events" = {
      visibility_timeout_seconds = 60
      message_retention_seconds  = 345600
      receive_wait_time_seconds  = 10
      create_dlq                 = true
      max_receive_count          = 5
    }
    "${local.project_name}-${local.environment}-jobs" = {
      visibility_timeout_seconds = 120
      message_retention_seconds  = 345600
      receive_wait_time_seconds  = 20
      create_dlq                 = true
      max_receive_count          = 3
    }
    "${local.project_name}-${local.environment}-shipment-events-queue" = {
      dlq_name                   = "${local.project_name}-${local.environment}-shipment-events-dlq"
      visibility_timeout_seconds = 60
      message_retention_seconds  = 345600
      receive_wait_time_seconds  = 20
      create_dlq                 = true
      max_receive_count          = 5
    }
    "${local.project_name}-${local.environment}-notification-events-queue" = {
      dlq_name                   = "${local.project_name}-${local.environment}-notification-events-dlq"
      visibility_timeout_seconds = 120
      message_retention_seconds  = 345600
      receive_wait_time_seconds  = 20
      create_dlq                 = true
      max_receive_count          = 5
    }
    "${local.project_name}-${local.environment}-analytics-events-queue" = {
      dlq_name                   = "${local.project_name}-${local.environment}-analytics-events-dlq"
      visibility_timeout_seconds = 120
      message_retention_seconds  = 345600
      receive_wait_time_seconds  = 20
      create_dlq                 = true
      max_receive_count          = 5
    }
  }
}
