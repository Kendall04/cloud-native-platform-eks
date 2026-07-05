include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

locals {
  env_config   = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  project_name = local.env_config.locals.project_name
  environment  = local.env_config.locals.environment
  region       = local.env_config.locals.region
  account_id   = get_aws_account_id()

  lambda_source_dir = abspath("${get_terragrunt_dir()}/../../../../lambdas/api-gateway-jwt-authorizer")

  auth_service_jwt_secret_id        = "${local.project_name}/${local.environment}/api-gateway-authorizer/auth-service-jwt-secret"
  platform_trusted_proxy_secret_id  = "${local.project_name}/${local.environment}/api-gateway-authorizer/platform-trusted-proxy-secret"
  auth_service_jwt_secret_arn       = "arn:aws:secretsmanager:${local.region}:${local.account_id}:secret:${local.auth_service_jwt_secret_id}-*"
  platform_trusted_proxy_secret_arn = "arn:aws:secretsmanager:${local.region}:${local.account_id}:secret:${local.platform_trusted_proxy_secret_id}-*"
  authorizer_secret_resource_arns = [
    local.auth_service_jwt_secret_arn,
    local.platform_trusted_proxy_secret_arn,
  ]
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
    JWT_ISSUER                       = local.env_config.locals.jwt_issuer
    JWT_AUDIENCE                     = local.env_config.locals.jwt_audience
    AUTH_SERVICE_JWT_SECRET_ID       = local.auth_service_jwt_secret_id
    PLATFORM_TRUSTED_PROXY_SECRET_ID = local.platform_trusted_proxy_secret_id
    SECRET_CACHE_TTL_SECONDS         = "300"
  }

  managed_policy_arns = [
    "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
  ]

  inline_policy_json = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "ReadAuthorizerRuntimeSecrets"
        Effect   = "Allow"
        Action   = "secretsmanager:GetSecretValue"
        Resource = local.authorizer_secret_resource_arns
      }
    ]
  })
}
