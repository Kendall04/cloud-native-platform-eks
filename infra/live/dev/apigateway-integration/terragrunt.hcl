include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

locals {
  env_config                    = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  project_name                  = local.env_config.locals.project_name
  environment                   = local.env_config.locals.environment
  enable_apigateway_integration = try(local.env_config.locals.enable_apigateway_integration, false)
}

terraform {
  source = "../../../modules/apigateway-integration"
}

dependency "apigateway_core" {
  config_path = "../apigateway-core"

  mock_outputs = {
    api_id                     = "a1b2c3d4"
    vpc_link_id                = "vpclink-1234567890"
    vpc_link_security_group_id = "sg-00000000000000000"
    authorizer_id              = "authorizer-1234567890"
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

inputs = {
  enabled           = local.enable_apigateway_integration
  api_id            = dependency.apigateway_core.outputs.api_id
  vpc_link_id       = dependency.apigateway_core.outputs.vpc_link_id
  authorizer_id     = dependency.apigateway_core.outputs.authorizer_id
  alb_listener_port = 80
  alb_arn           = "arn:aws:elasticloadbalancing:us-east-1:145023118802:loadbalancer/app/cloud-native-platform-dev/feabb93df9e991e0"
  alb_listener_arn  = "arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9"

  alb_discovery_tags = {
    "ingress.k8s.aws/stack" = "cloud-native-platform"
    "elbv2.k8s.aws/cluster" = "logistics-platform-dev"
  }

  public_route_keys = [
    "POST /auth/login",
    "POST /auth/register",
    "POST /auth/refresh"
  ]

  protected_route_keys = [
    "GET /auth/me",
    "GET /auth/validate",
    "ANY /shipments",
    "ANY /shipments/{proxy+}",
    "ANY /tracking/{proxy+}"
  ]
}
