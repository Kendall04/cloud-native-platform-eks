include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

locals {
  env_config                         = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  project_name                       = local.env_config.locals.project_name
  environment                        = local.env_config.locals.environment
  shipment_service_account_namespace = "apps"
  shipment_service_account_name      = "shipment-service"
  tracking_service_account_namespace = "apps"
  tracking_service_account_name      = "tracking-service"
  shipment_queue_name                = "${local.project_name}-${local.environment}-shipment-events-queue"
}

terraform {
  source = "../../../modules/iam"
}

dependency "eks" {
  config_path = "../eks"

  mock_outputs = {
    oidc_provider_arn = "arn:aws:iam::123456789012:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
    oidc_provider_url = "https://oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

dependency "sqs" {
  config_path = "../sqs"

  mock_outputs = {
    queue_arns = {
      "${local.shipment_queue_name}" = "arn:aws:sqs:us-east-1:123456789012:${local.shipment_queue_name}"
    }
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

dependency "eventbridge" {
  config_path = "../eventbridge"

  mock_outputs = {
    event_bus_arn = "arn:aws:events:us-east-1:123456789012:event-bus/${local.project_name}-${local.environment}-bus"
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

inputs = {
  roles = {
    "${local.project_name}-${local.environment}-ec2-ssm" = {
      description = "EC2 role with Systems Manager access."
      assume_role_policy = jsonencode({
        Version = "2012-10-17"
        Statement = [
          {
            Effect = "Allow"
            Action = "sts:AssumeRole"
            Principal = {
              Service = "ec2.amazonaws.com"
            }
          }
        ]
      })
      managed_policy_arns = [
        "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
      ]
    }
    "${local.project_name}-${local.environment}-shipment-service-irsa" = {
      description = "IRSA role for shipment-service running on EKS."
      assume_role_policy = jsonencode({
        Version = "2012-10-17"
        Statement = [
          {
            Effect = "Allow"
            Action = "sts:AssumeRoleWithWebIdentity"
            Principal = {
              Federated = dependency.eks.outputs.oidc_provider_arn
            }
            Condition = {
              StringEquals = {
                "${replace(dependency.eks.outputs.oidc_provider_url, "https://", "")}:aud" = "sts.amazonaws.com"
                "${replace(dependency.eks.outputs.oidc_provider_url, "https://", "")}:sub" = "system:serviceaccount:${local.shipment_service_account_namespace}:${local.shipment_service_account_name}"
              }
            }
          }
        ]
      })
      inline_policies = {
        shipment-service = jsonencode({
          Version = "2012-10-17"
          Statement = [
            {
              Effect = "Allow"
              Action = [
                "sqs:ReceiveMessage",
                "sqs:DeleteMessage",
                "sqs:GetQueueAttributes",
                "sqs:ChangeMessageVisibility"
              ]
              Resource = dependency.sqs.outputs.queue_arns[local.shipment_queue_name]
            },
            {
              Effect = "Allow"
              Action = [
                "events:PutEvents"
              ]
              Resource = dependency.eventbridge.outputs.event_bus_arn
            }
          ]
        })
      }
    }
    "${local.project_name}-${local.environment}-tracking-service-irsa" = {
      description = "IRSA role for tracking-service running on EKS."
      assume_role_policy = jsonencode({
        Version = "2012-10-17"
        Statement = [
          {
            Effect = "Allow"
            Action = "sts:AssumeRoleWithWebIdentity"
            Principal = {
              Federated = dependency.eks.outputs.oidc_provider_arn
            }
            Condition = {
              StringEquals = {
                "${replace(dependency.eks.outputs.oidc_provider_url, "https://", "")}:aud" = "sts.amazonaws.com"
                "${replace(dependency.eks.outputs.oidc_provider_url, "https://", "")}:sub" = "system:serviceaccount:${local.tracking_service_account_namespace}:${local.tracking_service_account_name}"
              }
            }
          }
        ]
      })
      inline_policies = {
        tracking-service = jsonencode({
          Version = "2012-10-17"
          Statement = [
            {
              Effect = "Allow"
              Action = [
                "events:PutEvents"
              ]
              Resource = dependency.eventbridge.outputs.event_bus_arn
            }
          ]
        })
      }
    }
  }
}
