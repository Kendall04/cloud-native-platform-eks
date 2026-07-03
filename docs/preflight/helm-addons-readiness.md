# Helm Addons Readiness Checkpoint

Date: 2026-07-03T19:43:23Z

Branch: `chore/helm-addons-readiness`

AWS account: `145023118802`

Region: `us-east-1`

## Goal

Validate readiness for installing cluster-wide Helm addons without installing them yet.

This checkpoint focuses on:

- AWS Load Balancer Controller.
- Cluster Autoscaler.
- IRSA roles and trust policies.
- Rendered Helm manifests.
- Private cluster read-only operations through the management EC2 host.

No Helm release was installed in this phase.

## EKS Status

Cluster:

- Name: `logistics-platform-dev`
- Status: `ACTIVE`
- Kubernetes version: `1.35`
- VPC: `vpc-0fe33938202034387`
- Endpoint private access: enabled
- Endpoint public access: disabled
- OIDC issuer: `https://oidc.eks.us-east-1.amazonaws.com/id/C45613EAD1A4FB94AC9D9AA7D391C4FC`

Node groups:

- `api-node-group`
- `worker-node-group`

Managed addons:

- `aws-ebs-csi-driver`
- `coredns`
- `kube-proxy`
- `vpc-cni`

## Management EC2 Status

Management instance:

- Instance ID: `i-01b3101eaca85d043`
- State: `running`
- Instance type: `t3.nano`
- Private IP: `10.0.129.245`
- Public IP: none
- Subnet: `subnet-03aa254292f6017e8`
- Security group: `sg-0980b6aa338f1ef83`
- SSM status: `Online`

## Kubernetes Read-Only Result

Commands were executed from the private management EC2 through SSM Run Command.

Validated:

- `aws sts get-caller-identity`
- `aws eks update-kubeconfig`
- `kubectl get nodes -o wide`
- `kubectl get pods -A`
- `kubectl get namespaces`
- `kubectl get serviceaccounts -A`
- `kubectl get deployments -A`
- `kubectl get events -A --sort-by=.lastTimestamp`

Result:

- 3 nodes are `Ready`.
- Current pods are limited to `kube-system` managed components.
- Namespaces: `default`, `kube-node-lease`, `kube-public`, `kube-system`.
- Deployments: `coredns`, `ebs-csi-controller`.
- No application workloads are deployed.
- Events query returned no relevant warning output in the captured command.

## Chart And Values

Chart path:

- `k8s/charts/cluster-addons`

Values used:

- Base values: `k8s/charts/cluster-addons/values.yaml`
- Dev overlay: `k8s/charts/cluster-addons/values-dev.yaml`
- Generated runtime override: `/tmp/cloud-native-platform-addons-readiness/generated-cluster-addons-values.yaml`

The repository does not currently have `k8s/environments/dev/cluster-addons.values.yaml`. The deploy helper script uses `k8s/charts/cluster-addons/values-dev.yaml` and generates a temporary values file from Terragrunt outputs.

Deploy helper:

- `scripts/deploy-cluster-addons.sh`

Important: the helper performs `helm upgrade --install`, so it was inspected but not executed in this phase.

## Dependencies

Chart dependencies:

- `aws-load-balancer-controller` chart `1.14.0`
- `cluster-autoscaler` chart `9.56.0`

Rendered images:

- AWS Load Balancer Controller: `public.ecr.aws/eks/aws-load-balancer-controller:v2.14.0`
- Cluster Autoscaler: `registry.k8s.io/autoscaling/cluster-autoscaler:v1.35.0`

## Render Validation

Commands:

```bash
tmp="$(mktemp -d)"
cp -R k8s/charts/cluster-addons/. "$tmp/"
helm dependency build "$tmp"
helm lint "$tmp" \
  -f k8s/charts/cluster-addons/values-dev.yaml \
  -f /tmp/cloud-native-platform-addons-readiness/generated-cluster-addons-values.yaml
helm template cluster-addons "$tmp" \
  -f k8s/charts/cluster-addons/values-dev.yaml \
  -f /tmp/cloud-native-platform-addons-readiness/generated-cluster-addons-values.yaml \
  --namespace kube-system \
  --debug \
  > /tmp/cloud-native-platform-cluster-addons-rendered.yaml
```

Result:

- `helm dependency build`: OK.
- `helm lint`: OK.
- `helm template`: OK.
- Rendered manifest length: 866 lines.

No placeholder values were found in the rendered manifest.

## Rendered Resources Summary

Namespace:

- `kube-system`

AWS Load Balancer Controller:

- `ServiceAccount`: `aws-load-balancer-controller`
- `ClusterRole`
- `ClusterRoleBinding`
- leader election `Role`
- leader election `RoleBinding`
- webhook `Secret`
- webhook `Service`
- `Deployment`
- mutating webhook configuration
- validating webhook configuration
- `IngressClassParams`
- `IngressClass` named `alb`

Cluster Autoscaler:

- `PodDisruptionBudget`
- `ServiceAccount`: `cluster-autoscaler`
- `ClusterRole`
- `ClusterRoleBinding`
- `Role`
- `RoleBinding`
- `Service`
- `Deployment`

## Rendered Configuration

AWS Load Balancer Controller:

