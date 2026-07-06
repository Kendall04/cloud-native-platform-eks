# RDS/EKS Cost Plan Checkpoint

Execution date: 2026-07-03T06:13:41Z

## Scope

This checkpoint refreshes plans and cost/risk assessment for the next higher-cost `dev` infrastructure stacks:

- `rds`
- `eks`

No apply, destroy, Kubernetes deploy, Helm release, image build, or Git push was executed.

## Execution Context

- Branch: `chore/rds-eks-cost-plan-checkpoint`
- Base branch: `develop`
- AWS account: `1450********8802`
- AWS identity: `arn:aws:iam::145023118802:user/terraform-lab`
- Region: `us-east-1`
- Logs: `/tmp/cloud-native-platform-rds-eks-checkpoint`

## Base Infrastructure Status

The already-applied base infrastructure was validated before planning:

- VPC: `vpc-0fe33938202034387`, CIDR `10.0.0.0/16`, `available`
- NAT: 3 running `t3.nano` instances with associated public IPv4 addresses
- ECR: service repositories exist with immutable tags
- S3: app buckets and tfstate bucket exist
- SQS: 10 queues/DLQs exist
- EventBridge: `cloud-native-platform-dev-bus` exists

Remote state backend was validated:

- S3 bucket: `cloud-native-platform-145023118802-dev-us-east-1-tfstate`
- DynamoDB lock table: `cloud-native-platform-145023118802-dev-terraform-locks`
- Lock table status: `ACTIVE`

## Static Validation

The following validations passed:

```bash
terraform fmt -check -recursive infra
terragrunt hcl format --check infra/live
bash -n scripts/bootstrap-terraform-backend.sh
shellcheck scripts/bootstrap-terraform-backend.sh
helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm dependency build + helm template for k8s/charts/cluster-addons
```

## RDS Configuration

Detected from `infra/live/dev/rds/terragrunt.hcl` and `infra/modules/rds`:

- Engine: PostgreSQL
- Engine version: `15.7`
- Instance class: `db.t4g.micro`
- Initial storage: `20 GiB`
- Max autoscaled storage: `100 GiB`
- Storage type: `gp3`
- Storage encryption: enabled
- Database name: `platform`
- Master username: `platform_admin`
- Master password: managed by RDS/Secrets Manager
- Public access: disabled
- Multi-AZ: disabled
- Backup retention: `7` days
- CloudWatch log exports: `postgresql`, `upgrade`
- Performance Insights: enabled
- Deletion protection: enabled
- Final snapshot on delete: required
- Subnets: private VPC subnets
- Security group ingress: PostgreSQL `5432` from VPC CIDR `10.0.0.0/16`

Risk notes:

- Low instance size, but continuous RDS hourly cost starts after apply.
- RDS-managed master password is good; no static DB password is required in Terraform.
- Deletion protection and final snapshot are safer, but they make cleanup intentionally slower.
- Ingress from whole VPC CIDR is broad for production; acceptable for this staged dev checkpoint, but later can be narrowed to workload security groups after EKS/IRSA is live.

## EKS Configuration

Detected from `infra/live/dev/eks/terragrunt.hcl` and `infra/modules/eks`:

- Cluster name: `logistics-platform-dev`
- Kubernetes version: `1.35`
- Endpoint private access: enabled
- Endpoint public access: disabled
- Control plane logs enabled: `api`, `audit`, `authenticator`, `controllerManager`, `scheduler`
- Control plane log retention: `30` days
- Authentication mode: `API_AND_CONFIG_MAP`
- Bootstrap cluster creator admin permissions: enabled
- OIDC provider: planned
- IRSA addon roles: planned for `vpc-cni` and `aws-ebs-csi-driver`
- AWS Load Balancer Controller IAM/IRSA prerequisites: planned
- Cluster Autoscaler IAM/IRSA prerequisites: planned

Managed node groups:

- `api-node-group`
  - Instance type: `t3.large`
  - Capacity type: `ON_DEMAND`
  - Desired/min/max: `2/2/6`
  - Disk size: `50 GiB`
- `worker-node-group`
  - Instance type: `t3.large`
  - Capacity type: `SPOT`
  - Desired/min/max: `1/1/10`
  - Disk size: `50 GiB`

Managed addons:

- `vpc-cni`: `v1.22.2-eksbuild.1`
- `kube-proxy`: `v1.35.3-eksbuild.13`
- `coredns`: `v1.14.3-eksbuild.3`
- `aws-ebs-csi-driver`: `v1.62.0-eksbuild.1`

Risk notes:

- EKS control plane creates fixed hourly cost.
- Node groups add EC2 and EBS cost immediately after they become active.
- Private-only endpoint is stronger, but day-2 access depends on network path or AWS tooling access from inside the VPC.
- `api-node-group` has two on-demand `t3.large` nodes as the minimum; this is the main cost jump.
- Worker group uses Spot, so it is cheaper but can be interrupted.

