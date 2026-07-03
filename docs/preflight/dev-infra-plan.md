# Dev Infrastructure Plan Preflight

Execution date: 2026-07-03T05:32:34Z

## Scope

This preflight validates whether the `dev` AWS environment can initialize and plan safely before any application infrastructure is applied.

No Terraform/Terragrunt apply, Kubernetes apply, Helm install, image build, or deployment command was executed.

## Tool Versions

- Terraform: v1.15.7
- Terragrunt: v1.0.8
- AWS CLI: 2.35.12
- Helm: v4.2.2
- kubectl client: v1.36.2

## AWS Identity

- Account: `1450********8802`
- Identity: `arn:aws:iam::145023118802:user/terraform-lab`
- Region: `us-east-1`
- Credential source: local AWS CLI profile/config for this preflight

No access keys or secret values were written to repository files.

## Remote State Status

Terragrunt remote state is configured in `infra/live/root.hcl`:

- Backend: S3
- Bucket: `cloud-native-platform-145023118802-dev-us-east-1-tfstate`
- Key pattern: `<stack>/terraform.tfstate`
- Region: `us-east-1`
- Lock table: `cloud-native-platform-145023118802-dev-terraform-locks`
- Encryption: enabled

Preflight checks passed:

- S3 bucket `head-bucket`: OK
- DynamoDB lock table: `ACTIVE`
- DynamoDB billing mode: `PAY_PER_REQUEST`
- DynamoDB key schema: `LockID` string hash key

## Required Environment Variables

Only the API Gateway authorizer stack currently requires local environment variables through `get_env`:

- `AUTH_SERVICE_JWT_SECRET`
- `PLATFORM_TRUSTED_PROXY_SECRET`

For plan-only validation, non-secret placeholder values were exported in the shell process. Real runtime secrets must not be committed.

## Static Validation Results

The following validations passed:

```bash
terraform fmt -check -recursive infra
terragrunt hcl format --check infra/live
helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm dependency build + helm template for k8s/charts/cluster-addons
```

## Detected Dev Stacks

Terragrunt stacks under `infra/live/dev`:

- `vpc`
- `nat`
- `ecr`
- `s3`
- `sqs`
- `eventbridge`
- `rds`
- `eks`
- `iam`
- `api-gateway-authorizer`
- `notification-lambda`
- `apigateway-core`
- `apigateway-integration`

## Init/Plan Results

Logs were written under `/tmp/cloud-native-platform-init-<stack>.log` and `/tmp/cloud-native-platform-plan-<stack>.log`.

| Stack | Init | Plan | Summary |
| --- | --- | --- | --- |
| `vpc` | OK | OK | Plans VPC, public/private subnets, route tables, associations, and internet gateway. `19 to add`. |
| `nat` | OK | OK | Plans NAT instances, EIPs, route updates, security groups, and SSM instance roles. `24 to add`. |
| `ecr` | OK | OK | Plans ECR repositories and lifecycle policies for platform images. `12 to add`. |
| `s3` | OK | OK | Plans application artifact/log buckets with ownership, encryption, public access block, and versioning. `10 to add`. |
| `sqs` | OK | OK | Plans application queues and DLQs. `10 to add`. |
| `eventbridge` | OK | OK | Plans event bus, rules, SQS targets, and queue policies. `10 to add`. |
| `rds` | OK | OK | Plans PostgreSQL instance, subnet group, and security group rules. `5 to add`. |
| `eks` | OK | OK | Plans EKS cluster, managed node groups, addons, OIDC provider, CloudWatch log group, and IAM roles/policies. `27 to add`. |
| `iam` | OK | OK | Plans shared IAM/IRSA roles and inline policies for workloads. `6 to add`. |
| `api-gateway-authorizer` | OK | OK | Plans Lambda authorizer, log group, IAM role, and basic execution policy attachment. `4 to add`. |
| `notification-lambda` | OK | OK | Plans notification Lambda, log group, IAM role/policies, and SQS event source mapping. `7 to add`. |
| `apigateway-core` | OK | OK | Plans HTTP API, Lambda authorizer binding, stage, VPC Link, security group, and logs. `8 to add`. |
| `apigateway-integration` | OK | Blocked | Fails because the internal ALB lookup returns zero results. This ALB is expected to be created later by Kubernetes Ingress through AWS Load Balancer Controller. |

## Blockers

1. `apigateway-integration` cannot plan until the internal ALB exists.
   - Error source: `data.aws_lb.ingress_by_tags[0]`.
   - Summary: AWS load balancer tag lookup returned zero results.
   - Expected resolution: create EKS, install/configure AWS Load Balancer Controller, deploy the Helm ingress, then re-run this stack plan.

2. Runtime secrets are still required before real apply/deploy.
   - `AUTH_SERVICE_JWT_SECRET`
   - `PLATFORM_TRUSTED_PROXY_SECRET`

3. A Terraform backend warning was observed for the DynamoDB lock table argument.
   - Current backend works for this preflight.
   - Future cleanup can evaluate the newer `use_lockfile` backend behavior separately.

## Recommended Future Apply Order

After review, apply in controlled phases rather than `run-all apply`:

1. `vpc`
2. `nat`
3. `ecr`
4. `s3`
5. `sqs`
6. `eventbridge`
7. `rds`
8. `eks`
9. `iam`
10. `api-gateway-authorizer`
11. `notification-lambda`
12. `apigateway-core`

Hold `apigateway-integration` until the AWS Load Balancer Controller has created the internal ALB from the Kubernetes ingress.

## Cost Risks

- EKS control plane has continuous hourly cost.
- Managed node groups create EC2 capacity and attached storage cost.
- NAT mode is configured as instances, reducing cost versus NAT Gateway but adding operational responsibility.
- RDS PostgreSQL has continuous instance, storage, backup, and snapshot cost; deletion protection and final snapshot behavior should be reviewed before cleanup.
- ALB cost begins once the ingress/controller creates a load balancer.
- API Gateway and Lambda are usage-based but can still generate cost through tests.
- CloudWatch logs and EKS control plane logs can accumulate cost.
- SQS/EventBridge costs are usually low for demo traffic but scale with request volume.

## Next Steps

- Review plan logs in `/tmp` before any apply.
- Run a cost sanity check for EKS, node groups, RDS, ALB, and NAT instances.
- Execute future applies stack by stack, stopping before `apigateway-integration`.
- Defer `apigateway-integration` until after Kubernetes ingress creates the internal ALB.

## Explicitly Not Executed

- `terraform apply`
- `terragrunt apply`
- `terragrunt run-all apply`
- `kubectl apply`
- `helm upgrade --install`
- Any Kubernetes deployment
- Any Git push
