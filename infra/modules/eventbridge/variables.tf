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

variable "event_bus_name" {
  description = "Name of the custom EventBridge event bus."
  type        = string
}

variable "rules" {
  description = "Map of EventBridge rules keyed by logical rule name."
  type = map(object({
    description      = optional(string, null)
    event_pattern    = string
    target_queue_arn = string
    target_queue_url = string
    input_path       = optional(string, "$.detail")
  }))
}

variable "tags" {
  description = "Additional tags applied to created resources."
  type        = map(string)
  default     = {}
}