- Service account annotation:
  - `eks.amazonaws.com/role-arn: arn:aws:iam::145023118802:role/logistics-platform-dev-aws-load-balancer-controller-role`
- `--cluster-name=logistics-platform-dev`
- `--aws-region=us-east-1`
- `--aws-vpc-id=vpc-0fe33938202034387`
- Replicas: `2`

Cluster Autoscaler:

- Service account annotation:
  - `eks.amazonaws.com/role-arn: arn:aws:iam::145023118802:role/logistics-platform-dev-cluster-autoscaler-role`
- Auto-discovery:
  - `asg:tag=k8s.io/cluster-autoscaler/enabled,k8s.io/cluster-autoscaler/logistics-platform-dev`
- `--balance-similar-node-groups=true`
- `--skip-nodes-with-system-pods=false`
- AWS region env: `us-east-1`
- Replicas: `2`

## IRSA Roles

OIDC provider:

- `arn:aws:iam::145023118802:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/C45613EAD1A4FB94AC9D9AA7D391C4FC`

AWS Load Balancer Controller role:

- Role: `logistics-platform-dev-aws-load-balancer-controller-role`
- ARN: `arn:aws:iam::145023118802:role/logistics-platform-dev-aws-load-balancer-controller-role`
- Trust subject: `system:serviceaccount:kube-system:aws-load-balancer-controller`
- Trust audience: `sts.amazonaws.com`
- Attached policy: `logistics-platform-dev-aws-load-balancer-controller`
- Policy includes expected ELBv2, EC2 security group, tagging, listener, target group, WAF/Shield/ACM read or integration permissions for AWS Load Balancer Controller.

Cluster Autoscaler role:

- Role: `logistics-platform-dev-cluster-autoscaler-role`
- ARN: `arn:aws:iam::145023118802:role/logistics-platform-dev-cluster-autoscaler-role`
- Trust subject: `system:serviceaccount:kube-system:cluster-autoscaler`
- Trust audience: `sts.amazonaws.com`
- Attached policy: `logistics-platform-dev-cluster-autoscaler`
- Policy allows `autoscaling:SetDesiredCapacity` and `autoscaling:TerminateInstanceInAutoScalingGroup` only when ASG tags match:
  - `k8s.io/cluster-autoscaler/enabled=true`
  - `k8s.io/cluster-autoscaler/logistics-platform-dev=owned`
- Policy includes expected read permissions for ASG, EC2 instance types/images/launch templates, and EKS nodegroups.

## Discovery Tags

Cluster Autoscaler ASG tags are present on both node groups:

- `k8s.io/cluster-autoscaler/enabled=true`
- `k8s.io/cluster-autoscaler/logistics-platform-dev=owned`
- `kubernetes.io/cluster/logistics-platform-dev=owned`

Subnet tags for AWS Load Balancer Controller discovery are present:

- Public subnets:
  - `kubernetes.io/role/elb=1`
  - `kubernetes.io/cluster/logistics-platform-dev=shared`
- Private subnets:
  - `kubernetes.io/role/internal-elb=1`
  - `kubernetes.io/cluster/logistics-platform-dev=shared`

## Helm Release State

The management EC2 does not currently have Helm installed:

- `helm: command not found`

Because Helm is not installed on the management host, `helm list -A` could not be used from inside the VPC in this phase.

No Helm release was installed by this phase. The live cluster read-only output shows only managed `kube-system` components and no addon controller deployments yet.

## Readiness Assessment

AWS Load Balancer Controller:

- IRSA role exists.
- Trust policy matches `system:serviceaccount:kube-system:aws-load-balancer-controller`.
- ServiceAccount annotation renders with the expected role ARN.
- Cluster name, region, and VPC ID render correctly.
- Public/private subnet discovery tags exist.
- Chart renders successfully.
- No placeholders in rendered manifest.
- Ready for a controlled install phase.

Cluster Autoscaler:

- IRSA role exists.
- Trust policy matches `system:serviceaccount:kube-system:cluster-autoscaler`.
- ServiceAccount annotation renders with the expected role ARN.
- Auto-discovery cluster tag config renders correctly.
- Required ASG tags exist on node group ASGs.
- Chart renders successfully.
- No placeholders in rendered manifest.
- Ready for a controlled install phase.

## Blockers

No blocker was found for installing the cluster addons.

Operational note:

- Helm is not installed on the management EC2. The next install phase can either run Helm from the local environment if it has network access to the private endpoint, or add Helm to the management host through IaC/user data before executing Helm from inside the VPC.

The preferred path is to keep the private endpoint disabled publicly and run Helm through the private management path.

## Decision

Proceed to a controlled install phase for cluster addons.

Recommended next phase:

`Fase 2.1 - Install cluster addons through private management path`

Suggested scope:

- Add Helm tooling to the management EC2 through IaC if Helm will run from inside the VPC.
- Install only `cluster-addons`.
- Validate AWS Load Balancer Controller deployment and webhook readiness.
- Validate Cluster Autoscaler deployment logs and leader election.
- Do not deploy application workloads yet.
- Do not apply API Gateway integration until an internal ALB exists.

## Non-Actions

This phase did not execute:

- `helm upgrade --install`
- `kubectl apply`
- `kubectl delete`
- Terraform/Terragrunt apply
- Workload deploys
- API Gateway apply
- Lambda apply
- `apigateway-integration` apply
- Git push
