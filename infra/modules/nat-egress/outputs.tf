output "nat_instance_ids" {
  description = "NAT instance IDs keyed by availability zone when nat_mode is instance."
  value       = { for az, instance in module.instance : az => instance.instance_id }
}

output "nat_instance_public_ips" {
  description = "NAT instance public IPs keyed by availability zone when nat_mode is instance."
  value       = { for az, instance in module.instance : az => instance.public_ip }
}

output "default_route_table_ids" {
  description = "Private route tables managed by this module."
  value       = values(aws_route.default)[*].route_table_id
}
