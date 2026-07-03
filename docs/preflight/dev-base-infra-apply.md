# Dev Base Infrastructure Apply

Execution dates:

- Initial staged apply: 2026-07-03T05:51:59Z
- S3 naming fix and resumed apply: 2026-07-03T06:00:51Z

## Scope

This phase applied only the approved base infrastructure stacks for the AWS `dev` environment.

Applied stacks:

- `vpc`
- `nat`
- `ecr`
- `s3`
- `sqs`
- `eventbridge`

Excluded stacks were not applied:

- `rds`
- `eks`
- `iam`
- `api-gateway-authorizer`
- `notification-lambda`
- `apigateway-core`
- `apigateway-integration`

No Kubernetes, Helm, image build, or deployment command was executed.

## Execution Context

- Branch: `chore/dev-base-infra-apply`
- Base commit: `0308dab chore(infra): prepare dev infrastructure planning (#5)`
- AWS account: `1450********8802`
- AWS identity: `arn:aws:iam::145023118802:user/terraform-lab`
- Region: `us-east-1`
- Logs: `/tmp/cloud-native-platform-apply`

GitHub CLI authentication was logged out before apply. No GitHub push or PR was performed in this phase.

## Backend Validation

Remote state backend was validated before apply:

- S3 bucket: `cloud-native-platform-145023118802-dev-us-east-1-tfstate`
- DynamoDB lock table: `cloud-native-platform-145023118802-dev-terraform-locks`
- Lock table status: `ACTIVE`
- Billing mode: `PAY_PER_REQUEST`

## Static Validation

The following validations passed before apply:

```bash
terraform fmt -check -recursive infra
terragrunt hcl format --check infra/live
bash -n scripts/bootstrap-terraform-backend.sh
shellcheck scripts/bootstrap-terraform-backend.sh
helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm dependency build + helm template for k8s/charts/cluster-addons
```

## S3 Naming Correction

The first `s3` apply attempt failed because these application bucket names were already taken globally:

- `cloud-native-platform-dev-us-east-1-artifacts`
- `cloud-native-platform-dev-us-east-1-logs`

Before changing code:

- `terragrunt state list` for `s3` returned no resources.
- `terragrunt output -json` returned `{}`.
- No manual state edits, imports, or removals were performed.

The application bucket names were updated in `infra/live/dev/s3/terragrunt.hcl` to include the AWS account ID through `get_aws_account_id()`:

- `cloud-native-platform-145023118802-dev-us-east-1-artifacts`
- `cloud-native-platform-145023118802-dev-us-east-1-logs`

The remote state backend bucket name was not changed.

## Apply Results

| Stack | Plan | Apply | Result |
| --- | --- | --- | --- |
| `vpc` | `19 to add` | `19 added` | Success |
| `nat` | `24 to add` | `24 added` | Success |
| `ecr` | `12 to add` | `12 added` | Success |
| `s3` | `10 to add` | `10 added` | Success after account-scoped bucket naming fix |
| `sqs` | `10 to add` | `10 added` | Success |
| `eventbridge` | `10 to add` | `10 added` | Success |

## Resources Created

### VPC

- VPC: `vpc-0fe33938202034387`
- CIDR: `10.0.0.0/16`
- State: `available`
- Public subnets: 3
- Private subnets: 3
- Public route table has default route through internet gateway.
- Private route tables have default routes through NAT instances.

### NAT

NAT uses EC2 instances, not NAT Gateways.

- `i-02ee098265fcb9b88`, `t3.nano`, running
- `i-0995259bf6eee77c8`, `t3.nano`, running
- `i-033abe7faebb2185d`, `t3.nano`, running

Elastic IPs were allocated and associated with the NAT instances:

- `34.227.237.179`
- `54.204.16.56`
- `34.236.66.48`

### ECR

Repositories created with immutable tags and scan-on-push enabled:

- `analytics-worker`
- `auth-service`
- `notification-service`
- `shipment-service`
- `tracking-service`
- `tracking-worker`

### S3

Application buckets created:

