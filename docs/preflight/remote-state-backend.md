# Remote State Backend Preflight

Execution date: 2026-07-03T05:05:00Z

## Scope

This document records the non-destructive preflight for the Terraform/Terragrunt remote state backend used by the `dev` environment.

No S3 bucket, DynamoDB table, Terraform state, Terragrunt plan, or AWS runtime resource was created or modified during this phase.

## Current Backend Configuration

The backend is defined in `infra/live/root.hcl` through Terragrunt `remote_state`.

Current generated backend:

- Backend: `s3`
- Bucket expression: `${local.project_name}-${local.aws_account_id}-${local.environment}-${local.region}-tfstate`
- Resolved bucket for dev: `cloud-native-platform-145023118802-dev-us-east-1-tfstate`
- State key: `${path_relative_to_include()}/terraform.tfstate`
- Region: `us-east-1`
- Lock table expression: `${local.project_name}-${local.aws_account_id}-${local.environment}-terraform-locks`
- Resolved lock table for dev: `cloud-native-platform-145023118802-dev-terraform-locks`
- Encryption: enabled
- Locking: DynamoDB table
- Bucket versioning/public access/TLS safeguards: configured through Terragrunt remote state options

The state key is stack-relative. For example, the `vpc` stack would use a key equivalent to:

```text
dev/vpc/terraform.tfstate
```

## AWS Read-Only Checks

Identity used:

- Account: `1450********8802`
- Principal: `arn:aws:iam::145023118802:user/terraform-lab`
- Region: `us-east-1`

Read checks:

- `aws s3api head-bucket --bucket cloud-native-platform-dev-us-east-1-terraform-state`
  - Result: `403 Forbidden`
- `aws dynamodb describe-table --table-name cloud-native-platform-dev-terraform-locks --region us-east-1`
  - Result: `ResourceNotFoundException`
- `aws s3api list-buckets`
  - Result: no buckets visible in this account

## Interpretation

The S3 `403 Forbidden` likely means the bucket name exists globally but is not accessible to this AWS account, or a bucket policy denies this identity. Since `list-buckets` returned no buckets in the account, the safest assumption is that the current bucket name is already owned elsewhere or otherwise unusable.

The DynamoDB lock table does not exist in this account and region.

## Permissions Observed

The IAM user has `AdministratorAccess` attached. IAM policy simulation for bootstrap-relevant actions returned `allowed` for:

- `s3:CreateBucket`
- `s3:PutBucketVersioning`
- `s3:PutEncryptionConfiguration`
- `s3:PutPublicAccessBlock`
- `s3:GetBucketLocation`
- `dynamodb:CreateTable`
- `dynamodb:DescribeTable`
- `dynamodb:UpdateTimeToLive`

This is sufficient for a controlled backend bootstrap phase, assuming account-level service control policies or external constraints do not override these permissions.

## Bootstrap Support In Repo

Current status: no dedicated backend bootstrap script or stack exists.

Found:

- Generic S3 Terraform module under `infra/modules/s3`
- Dev application S3 stack under `infra/live/dev/s3`
- Terragrunt remote state configuration in `infra/live/root.hcl`

Not found:

- Dedicated remote-state bootstrap script
- Dedicated local-state Terraform bootstrap stack
- Existing backend bucket/table creation workflow

## Backend Naming Decision

The previous bucket name was not kept:

```text
cloud-native-platform-dev-us-east-1-terraform-state
```

Reason: S3 bucket names are globally unique, and the current name returned `403 Forbidden`.

Final bucket/table naming:

```text
cloud-native-platform-145023118802-dev-us-east-1-tfstate
cloud-native-platform-145023118802-dev-terraform-locks
```

Rationale:

- Includes AWS account id to reduce global S3 collision risk.
- Keeps project, environment, and region readable for portfolio review.
- Keeps table naming aligned with the bucket.
- Avoids using random suffixes while remaining reproducible.

Implementation:

- `infra/live/root.hcl` now derives `local.aws_account_id` with Terragrunt `get_aws_account_id()`.
- `local.state_bucket` resolves to `${local.project_name}-${local.aws_account_id}-${local.environment}-${local.region}-tfstate`.
- `local.state_lock_table` resolves to `${local.project_name}-${local.aws_account_id}-${local.environment}-terraform-locks`.
- State key structure still uses `path_relative_to_include()`.

This requires valid AWS credentials when Terragrunt evaluates the backend configuration.

## Recommended Bootstrap Strategy

