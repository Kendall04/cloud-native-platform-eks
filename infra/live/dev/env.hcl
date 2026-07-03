locals {
  project_name                  = "cloud-native-platform"
  environment                   = "dev"
  region                        = "us-east-1"
  eks_cluster_name              = "logistics-platform-dev"
  nat_mode                      = "instance"
  nat_instance_type             = "t3.nano"
  nat_instance_key_name         = null
  nat_instance_allowed_ssh_cidr = "127.0.0.1/32"
  nat_instance_enable_ssm       = true
  enable_apigateway_integration = true
  internal_alb_name             = "cloud-native-platform-dev"
  jwt_issuer                    = "logistics-platform"
  jwt_audience                  = "logistics-platform-clients"
  platform_database_name        = "platform"
  notification_from_email       = "notifications@logistics.local"
  notification_to_email         = "ops@logistics.local"

  tags = {
    Repository = "cloud-native-platform-eks"
    ManagedBy  = "Terraform"
    Owner      = "platform-team"
  }
}
