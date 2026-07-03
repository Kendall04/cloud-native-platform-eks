include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

locals {
  env_config   = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  project_name = local.env_config.locals.project_name
  environment  = local.env_config.locals.environment
  region       = local.env_config.locals.region

  notification_queue_name = "${local.project_name}-${local.environment}-notification-events-queue"
  lambda_source_dir       = abspath("${get_terragrunt_dir()}/../../../../lambdas/notification-lambda")
}

terraform {
  source = "../../../modules/lambda"
}

dependency "sqs" {
  config_path = "../sqs"

  mock_outputs = {
    queue_arns = {
      "${local.notification_queue_name}" = "arn:aws:sqs:us-east-1:123456789012:${local.notification_queue_name}"
    }
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

inputs = {
  function_name = "${local.project_name}-${local.environment}-notification"
  description   = "SES-backed notification worker for Phase A event fan-out."
  source_dir    = local.lambda_source_dir
  handler       = "src/index.handler"
  runtime       = "nodejs22.x"
  timeout       = 30
  memory_size   = 256

  environment_variables = {
    SES_FROM_EMAIL = local.env_config.locals.notification_from_email
    SES_TO_EMAIL   = local.env_config.locals.notification_to_email
  }

  managed_policy_arns = [
    "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole",
    "arn:aws:iam::aws:policy/service-role/AWSLambdaSQSQueueExecutionRole"
  ]

  inline_policy_json = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ses:SendEmail",
          "ses:SendRawEmail"
        ]
        Resource = "*"
      }
    ]
  })

  sqs_event_sources = {
    notification-events = {
      event_source_arn                   = dependency.sqs.outputs.queue_arns[local.notification_queue_name]
      batch_size                         = 10
      maximum_batching_window_in_seconds = 5
    }
  }
}
