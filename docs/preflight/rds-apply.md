# RDS Apply Evidence

Initial execution date: 2026-07-03T14:32:10Z
Resume execution date: 2026-07-03T15:50:04Z

## Scope

This evidence covers the initial RDS apply attempt and the resumed RDS apply
after correcting the PostgreSQL engine version. Both executions were scoped only
to the `rds` Terragrunt stack for the AWS `dev` environment.

No EKS, IAM, API Gateway, Lambda, Kubernetes, Helm release, destroy, Git push, or
other stack apply was executed.

## Execution Context

- Branch: `chore/rds-eks-cost-plan-checkpoint`
- AWS account: `1450********8802`
- AWS identity: `arn:aws:iam::145023118802:user/terraform-lab`
- Region: `us-east-1`
- Stack: `infra/live/dev/rds`
- Logs: `/tmp/cloud-native-platform-rds-apply`

## Pre-Apply Validation

Backend and base infrastructure were validated before the RDS apply attempt:

- Terraform state bucket: `cloud-native-platform-145023118802-dev-us-east-1-tfstate`
- DynamoDB lock table: `cloud-native-platform-145023118802-dev-terraform-locks`
- VPC: `vpc-0fe33938202034387`, CIDR `10.0.0.0/16`, `available`

Static validation passed:

```bash
terraform fmt -check -recursive infra
terragrunt hcl format --check infra/live
```

## Initial RDS Plan

Commands:

```bash
cd infra/live/dev/rds
terragrunt init
terragrunt plan -no-color
```

Result:

- Init: OK
- Plan: OK
- Summary: `5 to add, 0 to change, 0 to destroy`
- Destroy/replacement: none

Resources planned:

- `aws_db_instance.this`
- `aws_db_subnet_group.this`
- `aws_security_group.this`
- `aws_vpc_security_group_egress_rule.all`
- `aws_vpc_security_group_ingress_rule.cidr["10.0.0.0/16"]`

Planned DB properties:

- Identifier: `cloud-native-platform-dev-postgres`
- Engine: PostgreSQL
- Requested engine version: `15.7`
- Instance class: `db.t4g.micro`
- Storage: `20 GiB`, `gp3`
- Max allocated storage: `100 GiB`
- Encryption: enabled
- Public access: disabled
- Multi-AZ: disabled
- Backup retention: `7` days
- Deletion protection: enabled
- Master password: RDS-managed, no static password in Terraform

## Initial Apply Result

Command:

```bash
terragrunt apply -auto-approve -no-color
```

Result: failed before DB instance creation.

AWS rejected the requested RDS engine version:

```text
InvalidParameterCombination: Cannot find version 15.7 for postgres
```

Read-only engine version inspection in `us-east-1` showed available PostgreSQL
15 versions beginning at:

```text
15.13, 15.14, 15.15, 15.16, 15.17, 15.18
```

## Partial State After Initial Failure

The apply created and recorded the non-DB prerequisites before failing:

```text
aws_db_subnet_group.this
aws_security_group.this
aws_vpc_security_group_egress_rule.all
aws_vpc_security_group_ingress_rule.cidr["10.0.0.0/16"]
```

No manual Terraform state manipulation was performed.

## Initial RDS Validation

DB instance:

- Final status: not created
- `describe-db-instances`: no matching project DB instance returned

Subnet group:

- Name: `cloud-native-platform-dev-postgres-subnets`
- Status: `Complete`
- VPC: `vpc-0fe33938202034387`
- Subnets:
  - `subnet-0ecdf9e460a352dc6`
  - `subnet-0d50cde4bff9b5154`
  - `subnet-03aa254292f6017e8`

Security group:

- Name: `cloud-native-platform-dev-postgres-sg`
- ID: `sg-004039e0daea1a704`
- Ingress: TCP `5432` from `10.0.0.0/16`

Secrets Manager:

- No matching RDS/postgres/project secret was created because the DB instance
  was not created.
- No secret value was retrieved.

## Engine Version Fix

The RDS apply was resumed after changing:

```text
engine_version = "15.7"
```

to:

```text
engine_version = "15.18"
```

Changed file:

```text
infra/live/dev/rds/terragrunt.hcl
```

No instance class, storage, subnet, security group, deletion protection, public
access, Multi-AZ, or credential handling settings were changed.

## Plan After Engine Fix

Commands:

```bash
cd infra/live/dev/rds
terragrunt init
terragrunt plan -no-color
```

Result:

- Init: OK
- Plan: OK
- Summary: `1 to add, 0 to change, 0 to destroy`
- Destroy/replacement: none

Terraform refreshed and reused the existing partial resources:

- `aws_db_subnet_group.this`
- `aws_security_group.this`
- `aws_vpc_security_group_egress_rule.all`
- `aws_vpc_security_group_ingress_rule.cidr["10.0.0.0/16"]`

Only the missing DB instance was planned for creation.

## Apply After Engine Fix

Command:

```bash
terragrunt apply -auto-approve -no-color
```

Result:

- Apply: OK
- Summary: `1 added, 0 changed, 0 destroyed`
- DB identifier: `cloud-native-platform-dev-postgres`

## Final RDS Validation

DB instance:

- Identifier: `cloud-native-platform-dev-postgres`
- Status: `available`
- Engine: `postgres`
- Engine version: `15.18`
- Instance class: `db.t4g.micro`
- Allocated storage: `20 GiB`
- Max allocated storage: `100 GiB`
- Storage type: `gp3`
- Storage encrypted: `true`
- Publicly accessible: `false`
- Multi-AZ: `false`
- Backup retention: `7` days
- Deletion protection: `true`
- Security group: `sg-004039e0daea1a704`

Subnet group:

- Name: `cloud-native-platform-dev-postgres-subnets`
- Status: `Complete`
- VPC: `vpc-0fe33938202034387`
- Subnets:
  - `subnet-0ecdf9e460a352dc6`
  - `subnet-0d50cde4bff9b5154`
  - `subnet-03aa254292f6017e8`

Secrets Manager:

- RDS-managed master user secret exists.
- Secret metadata was listed.
- Secret value was not retrieved.

## Excluded Stacks

Confirmed not applied:

- `eks`
- `iam`
- `api-gateway-authorizer`
- `notification-lambda`
- `apigateway-core`
- `apigateway-integration`

AWS read-only checks showed:

- EKS clusters: none
- API Gateway APIs matching the project: none
- Lambda functions matching the project: none

## Cost And Risk Notes

New active RDS cost drivers after the successful resumed apply:

- `db.t4g.micro` PostgreSQL instance
- `20 GiB` gp3 storage, autoscaling up to `100 GiB`
- Backup retention for `7` days
- CloudWatch PostgreSQL and upgrade logs as generated
- Possible T-class CPU credit charges under sustained burst load

The existing active base infrastructure remains the main cost driver:

- NAT EC2 instances
- Associated public IPv4 addresses
- ECR/S3/SQS/EventBridge usage as applicable

## Recommendation

RDS is now applied and validated.

Recommended next phase:

1. Pause for cost confirmation before EKS.
2. Re-plan EKS immediately before apply.
3. Apply EKS only if the plan still shows expected cluster, node group, addon,
   OIDC, and IRSA prerequisite resources.
4. Stop if the cluster or node groups do not become healthy.
5. Do not continue to Kubernetes or Helm until EKS is healthy.

## Explicitly Not Executed

- `terraform apply`
- `terragrunt apply` outside `infra/live/dev/rds`
- `terragrunt run-all apply`
- EKS apply
- IAM apply
- API Gateway apply
- Lambda apply
- Kubernetes deploy
- Helm release
- destroy
- Git push
