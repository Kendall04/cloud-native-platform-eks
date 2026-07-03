variable "name_prefix" { type = string }
variable "vpc_id" { type = string }
variable "public_subnet_id" { type = string }
variable "availability_zone" { type = string }
variable "instance_type" { type = string }
variable "key_name" {
  type     = string
  default  = null
  nullable = true
}
variable "allowed_ssh_cidr" { type = string }
variable "private_subnet_cidr" {
  type     = string
  default  = null
  nullable = true
}
variable "private_subnet_cidrs" {
  type    = list(string)
  default = []
}
variable "enable_ssm" { type = bool }
variable "tags" { type = map(string) }
