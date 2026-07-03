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
    api_id        = "a1b2c3d4"
    vpc_link_id   = "vpclink-1234567890"
    authorizer_id = "authorizer-1234567890"
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

inputs = {
  enabled           = local.enable_apigateway_integration
  api_id            = dependency.apigateway_core.outputs.api_id
  vpc_link_id       = dependency.apigateway_core.outputs.vpc_link_id
  authorizer_id     = dependency.apigateway_core.outputs.authorizer_id
  alb_listener_port = 80

  alb_discovery_tags = {
    "kubernetes.io/namespace"    = "apps"
    "kubernetes.io/ingress-name" = "platform-services"
  }

  public_route_keys = [
    "ANY /auth",
    "ANY /auth/{proxy+}",
    "GET /shipments/swagger",
    "GET /shipments/swagger/{proxy+}",
    "GET /tracking/swagger",
    "GET /tracking/swagger/{proxy+}"
  ]

  protected_route_keys = [
    "GET /auth/me",
    "GET /auth/validate",
    "ANY /shipments",
    "ANY /shipments/{proxy+}",
    "ANY /tracking",
    "ANY /tracking/{proxy+}",
    "ANY /admin/users",
    "ANY /admin/users/{proxy+}",
    "ANY /admin/shipments",
    "ANY /admin/shipments/{proxy+}",
    "ANY /admin/tracking-events",
    "ANY /admin/tracking-events/{proxy+}"
  ]
}
