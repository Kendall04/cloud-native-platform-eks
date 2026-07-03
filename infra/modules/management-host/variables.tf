variable "project_name" {
  description = "Project name used for tags and naming."
  type        = string
}

variable "environment" {
  description = "Environment name used for tags and naming."
  type        = string
}

variable "region" {
  description = "AWS region."
  type        = string
}

variable "tags" {
  description = "Common resource tags."
  type        = map(string)
  default     = {}
}

variable "name" {
  description = "Name prefix for the management host resources."
  type        = string
}

variable "vpc_id" {
  description = "VPC ID where the management host runs."
  type        = string
}

variable "vpc_cidr_block" {
  description = "VPC CIDR used for DNS egress to the VPC resolver."
  type        = string
}

variable "private_subnet_id" {
  description = "Private subnet ID where the management host runs."
  type        = string
}

variable "cluster_name" {
  description = "EKS cluster name."
  type        = string
}

variable "cluster_security_group_id" {
  description = "EKS cluster security group ID."
  type        = string
}

variable "instance_type" {
  description = "EC2 instance type for the management host."
  type        = string
  default     = "t3.nano"
}

variable "kubectl_version" {
  description = "kubectl version installed by user data."
  type        = string
  default     = "v1.35.0"
}

variable "root_volume_size" {
  description = "Root volume size in GiB."
  type        = number
  default     = 8
}
