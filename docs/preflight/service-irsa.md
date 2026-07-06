# Service IRSA Apply Evidence

Date: 2026-07-04T05:14:53Z

Branch: `feat/service-irsa`

AWS account: `145023118802`

Region: `us-east-1`

## Goal

Apply only the IAM roles and inline policies needed by `shipment-service` and `tracking-service` for future Kubernetes IRSA usage.

This phase intentionally did not create Kubernetes namespace, Kubernetes secrets, workloads, API Gateway, Lambda, or API Gateway integration.

## Starting State

EKS and management path:

- EKS cluster `logistics-platform-dev` is healthy.
- 3 nodes are `Ready`.
- `cluster-addons` Helm release is `deployed`.
- AWS Load Balancer Controller pods are running.
- Cluster Autoscaler pods are running.
- Private management EC2 `i-0417796819b2e0f46` can run read-only `kubectl` through SSM.

Runtime blockers before IAM apply:

- Namespace `apps` did not exist.
- Secret `platform-runtime-secrets` did not exist.
- `platform-services` Helm release was not installed.

## `cloud-native-platform-dev-ec2-ssm` Scope Review

Previous IAM readiness showed an extra planned role:

- `cloud-native-platform-dev-ec2-ssm`

This role was defined directly in:

- `infra/live/dev/iam/terragrunt.hcl`

It was not referenced by the active management host, NAT stack, Kubernetes chart, docs as an active dependency, or another current stack output.

Current SSM consumers already have their own dedicated roles:

- Management EC2 uses the `management-host` module and `cloud-native-platform-dev-management-profile`.
- NAT instances use NAT-specific SSM role/profile resources when SSM is enabled.

Decision:

- Remove `cloud-native-platform-dev-ec2-ssm` from the `iam` stack scope before apply.

Reason:

- Fase 2.6 is scoped to service IRSA only.
- Applying an unrelated EC2 SSM role would expand IAM scope without a current consumer.

## IaC Change

Changed:

- `infra/live/dev/iam/terragrunt.hcl`

Change summary:

- Removed the unused `cloud-native-platform-dev-ec2-ssm` role definition from the `roles` input.
- Kept only:
  - `cloud-native-platform-dev-shipment-service-irsa`
  - `cloud-native-platform-dev-tracking-service-irsa`

No module changes were required.

## IAM Plan

Commands:

- `terragrunt init`
- `terragrunt plan -no-color`

Logs:

- `/tmp/cloud-native-platform-service-irsa/iam-init.log`
- `/tmp/cloud-native-platform-service-irsa/iam-plan.log`

Final plan:

- `4 to add`
- `0 to change`
- `0 to destroy`

Resources planned:

- `aws_iam_role.this["cloud-native-platform-dev-shipment-service-irsa"]`
- `aws_iam_role.this["cloud-native-platform-dev-tracking-service-irsa"]`
- `aws_iam_role_policy.this["cloud-native-platform-dev-shipment-service-irsa-shipment-service"]`
- `aws_iam_role_policy.this["cloud-native-platform-dev-tracking-service-irsa-tracking-service"]`

Not planned:

- `cloud-native-platform-dev-ec2-ssm`

No destroy or replacement was planned.

## IAM Apply

Command:

- `terragrunt apply -auto-approve -no-color`

Log:

- `/tmp/cloud-native-platform-service-irsa/iam-apply.log`

Result:

- Apply OK
- `4 added`
- `0 changed`
- `0 destroyed`

Final plan:

- `No changes`

Log:

- `/tmp/cloud-native-platform-service-irsa/iam-final-plan.log`

Outputs:

- `/tmp/cloud-native-platform-service-irsa/iam-outputs.json`

Role outputs:

- `cloud-native-platform-dev-shipment-service-irsa`: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-shipment-service-irsa`
- `cloud-native-platform-dev-tracking-service-irsa`: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-tracking-service-irsa`

## Roles Created

Shipment role:

