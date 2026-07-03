locals {
  name        = coalesce(var.name, "${var.project_name}-${var.environment}-db")
  common_tags = merge(var.tags, { Module = "rds" })
}

resource "aws_db_subnet_group" "this" {
  name       = "${local.name}-subnets"
  subnet_ids = var.subnet_ids

  tags = merge(local.common_tags, {
    Name = "${local.name}-subnets"
  })
}

resource "aws_security_group" "this" {
  name        = "${local.name}-sg"
  description = "Security group for ${local.name}."
  vpc_id      = var.vpc_id

  tags = merge(local.common_tags, {
    Name = "${local.name}-sg"
  })
}

resource "aws_vpc_security_group_ingress_rule" "cidr" {
  for_each = toset(var.allowed_cidr_blocks)

  security_group_id = aws_security_group.this.id
  cidr_ipv4         = each.value
  from_port         = var.port
  to_port           = var.port
  ip_protocol       = "tcp"
  description       = "Database access from approved CIDR ranges."
}

resource "aws_vpc_security_group_ingress_rule" "security_group" {
  for_each = toset(var.allowed_security_group_ids)

  security_group_id            = aws_security_group.this.id
  referenced_security_group_id = each.value
  from_port                    = var.port
  to_port                      = var.port
  ip_protocol                  = "tcp"
  description                  = "Database access from approved security groups."
}

resource "aws_vpc_security_group_egress_rule" "all" {
  security_group_id = aws_security_group.this.id
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
  description       = "Allow all outbound traffic."
}

resource "aws_db_instance" "this" {
  identifier                      = local.name
  engine                          = var.engine
  engine_version                  = var.engine_version
  instance_class                  = var.instance_class
  allocated_storage               = var.allocated_storage
  max_allocated_storage           = var.max_allocated_storage
  storage_type                    = var.storage_type
  storage_encrypted               = var.storage_encrypted
  db_name                         = var.database_name
  username                        = var.username
  password                        = var.manage_master_user_password ? null : var.password
  manage_master_user_password     = var.manage_master_user_password
  port                            = var.port
  db_subnet_group_name            = aws_db_subnet_group.this.name
  vpc_security_group_ids          = [aws_security_group.this.id]
  publicly_accessible             = var.publicly_accessible
  multi_az                        = var.multi_az
  backup_retention_period         = var.backup_retention_period
  backup_window                   = var.backup_window
  maintenance_window              = var.maintenance_window
  apply_immediately               = var.apply_immediately
  performance_insights_enabled    = var.performance_insights_enabled
  deletion_protection             = var.deletion_protection
  skip_final_snapshot             = var.skip_final_snapshot
  final_snapshot_identifier       = var.skip_final_snapshot ? null : coalesce(var.final_snapshot_identifier, "${local.name}-final")
  enabled_cloudwatch_logs_exports = var.enabled_cloudwatch_logs_exports
  copy_tags_to_snapshot           = true
  auto_minor_version_upgrade      = true

  tags = merge(local.common_tags, {
    Name = local.name
  })
}