## Plan Results

### RDS

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

Resources planned:

- `aws_db_instance.this`
- `aws_db_subnet_group.this`
- `aws_security_group.this`
- `aws_vpc_security_group_egress_rule.all`
- `aws_vpc_security_group_ingress_rule.cidr["10.0.0.0/16"]`

Warnings:

- Terraform backend warning: `dynamodb_table` is deprecated in favor of newer backend locking behavior.

No destroy or replacement actions were planned.

### EKS

Commands:

```bash
cd infra/live/dev/eks
terragrunt init
terragrunt plan -no-color
```

Result:

- Init: OK
- Plan: OK
- Summary: `27 to add, 0 to change, 0 to destroy`

Resources planned:

- EKS cluster
- EKS CloudWatch log group
- OIDC provider
- 2 managed node groups
- 4 managed EKS addons
- Cluster IAM role and node IAM roles
- IRSA roles/policy attachments for VPC CNI and EBS CSI
- AWS Load Balancer Controller IAM policy/role prerequisites
- Cluster Autoscaler IAM policy/role prerequisites

Warnings:

- Terraform backend warning: `dynamodb_table` is deprecated in favor of newer backend locking behavior.

No destroy or replacement actions were planned.

## Other Plans

No additional plans were run for:

- `iam`
- `api-gateway-authorizer`
- `notification-lambda`
- `apigateway-core`
- `apigateway-integration`

Reason: this checkpoint is intentionally scoped to RDS/EKS cost approval. `apigateway-integration` remains expected to block until the internal ALB exists after Kubernetes ingress is deployed.

## Cost Checkpoint

Current active cost drivers:

- 3 NAT EC2 `t3.nano` instances
- 3 associated public IPv4 addresses
- ECR repositories as images accumulate
- S3 storage/requests
- SQS/EventBridge requests and retention

Incremental RDS cost after apply:

- Continuous `db.t4g.micro` PostgreSQL compute
- `20 GiB` gp3 storage, autoscaling up to `100 GiB`
- Backup storage beyond free allowance, if applicable
- Performance Insights and exported CloudWatch logs
- Possible burst CPU credit charges for T-class RDS under sustained load

Incremental EKS cost after apply:

- EKS control plane hourly charge
- 2 on-demand `t3.large` API nodes
- 1 Spot `t3.large` worker node, with interruption risk
- 3 node EBS volumes at `50 GiB` each
- EKS control plane CloudWatch logs

Costs not yet introduced in this phase:

- ALB hourly/LCU charges
- API Gateway request usage
- Lambda usage
- Container image storage growth beyond empty repositories
- Observability stack costs

Pricing note:

- AWS lists EKS standard Kubernetes version support at `$0.10` per cluster-hour and extended support at `$0.60` per cluster-hour.
- AWS public IPv4 pricing is `$0.005` per IP-hour.
- AWS EBS gp3 examples use `$0.08` per GB-month in regions with that rate.
- Exact EC2/RDS rates should be verified in AWS Pricing Calculator before apply.

## Recommended Apply Strategy

Recommended Fase 1.10 order:

1. Apply `rds` first.
2. Validate RDS.
3. Stop for checkpoint.
4. Apply `eks` only if cost approval is explicit.
5. Validate EKS health before any Kubernetes/Helm phase.

RDS validation:

- DB instance status is `available`.
- DB subnet group uses private subnets.
- DB is not publicly accessible.
- Security group allows expected private access only.
- RDS-managed master secret ARN exists.
- Outputs are captured.

EKS validation:

- Cluster status is `ACTIVE`.
- Node groups are `ACTIVE`.
- Nodes join the cluster.
- Managed addons are healthy.
- Endpoint access is private-only as expected.
- OIDC provider exists.
- IRSA prerequisite roles exist.

Stop limits:

- Stop if RDS does not become `available`.
- Stop if EKS cluster does not become `ACTIVE`.
- Stop if either node group does not become `ACTIVE`.
- Stop if a refreshed plan shows destroys/replacements or unexpected resource classes.
- Stop if the approved budget does not include EKS control plane plus three `t3.large` nodes.
- Do not continue to Helm/Kubernetes until EKS is healthy.
- Do not apply `apigateway-integration` until the internal ALB exists.

## Recommendation

Proceed to Fase 1.10 only with explicit approval for continuous RDS and EKS costs.

Preferred path:

1. Apply and validate `rds`.
2. Pause.
3. Apply and validate `eks` in the same phase only if the cost checkpoint is accepted.

If work will pause for more than a short window, prefer a cleanup/pause phase before creating EKS.

## Explicitly Not Executed

- `terraform apply`
- `terragrunt apply`
- `terragrunt run-all apply`
- `kubectl apply`
- `helm upgrade --install`
- any Kubernetes deployment
- any destroy
- any Git push
