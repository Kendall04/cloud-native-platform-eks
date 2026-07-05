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

variable "enabled" {
  description = "Whether to enable ALB discovery, integrations, and route creation."
  type        = bool
  default     = true
}

variable "api_id" {
  description = "ID of the HTTP API created by apigateway-core."
  type        = string
}

variable "vpc_link_id" {
  description = "ID of the API Gateway VPC link created by apigateway-core."
  type        = string
}

variable "authorizer_id" {
  description = "ID of the API Gateway request authorizer created by apigateway-core."
  type        = string
}

variable "alb_discovery_tags" {
  description = "Tags used to discover the ALB created by the Kubernetes ingress."
  type        = map(string)
  default     = {}
}

variable "alb_listener_arn" {
  description = "Optional explicit ALB listener ARN. When set, discovery by tags or name is skipped."
  type        = string
  default     = null
  nullable    = true
}

variable "alb_arn" {
  description = "Optional explicit ALB ARN used when listener discovery by port is still desired."
  type        = string
  default     = null
  nullable    = true
}

variable "alb_name" {
  description = "Optional fallback ALB name when tag-based discovery is not used."
  type        = string
  default     = null
  nullable    = true

  validation {
    condition     = !var.enabled || var.alb_listener_arn != null || var.alb_arn != null || length(var.alb_discovery_tags) > 0 || var.alb_name != null
    error_message = "Set alb_listener_arn, alb_arn, alb_discovery_tags for tag-based lookup, or provide alb_name as a fallback."
  }
}

variable "alb_listener_port" {
  description = "Listener port on the ALB used by API Gateway."
  type        = number
  default     = 80
}

variable "public_route_keys" {
  description = "Route keys that are exposed without an authorizer."
  type        = list(string)
  default     = []
}

variable "protected_route_keys" {
  description = "Route keys that require the API Gateway Lambda authorizer."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Additional tags applied to created resources."
  type        = map(string)
  default     = {}
}
