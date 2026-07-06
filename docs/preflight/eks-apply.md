# EKS Apply Evidence

Execution date: 2026-07-03T16:23:26Z

## Scope

This phase applied only the `eks` Terragrunt stack for the AWS `dev`
environment.

No Helm release, Kubernetes apply, application workload deploy, API Gateway,
Lambda, standalone IAM stack, `apigateway-integration`, destroy, or Git push was
executed.

## Execution Context

- Branch: `chore/eks-apply`
- AWS account: `1450********8802`
- AWS identity: `arn:aws:iam::145023118802:user/terraform-lab`
- Region: `us-east-1`
- Stack: `infra/live/dev/eks`
- Logs: `/tmp/cloud-native-platform-eks-apply`

## Base Infrastructure Validated

Validated before EKS apply:

- Terraform state bucket: `cloud-native-platform-145023118802-dev-us-east-1-tfstate`
- DynamoDB lock table: `cloud-native-platform-145023118802-dev-terraform-locks`, `ACTIVE`
- VPC: `vpc-0fe33938202034387`, CIDR `10.0.0.0/16`, `available`
- NAT: 3 running `t3.nano` instances
- RDS: `cloud-native-platform-dev-postgres`, `available`, PostgreSQL `15.18`,
  private, encrypted, deletion protection enabled

Static validation passed:

```bash
terraform fmt -check -recursive infra
terragrunt hcl format --check infra/live
bash -n scripts/bootstrap-terraform-backend.sh
shellcheck scripts/bootstrap-terraform-backend.sh
helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm dependency build + helm template for k8s/charts/cluster-addons
```

## EKS Configuration Detected

Detected from `infra/live/dev/eks/terragrunt.hcl` and `infra/modules/eks`:

- Cluster name: `logistics-platform-dev`
- Kubernetes version: `1.35`
- Endpoint private access: enabled
- Endpoint public access: disabled
- Private subnets:
  - `subnet-0d50cde4bff9b5154`
  - `subnet-0ecdf9e460a352dc6`
  - `subnet-03aa254292f6017e8`
- Control plane logs enabled:
  - `api`
  - `audit`
  - `authenticator`
  - `controllerManager`
  - `scheduler`
- Control plane log retention: `30` days
- OIDC provider: enabled
- IRSA roles:
  - VPC CNI
  - EBS CSI driver
- AWS Load Balancer Controller IAM/IRSA prerequisites: enabled
- Cluster Autoscaler IAM/IRSA prerequisites: enabled

Managed node groups:

- `api-node-group`
  - Capacity type: `ON_DEMAND`
  - Instance type: `t3.large`
  - Desired/min/max: `2/2/6`
  - Disk size: `50 GiB`
  - AMI type: `AL2023_x86_64_STANDARD`
- `worker-node-group`
  - Capacity type: `SPOT`
  - Instance type: `t3.large`
  - Desired/min/max: `1/1/10`
  - Disk size: `50 GiB`
  - AMI type: `AL2023_x86_64_STANDARD`

Managed addons:

- `vpc-cni`
- `kube-proxy`
- `coredns`
- `aws-ebs-csi-driver`

## Plan Result

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
- Destroy/replacement: none

Main resources planned:

- EKS cluster
- EKS CloudWatch log group
- 2 managed node groups
- 4 managed addons
- OIDC provider
- Cluster IAM role
- Node group IAM roles
- VPC CNI and EBS CSI IRSA roles
- AWS Load Balancer Controller IAM policy/role prerequisites
- Cluster Autoscaler IAM policy/role prerequisites

## Apply Result

Command:

```bash
terragrunt apply -auto-approve -no-color
```

Result:

- Apply: OK
- Summary: `27 added, 0 changed, 0 destroyed`
- Cluster name: `logistics-platform-dev`
- Cluster security group: `sg-0216c41ff3acc9eb2`
- OIDC provider ARN:
  `arn:aws:iam::145023118802:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/C45613EAD1A4FB94AC9D9AA7D391C4FC`

## Cluster Validation

AWS CLI validation:

