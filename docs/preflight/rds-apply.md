# RDS Apply Evidence

Execution date: 2026-07-03T14:32:10Z

## Scope

This phase attempted to apply only the `rds` Terragrunt stack for the AWS `dev`
environment.

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

## RDS Plan

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

## Apply Result

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

## Partial State

The apply created and recorded the non-DB prerequisites before failing:

```text
aws_db_subnet_group.this
aws_security_group.this
aws_vpc_security_group_egress_rule.all
aws_vpc_security_group_ingress_rule.cidr["10.0.0.0/16"]
```

No manual Terraform state manipulation was performed.

## RDS Validation

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

New cost from this failed attempt should be limited to low-cost supporting
resources:

- RDS DB subnet group
- RDS security group and security group rules

The RDS database instance itself was not created, so there is no RDS compute or
storage charge from the DB instance yet.

The existing active base infrastructure remains the main cost driver:

- NAT EC2 instances
- Associated public IPv4 addresses
- ECR/S3/SQS/EventBridge usage as applicable

## Recommendation

Do not proceed to EKS.

Recommended next phase:

1. Fix the RDS engine version to a currently available PostgreSQL 15 version in
   `us-east-1`, for example a current supported `15.x` value.
2. Re-run `terragrunt plan` for `infra/live/dev/rds`.
3. Confirm Terraform plans only the missing DB instance and does not replace or
   destroy the subnet group/security group unexpectedly.
4. Re-run `terragrunt apply` for `rds` only.
5. Validate DB status, encryption, private access, deletion protection, and
   RDS-managed secret.

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
