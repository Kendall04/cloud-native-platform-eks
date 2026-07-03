include "root" {
  path           = find_in_parent_folders("root.hcl")
  merge_strategy = "deep"
}

dependencies {
  paths = ["../nat"]
}

locals {
  env_config = read_terragrunt_config(find_in_parent_folders("env.hcl"))
}

terraform {
  source = "../../../modules/eks"
}

dependency "vpc" {
  config_path = "../vpc"

  mock_outputs = {
    vpc_id          = "vpc-00000000000000000"
    private_subnets = ["subnet-00000000000000000", "subnet-11111111111111111", "subnet-22222222222222222"]
  }

  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

inputs = {
  cluster_name                                      = local.env_config.locals.eks_cluster_name
  vpc_id                                            = dependency.vpc.outputs.vpc_id
  private_subnet_ids                                = dependency.vpc.outputs.private_subnets
  create_aws_load_balancer_controller_prerequisites = true
  create_cluster_autoscaler_prerequisites           = true
}
