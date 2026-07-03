include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

locals {
  env_config   = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  project_name = local.env_config.locals.project_name
  environment  = local.env_config.locals.environment
  region       = local.env_config.locals.region

  lambda_source_dir = abspath("${get_terragrunt_dir()}/../../../../lambdas/api-gateway-jwt-authorizer")
}

terraform {
  source = "../../../modules/lambda"
}

inputs = {
  function_name = "${local.project_name}-${local.environment}-api-jwt-authorizer"
  description   = "JWT request authorizer for the shared HTTP API."
  source_dir    = local.lambda_source_dir
  handler       = "src/index.handler"
  runtime       = "nodejs22.x"
  timeout       = 10
  memory_size   = 128

  environment_variables = {
    JWT_ISSUER                    = local.env_config.locals.jwt_issuer
    JWT_AUDIENCE                  = local.env_config.locals.jwt_audience
    JWT_SECRET                    = get_env("AUTH_SERVICE_JWT_SECRET")
    PLATFORM_TRUSTED_PROXY_SECRET = get_env("PLATFORM_TRUSTED_PROXY_SECRET")
  }

  managed_policy_arns = [
    "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
  ]
}
