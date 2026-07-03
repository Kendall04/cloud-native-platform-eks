variable "project_name" {
  description = "Project name used for tagging."
  type        = string
}

variable "environment" {
  description = "Environment name used for tagging."
  type        = string
}

variable "region" {
  description = "AWS region for the deployment."
  type        = string
}

variable "name_prefix" {
  description = "Prefix used to name NAT resources."
  type        = string
}

variable "nat_mode" {
  description = "Private egress mode. Supported values: gateway or instance."
  type        = string

  validation {
    condition     = contains(["gateway", "instance"], var.nat_mode)
    error_message = "nat_mode must be either gateway or instance."
  }
}

variable "vpc_id" {
  description = "VPC where the NAT resources and routes are managed."
  type        = string
}

variable "availability_zones" {
  description = "Availability zones aligned with the public/private subnet ordering."
  type        = list(string)
}

variable "public_subnet_ids" {
  description = "Public subnet IDs aligned with availability_zones."
  type        = list(string)

  validation {
    condition     = length(var.public_subnet_ids) == length(var.availability_zones)
    error_message = "public_subnet_ids must align with availability_zones."
  }
}

variable "private_subnet_cidrs" {
  description = "Private subnet CIDRs aligned with availability_zones."
  type        = list(string)

  validation {
    condition     = length(var.private_subnet_cidrs) == length(var.availability_zones)
    error_message = "private_subnet_cidrs must align with availability_zones."
  }
}

variable "private_route_table_ids" {
  description = "Private route table IDs aligned with availability_zones."
  type        = list(string)

  validation {
    condition     = length(var.private_route_table_ids) == 1 || length(var.private_route_table_ids) == length(var.availability_zones)
    error_message = "private_route_table_ids must contain either one shared route table or one route table per availability zone."
  }
}

variable "nat_gateway_ids" {
  description = "NAT gateway IDs supplied by the VPC when nat_mode is gateway."
  type        = list(string)
  default     = []

  validation {
    condition     = var.nat_mode != "gateway" || length(var.nat_gateway_ids) == 1 || length(var.nat_gateway_ids) == length(var.private_route_table_ids)
    error_message = "nat_gateway_ids must contain either one shared NAT gateway or one NAT gateway per private route table when nat_mode is gateway."
  }
}

variable "instance_type" {
  description = "Instance type used when nat_mode is instance."
  type        = string
  default     = "t3.nano"
}

variable "key_name" {
  description = "Optional EC2 key pair name for NAT instances."
  type        = string
  default     = null
  nullable    = true
}

variable "allowed_ssh_cidr" {
  description = "CIDR allowed to reach the NAT instance over SSH."
  type        = string
  default     = "127.0.0.1/32"
}

variable "enable_ssm" {
  description = "Whether NAT instances should be reachable through Systems Manager."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Additional tags for NAT resources."
  type        = map(string)
  default     = {}
}