- Name: `logistics-platform-dev`
- Status: `ACTIVE`
- Version: `1.35`
- Endpoint private access: `true`
- Endpoint public access: `false`
- VPC: `vpc-0fe33938202034387`
- Cluster security group: `sg-0216c41ff3acc9eb2`
- OIDC issuer:
  `https://oidc.eks.us-east-1.amazonaws.com/id/C45613EAD1A4FB94AC9D9AA7D391C4FC`
- Control plane logs: enabled for `api`, `audit`, `authenticator`,
  `controllerManager`, and `scheduler`

## Node Group Validation

`api-node-group`:

- Status: `ACTIVE`
- Capacity type: `ON_DEMAND`
- Instance type: `t3.large`
- Desired/min/max: `2/2/6`
- Disk size: `50 GiB`
- AMI type: `AL2023_x86_64_STANDARD`
- Health issues: none

`worker-node-group`:

- Status: `ACTIVE`
- Capacity type: `SPOT`
- Instance type: `t3.large`
- Desired/min/max: `1/1/10`
- Disk size: `50 GiB`
- AMI type: `AL2023_x86_64_STANDARD`
- Health issues: none

## Addon Validation

All managed addons were validated through AWS CLI:

- `vpc-cni`: `ACTIVE`, `v1.22.2-eksbuild.1`, no health issues
- `kube-proxy`: `ACTIVE`, `v1.35.3-eksbuild.13`, no health issues
- `coredns`: `ACTIVE`, `v1.14.3-eksbuild.3`, no health issues
- `aws-ebs-csi-driver`: `ACTIVE`, `v1.62.0-eksbuild.1`, no health issues

## OIDC And IRSA Prerequisites

OIDC provider exists:

```text
arn:aws:iam::145023118802:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/C45613EAD1A4FB94AC9D9AA7D391C4FC
```

Roles validated:

- `logistics-platform-dev-cluster-role`
- `logistics-platform-dev-api-node-group-role`
- `logistics-platform-dev-worker-node-group-role`
- `logistics-platform-dev-vpc-cni-irsa-role`
- `logistics-platform-dev-aws-ebs-csi-driver-irsa-role`
- `logistics-platform-dev-aws-load-balancer-controller-role`
- `logistics-platform-dev-cluster-autoscaler-role`

## Kubectl Read-Only Validation

`aws eks update-kubeconfig` completed and wrote the cluster context locally.

Read-only `kubectl get nodes` and `kubectl get pods -A` could not connect from
this workstation because the cluster endpoint is private-only and this network
path cannot reach it:

```text
Unable to connect to the server: net/http: request canceled while waiting for connection
```

This is consistent with the intended private endpoint design. AWS CLI validation
confirmed that the cluster, node groups, addons, and OIDC provider are healthy.

## Excluded Stacks And Actions

Confirmed not applied:

- `iam`
- `api-gateway-authorizer`
- `notification-lambda`
- `apigateway-core`
- `apigateway-integration`

AWS read-only checks showed:

- API Gateway APIs matching the project: none
- Lambda functions matching the project: none

Explicitly not executed:

- `kubectl apply`
- `helm upgrade --install`
- application workload deploys
- `terragrunt run-all apply`
- destroy
- Git push

## Cost And Risk Notes

New active EKS cost drivers:

- EKS control plane
- 2 on-demand `t3.large` nodes in `api-node-group`
- 1 spot `t3.large` node in `worker-node-group`
- EBS volumes for managed node groups
- EKS control plane CloudWatch logs

Existing active cost drivers remain:

- NAT EC2 instances
- Public IPv4 addresses
- RDS PostgreSQL `db.t4g.micro`
- RDS gp3 storage/backups/logs
- ECR/S3/SQS/EventBridge usage

Operational notes:

- Because the endpoint is private-only, future Kubernetes/Helm work must run from
  a network path that can reach the private EKS endpoint, or the access model
  must be adjusted deliberately.
- The Spot worker node group can be interrupted.
- Do not proceed to Helm releases until Kubernetes API access is intentionally
  available from the operator environment.

## Recommendation

Recommended next phase:

1. Resolve the private endpoint operator access path for `kubectl`/Helm.
2. Revalidate read-only Kubernetes access.
3. Install/validate EKS addons through Helm only after access is confirmed.
4. Do not deploy workloads until cluster-addons are installed and healthy.
