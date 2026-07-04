include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

dependencies {
  paths = ["../eks", "../s3", "../vpc"]
}

locals {
  env_config   = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  project_name = local.env_config.locals.project_name
  environment  = local.env_config.locals.environment
}

terraform {
  source = "../../../modules/management-host"
}

dependency "vpc" {
  config_path = "../vpc"

  mock_outputs = {
    vpc_id             = "vpc-00000000000000000"
    vpc_cidr_block     = "10.20.0.0/16"
    private_subnet_ids = ["subnet-00000000000000000", "subnet-11111111111111111"]
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

dependency "eks" {
  config_path = "../eks"

  mock_outputs = {
    cluster_name           = local.env_config.locals.eks_cluster_name
    cluster_security_group = "sg-00000000000000000"
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

dependency "s3" {
  config_path = "../s3"

  mock_outputs = {
    bucket_ids = {
      artifacts = "cloud-native-platform-dev-us-east-1-artifacts"
      logs      = "cloud-native-platform-dev-us-east-1-logs"
    }
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

inputs = {
  name                      = "${local.project_name}-${local.environment}-management"
  vpc_id                    = dependency.vpc.outputs.vpc_id
  vpc_cidr_block            = dependency.vpc.outputs.vpc_cidr_block
  private_subnet_id         = dependency.vpc.outputs.private_subnet_ids[0]
  cluster_name              = dependency.eks.outputs.cluster_name
  cluster_security_group_id = dependency.eks.outputs.cluster_security_group
  internal_alb_http_egress_security_group_ids = [
    "sg-079c5a1e99c17270a",
    "sg-0dcd35733de8447ba",
  ]
  instance_type        = "t3.nano"
  kubectl_version      = "v1.35.0"
  helm_version         = "v4.2.2"
  root_volume_size     = 8
  artifact_bucket_name = dependency.s3.outputs.bucket_ids.artifacts
  artifact_prefix      = "cluster-addons/${local.environment}"
}