Implemented option: AWS CLI idempotent bootstrap script.

```text
scripts/bootstrap-terraform-backend.sh
```

Behavior:

- Accepts `PROJECT_NAME`, `ENVIRONMENT`, `AWS_ACCOUNT_ID`, and `AWS_REGION`.
- Defaults to `PROJECT_NAME=cloud-native-platform`, `ENVIRONMENT=dev`, and `AWS_REGION=us-east-1`.
- Derives `AWS_ACCOUNT_ID` from `aws sts get-caller-identity` when omitted.
- Resolve bucket/table names deterministically.
- Fail fast if the bucket exists but is not owned or accessible by this account.
- Create the S3 bucket if missing.
- Enable S3 versioning.
- Enable SSE-S3 encryption by default.
- Enable S3 public access block.
- Enable bucket ownership controls with `BucketOwnerEnforced`.
- Create DynamoDB lock table with `PAY_PER_REQUEST`.
- Wait until the DynamoDB table exists.
- Print a concise summary.
- Never write AWS credentials or secrets to disk.

Future Fase 1.3 execution command:

```bash
AWS_REGION=us-east-1 \
PROJECT_NAME=cloud-native-platform \
ENVIRONMENT=dev \
AWS_ACCOUNT_ID=145023118802 \
scripts/bootstrap-terraform-backend.sh
```

Do not run this command until backend bootstrap execution is explicitly approved.

## Post-Bootstrap Validation

After running the script in a future phase, validate with:

```bash
aws s3api head-bucket \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws s3api get-bucket-versioning \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws s3api get-bucket-encryption \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws s3api get-public-access-block \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws dynamodb describe-table \
  --table-name cloud-native-platform-145023118802-dev-terraform-locks \
  --region us-east-1
```

## Bootstrap Execution Evidence

Execution date: 2026-07-03T05:20:00Z

The backend bootstrap script was executed for the `dev` environment only:

```bash
AWS_REGION=us-east-1 \
ENVIRONMENT=dev \
PROJECT_NAME=cloud-native-platform \
./scripts/bootstrap-terraform-backend.sh
```

AWS identity:

- Account: `1450********8802`
- Principal: `arn:aws:iam::145023118802:user/terraform-lab`
- Region: `us-east-1`

Resources created or confirmed:

- S3 bucket: `cloud-native-platform-145023118802-dev-us-east-1-tfstate`
- DynamoDB lock table: `cloud-native-platform-145023118802-dev-terraform-locks`

Validation results:

- `head-bucket`: accessible in `us-east-1`
- S3 versioning: `Enabled`
- S3 encryption: `AES256`
- S3 public access block:
  - `BlockPublicAcls=true`
  - `IgnorePublicAcls=true`
  - `BlockPublicPolicy=true`
  - `RestrictPublicBuckets=true`
- S3 ownership controls: `BucketOwnerEnforced`
- DynamoDB table status: `ACTIVE`
- DynamoDB billing mode: `PAY_PER_REQUEST`
- DynamoDB key schema: `LockID` string hash key

Commands used for validation:

```bash
aws s3api head-bucket \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws s3api get-bucket-versioning \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws s3api get-bucket-encryption \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws s3api get-public-access-block \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws s3api get-bucket-ownership-controls \
  --bucket cloud-native-platform-145023118802-dev-us-east-1-tfstate

aws dynamodb describe-table \
  --table-name cloud-native-platform-145023118802-dev-terraform-locks \
  --region us-east-1
```

No Terraform/Terragrunt init, plan, apply, Kubernetes command, Helm deployment, or application deployment was executed during this bootstrap.

## Risks

- Running Terragrunt before backend bootstrap may attempt remote state initialization.
- Keeping the current bucket name risks permanent collision with a bucket outside this account.
- A state backend bucket must not be deleted casually; losing state can orphan cloud resources.
- `AdministratorAccess` is acceptable for a lab bootstrap but should be replaced with narrower permissions for a mature workflow.
- The access key used for this preflight was shared in chat and should be rotated.
- Terragrunt `get_aws_account_id()` makes backend naming portable but requires AWS identity resolution before init/plan.
- The backend resources now exist and may incur minimal S3/DynamoDB charges.

## Explicitly Not Executed

- No `terraform init`
- No `terraform plan`
- No `terraform apply`
- No `terragrunt plan`
- No `terragrunt apply`
- No S3 bucket creation
- No DynamoDB table creation
- No AWS resource modification
- No deploy
- No push