- `cloud-native-platform-145023118802-dev-us-east-1-artifacts`
- `cloud-native-platform-145023118802-dev-us-east-1-logs`

Validated configuration:

- Versioning: enabled
- Encryption: SSE-S3/AES256
- Public access block: enabled

### SQS

Queues and DLQs created:

- `cloud-native-platform-dev-analytics-events-queue`
- `cloud-native-platform-dev-analytics-events-dlq`
- `cloud-native-platform-dev-events`
- `cloud-native-platform-dev-events-dlq`
- `cloud-native-platform-dev-jobs`
- `cloud-native-platform-dev-jobs-dlq`
- `cloud-native-platform-dev-notification-events-queue`
- `cloud-native-platform-dev-notification-events-dlq`
- `cloud-native-platform-dev-shipment-events-queue`
- `cloud-native-platform-dev-shipment-events-dlq`

### EventBridge

Event bus created:

- `cloud-native-platform-dev-bus`

Rules created and enabled:

- `cloud-native-platform-dev-bus-trk-upd-analytics`
- `cloud-native-platform-dev-bus-trk-upd-notification`
- `cloud-native-platform-dev-bus-trk-upd-shipment`

Each rule matches:

- `source`: `tracking-service`
- `detail-type`: `TrackingStatusUpdated`

Targets and queue policies were created for the analytics, notification, and shipment SQS queues.

## Explicitly Excluded

The following stacks were explicitly out of scope and were not applied:

- `rds`
- `eks`
- `iam`
- `api-gateway-authorizer`
- `notification-lambda`
- `apigateway-core`
- `apigateway-integration`

Validation after apply confirmed:

- No EKS clusters for this project.
- No RDS instances for this project.
- No API Gateway APIs for this project.
- No Lambda functions for this project.

## Cost and Risk Notes

- The VPC itself has no direct hourly cost.
- Three NAT EC2 instances are running and generating hourly EC2 cost.
- Three Elastic IPs are allocated and associated with NAT instances.
- ECR repositories are low cost until images/storage accumulate.
- S3, SQS, and EventBridge are usually low cost for demo traffic but can grow with storage, requests, messages, events, and retention.
- RDS and EKS were not applied, so the higher-cost continuous services are not running yet.

If work pauses for more than a short validation window, consider a controlled cleanup phase for the applied base infrastructure to avoid ongoing NAT instance cost.

## Local Evidence

Logs and outputs were saved locally:

- `/tmp/cloud-native-platform-apply/vpc-plan.log`
- `/tmp/cloud-native-platform-apply/vpc-apply.log`
- `/tmp/cloud-native-platform-apply/vpc-outputs.json`
- `/tmp/cloud-native-platform-apply/nat-plan.log`
- `/tmp/cloud-native-platform-apply/nat-apply.log`
- `/tmp/cloud-native-platform-apply/nat-outputs.json`
- `/tmp/cloud-native-platform-apply/ecr-plan.log`
- `/tmp/cloud-native-platform-apply/ecr-apply.log`
- `/tmp/cloud-native-platform-apply/ecr-outputs.json`
- `/tmp/cloud-native-platform-apply/s3-replan-before-fix.log`
- `/tmp/cloud-native-platform-apply/s3-plan-after-fix.log`
- `/tmp/cloud-native-platform-apply/s3-apply-after-fix.log`
- `/tmp/cloud-native-platform-apply/s3-outputs.json`
- `/tmp/cloud-native-platform-apply/sqs-plan.log`
- `/tmp/cloud-native-platform-apply/sqs-apply.log`
- `/tmp/cloud-native-platform-apply/sqs-outputs.json`
- `/tmp/cloud-native-platform-apply/eventbridge-plan.log`
- `/tmp/cloud-native-platform-apply/eventbridge-apply.log`
- `/tmp/cloud-native-platform-apply/eventbridge-outputs.json`

## Explicitly Not Executed

- `terraform apply` directly
- `terragrunt run-all apply`
- `terraform state rm`
- `terraform import`
- `kubectl apply`
- `helm upgrade --install`
- any Kubernetes deployment
- any image build
- any Git push