- Name: `cloud-native-platform-dev-shipment-service-irsa`
- ARN: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-shipment-service-irsa`
- Attached managed policies: none
- Inline policy: `shipment-service`

Tracking role:

- Name: `cloud-native-platform-dev-tracking-service-irsa`
- ARN: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-tracking-service-irsa`
- Attached managed policies: none
- Inline policy: `tracking-service`

The unused `cloud-native-platform-dev-ec2-ssm` role was not created.

## Trust Policies

OIDC provider:

- `arn:aws:iam::145023118802:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/C45613EAD1A4FB94AC9D9AA7D391C4FC`

Shipment trust subject:

- `system:serviceaccount:apps:shipment-service`

Tracking trust subject:

- `system:serviceaccount:apps:tracking-service`

Both trust policies require:

- audience: `sts.amazonaws.com`
- action: `sts:AssumeRoleWithWebIdentity`

## IAM Policies

Shipment inline policy:

- Allows SQS actions on `arn:aws:sqs:us-east-1:145023118802:cloud-native-platform-dev-shipment-events-queue`:
  - `sqs:ReceiveMessage`
  - `sqs:DeleteMessage`
  - `sqs:GetQueueAttributes`
  - `sqs:ChangeMessageVisibility`
- Allows EventBridge:
  - `events:PutEvents`
  - Resource: `arn:aws:events:us-east-1:145023118802:event-bus/cloud-native-platform-dev-bus`

Tracking inline policy:

- Allows EventBridge:
  - `events:PutEvents`
  - Resource: `arn:aws:events:us-east-1:145023118802:event-bus/cloud-native-platform-dev-bus`

No wildcard resource policy was added for the service IRSA roles.

## Helm Values And Render Validation

Values file:

- `k8s/environments/dev/platform-services.values.yaml`

Values already had the correct IRSA role annotations:

- `shipment-service`: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-shipment-service-irsa`
- `tracking-service`: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-tracking-service-irsa`

No values change was required.

Validation:

- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace apps --debug`

Rendered manifest:

- `/tmp/cloud-native-platform-service-irsa/platform-services-irsa-rendered.yaml`

Render result:

- Shipment ServiceAccount has the shipment IRSA role annotation.
- Tracking ServiceAccount has the tracking IRSA role annotation.
- Auth ServiceAccount has no IRSA annotation.
- Images still render by immutable digest.
- Runtime Secret references still point to `platform-runtime-secrets`.
- Namespace references still point to `apps`.

Suspicious scan result:

- No old account `795708473882`.
- No empty image digest.
- No AWS access key pattern.
- The string `password` appears only as a secret key name, not as a secret value.

## Remaining Blockers

Still not created:

- Namespace `apps`
- Secret `platform-runtime-secrets`
- `platform-services` Helm release
- Application workloads
- API Gateway
- Lambda
- API Gateway integration

Read-only SSM validation after IAM apply confirmed:

- Namespace `apps` is still not found.
- Secret lookup fails because namespace `apps` is still not found.
- Only `cluster-addons` is installed by Helm.

## Validation Commands

Additional validation completed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh`
- `helm lint` for `platform-services`
- `helm template` for `platform-services`
- `helm dependency build` for `cluster-addons`
- `helm lint` for `cluster-addons`
- `helm template` for `cluster-addons`

## Out Of Scope Confirmation

Not executed:

- `terragrunt run-all apply`
- apply for any stack except `infra/live/dev/iam`
- Helm install or upgrade
- `kubectl apply`
- `kubectl create`
- `kubectl delete`
- namespace creation
- Kubernetes Secret creation
- workload deployment
- Docker build
- Docker push
- API Gateway apply
- Lambda apply
- Git push

## Recommendation

Proceed to Fase 2.7 only after this branch is integrated.

Recommended Fase 2.7:

- Runtime namespace and secrets bootstrap readiness/apply.
- Decide whether `apps` is pre-created or Helm-created.
- Create `platform-runtime-secrets` through the private management path without committing or printing values.
- Validate secret keys exist without reading values.
- Re-render `platform-services`.
- Do not install workloads until namespace and runtime secrets are validated.
