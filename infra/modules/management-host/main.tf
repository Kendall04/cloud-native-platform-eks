locals {
  name            = var.name
  artifact_prefix = trim(var.artifact_prefix, "/")

  common_tags = merge(var.tags, {
    Name   = local.name
    Module = "management-host"
    Role   = "management"
  })
}

data "aws_ami" "al2023" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["al2023-ami-2023.*-x86_64"]
  }

  filter {
    name   = "architecture"
    values = ["x86_64"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

data "aws_caller_identity" "current" {}

resource "aws_security_group" "management" {
  name        = "${local.name}-sg"
  description = "Private SSM management host with EKS API egress."
  vpc_id      = var.vpc_id

  tags = merge(local.common_tags, {
    Name = "${local.name}-sg"
  })
}

resource "aws_vpc_security_group_egress_rule" "https" {
  security_group_id = aws_security_group.management.id
  description       = "HTTPS egress for SSM, AWS APIs, package downloads, and EKS API."
  ip_protocol       = "tcp"
  from_port         = 443
  to_port           = 443
  cidr_ipv4         = "0.0.0.0/0"
}

resource "aws_vpc_security_group_egress_rule" "dns_tcp" {
  security_group_id = aws_security_group.management.id
  description       = "TCP DNS egress to VPC resolver."
  ip_protocol       = "tcp"
  from_port         = 53
  to_port           = 53
  cidr_ipv4         = var.vpc_cidr_block
}

resource "aws_vpc_security_group_egress_rule" "dns_udp" {
  security_group_id = aws_security_group.management.id
  description       = "UDP DNS egress to VPC resolver."
  ip_protocol       = "udp"
  from_port         = 53
  to_port           = 53
  cidr_ipv4         = var.vpc_cidr_block
}

resource "aws_vpc_security_group_egress_rule" "internal_alb_http" {
  for_each = var.internal_alb_http_egress_security_group_ids

  security_group_id            = aws_security_group.management.id
  referenced_security_group_id = each.value
  description                  = "HTTP egress to internal ALB for private platform smoke tests."
  ip_protocol                  = "tcp"
  from_port                    = 80
  to_port                      = 80
}

resource "aws_vpc_security_group_ingress_rule" "eks_api_from_management" {
  security_group_id            = var.cluster_security_group_id
  referenced_security_group_id = aws_security_group.management.id
  description                  = "Allow private management host to reach the EKS API server."
  ip_protocol                  = "tcp"
  from_port                    = 443
  to_port                      = 443
}

resource "aws_iam_role" "management" {
  name = "${local.name}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })

  tags = merge(local.common_tags, {
    Name = "${local.name}-role"
  })
}

resource "aws_iam_role_policy_attachment" "ssm" {
  role       = aws_iam_role.management.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_role_policy" "eks_read_only" {
  name = "${local.name}-eks-read-only"
  role = aws_iam_role.management.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "DescribeTargetCluster"
        Effect = "Allow"
        Action = [
          "eks:DescribeCluster"
        ]
        Resource = "arn:aws:eks:${var.region}:${data.aws_caller_identity.current.account_id}:cluster/${var.cluster_name}"
      }
    ]
  })
}

resource "aws_iam_role_policy" "artifact_read_only" {
  count = var.artifact_bucket_name == null ? 0 : 1

  name = "${local.name}-artifact-read-only"
  role = aws_iam_role.management.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ListArtifactPrefix"
        Effect = "Allow"
        Action = [
          "s3:ListBucket"
        ]
        Resource = "arn:aws:s3:::${var.artifact_bucket_name}"
        Condition = {
          StringLike = {
            "s3:prefix" = [
              local.artifact_prefix,
              "${local.artifact_prefix}/*"
            ]
          }
        }
      },
      {
        Sid    = "ReadArtifactObjects"
        Effect = "Allow"
        Action = [
          "s3:GetObject"
        ]
        Resource = "arn:aws:s3:::${var.artifact_bucket_name}/${local.artifact_prefix}/*"
      }
    ]
  })
}

resource "aws_iam_role_policy" "temporary_secret_write" {
  count = length(var.temporary_secret_write_arns) == 0 ? 0 : 1

  name = "${local.name}-temporary-secret-write"
  role = aws_iam_role.management.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "TemporaryAuthorizerSecretSync"
        Effect = "Allow"
        Action = [
          "secretsmanager:DescribeSecret",
          "secretsmanager:PutSecretValue"
        ]
        Resource = sort(tolist(var.temporary_secret_write_arns))
      }
    ]
  })
}

resource "aws_iam_instance_profile" "management" {
  name = "${local.name}-profile"
  role = aws_iam_role.management.name

  tags = merge(local.common_tags, {
    Name = "${local.name}-profile"
  })
}

resource "aws_eks_access_entry" "management" {
  cluster_name  = var.cluster_name
  principal_arn = aws_iam_role.management.arn
  type          = "STANDARD"

  tags = merge(local.common_tags, {
    Name = "${local.name}-eks-access"
  })
}

resource "aws_eks_access_policy_association" "view" {
  cluster_name  = var.cluster_name
  principal_arn = aws_iam_role.management.arn
  policy_arn    = "arn:aws:eks::aws:cluster-access-policy/AmazonEKSAdminViewPolicy"

  access_scope {
    type = "cluster"
  }

  depends_on = [aws_eks_access_entry.management]
}

resource "aws_eks_access_policy_association" "apps_edit" {
  count = var.enable_apps_namespace_edit_access ? 1 : 0

  cluster_name  = var.cluster_name
  principal_arn = aws_iam_role.management.arn
  policy_arn    = "arn:aws:eks::aws:cluster-access-policy/AmazonEKSEditPolicy"

  access_scope {
    type       = "namespace"
    namespaces = ["apps"]
  }

  depends_on = [aws_eks_access_entry.management]
}

resource "aws_instance" "management" {
  ami                         = data.aws_ami.al2023.id
  instance_type               = var.instance_type
  subnet_id                   = var.private_subnet_id
  associate_public_ip_address = false
  iam_instance_profile        = aws_iam_instance_profile.management.name
  vpc_security_group_ids      = [aws_security_group.management.id]
  user_data_replace_on_change = true

  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 1
  }

  root_block_device {
    encrypted   = true
    volume_size = var.root_volume_size
    volume_type = "gp3"
  }

  user_data = templatefile("${path.module}/user_data.sh.tftpl", {
    kubectl_version = var.kubectl_version
    helm_version    = var.helm_version
  })

  tags = local.common_tags

  depends_on = [
    aws_iam_role_policy_attachment.ssm,
    aws_iam_role_policy.eks_read_only,
    aws_iam_role_policy.artifact_read_only
  ]
}
