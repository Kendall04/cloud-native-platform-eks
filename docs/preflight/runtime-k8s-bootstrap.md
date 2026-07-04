# Runtime Kubernetes Bootstrap

Date: 2026-07-04

Branch: `chore/runtime-k8s-bootstrap-access`

AWS account: `145023118802`

## Summary

Fase 2.7c created the runtime Kubernetes prerequisites required before installing `platform-services`:

- Namespace `apps`
- Secret `platform-runtime-secrets`

The bootstrap used controlled temporary access:

- EKS access was elevated from `AmazonEKSAdminViewPolicy` to `AmazonEKSClusterAdminPolicy`.
- AWS secret-source access was granted only while creating the runtime Secret.
- Both temporary permissions were removed after the namespace and Secret were created.

No Helm release was installed, and no app workload was deployed.

## Starting State

- EKS cluster `logistics-platform-dev` was healthy.
- `cluster-addons` was deployed.
- Management EC2 was available through SSM.
- `platform-services` was not installed.
- Namespace `apps` did not exist.
- Secret `platform-runtime-secrets` did not exist.
- API Gateway and Lambda were not applied.

Management EC2:

- Instance ID: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- Public IP: none
- IAM role: `cloud-native-platform-dev-management-role`

Pre-check command:

- SSM command ID: `89023e31-7b8a-4f42-b598-78b0b3f86d8b`

## Temporary Permissions

### Kubernetes Access

Temporary policy:

- `AmazonEKSClusterAdminPolicy`

Final policy after reduction:

- `AmazonEKSAdminViewPolicy`

### AWS Secret-Source Access

Temporary inline policy:

- `cloud-native-platform-dev-management-runtime-secret-bootstrap`

Temporary actions:

- `rds:DescribeDBInstances`
- `secretsmanager:DescribeSecret`
- `secretsmanager:GetSecretValue`

Scope:

- RDS describe was used only for the dev RDS instance metadata lookup.
- Secrets Manager access was scoped only to the RDS managed secret ARN for `cloud-native-platform-dev-postgres`.

No wildcard Secrets Manager access was granted.

## Management Elevation

Applied only stack:

- `infra/live/dev/management`

Plan result:

- `2 to add`
- `0 to change`
- `1 to destroy`

Controlled changes:

- Replaced management EKS access policy association:
  - from `AmazonEKSAdminViewPolicy`
  - to `AmazonEKSClusterAdminPolicy`
- Added temporary inline policy:
  - `aws_iam_role_policy.runtime_secret_bootstrap[0]`

Apply result:

- `2 added`
- `0 changed`
- `1 destroyed`

Final plan after elevation:

- `No changes`

Evidence:

- `/tmp/cloud-native-platform-runtime-k8s-bootstrap/management-elevate-init.log`
- `/tmp/cloud-native-platform-runtime-k8s-bootstrap/management-elevate-plan.log`
- `/tmp/cloud-native-platform-runtime-k8s-bootstrap/management-elevate-apply.log`
- `/tmp/cloud-native-platform-runtime-k8s-bootstrap/management-elevate-final-plan.log`

## Temporary Access Validation

Validated from the management EC2 through SSM:

- `kubectl auth can-i create namespaces`: yes
- `kubectl auth can-i create secrets -n apps`: yes
- RDS instance status was readable.
- RDS managed secret ARN was resolvable.
- Secrets Manager `describe-secret` returned metadata.
- Secrets Manager `get-secret-value` returned only `VersionId`.

No RDS managed secret value, password, JWT secret, bootstrap password, or connection string was printed.

Evidence:

- SSM command ID: `0a4c2951-870c-40d1-b115-f4f6b6f4898a`

## Namespace Created

Namespace:

- `apps`

Creation command:

- SSM command ID: `d8780a42-d522-4cde-a652-e7948d9d09d9`

Result:

- `apps` created and `Active`.

## Runtime Secret Created

Secret:

- Namespace: `apps`
- Name: `platform-runtime-secrets`
- Type: `Opaque`

Creation command:

- SSM command ID: `2803987f-1b02-41ac-8269-c37cac820fce`

Method:

- Created from the management EC2 through SSM.
- Used `set +x`.
- Read the RDS managed secret value only into memory.
- Generated runtime secrets in memory.
- Wrote temporary files under `/tmp` with restrictive permissions.
- Created the Kubernetes Secret with `kubectl create secret generic`.
- Removed temporary files through a shell trap.
- Printed only metadata and key names.

Sources used without printing values:

- RDS endpoint and port metadata.
- RDS managed secret username/password.
- Generated JWT secret.
- Generated bootstrap admin password.
- Generated trusted proxy secret.
- Generated internal service secret.
- Lab bootstrap admin email.

Created keys:

- `auth-bootstrap-admin-email`
- `auth-bootstrap-admin-password`
- `auth-connection-string`
- `auth-jwt-secret`
- `platform-internal-service-secret`
- `platform-trusted-proxy-secret`
- `shipment-connection-string`
- `tracking-connection-string`

No secret values were printed, committed, decoded, or stored in documentation.

## Secret Validation

Validation command:

- SSM command ID: `59e5eeae-0400-498c-bc0b-380a9f3b11d9`

Validated:

- Namespace `apps` exists.
- Secret `platform-runtime-secrets` exists.
- Secret contains 8 keys.
- Only key names were printed.

An earlier validation command had a local quoting error while listing keys, but it printed only namespace/Secret metadata and no values.

## Permission Reduction

Reduced only stack:

- `infra/live/dev/management`

Plan result:

- `1 to add`
- `0 to change`
- `2 to destroy`

Controlled changes:

- Replaced management EKS access policy association:
  - from `AmazonEKSClusterAdminPolicy`
  - to `AmazonEKSAdminViewPolicy`
- Destroyed temporary inline policy:
  - `aws_iam_role_policy.runtime_secret_bootstrap[0]`

Apply result:

- `1 added`
- `0 changed`
- `2 destroyed`

Final plan after reduction:

- `No changes`

Evidence:

- `/tmp/cloud-native-platform-runtime-k8s-bootstrap/management-reduce-plan.log`
- `/tmp/cloud-native-platform-runtime-k8s-bootstrap/management-reduce-apply.log`
- `/tmp/cloud-native-platform-runtime-k8s-bootstrap/management-reduce-final-plan.log`

## Final Management Role Permissions

Final EKS access:

- `AmazonEKSAdminViewPolicy`

Confirmed removed:

- `AmazonEKSClusterAdminPolicy`

Final inline policies:

- `cloud-native-platform-dev-management-artifact-read-only`
- `cloud-native-platform-dev-management-eks-read-only`

Final attached policies:

- `AmazonSSMManagedInstanceCore`

Confirmed absent:

- `secretsmanager:GetSecretValue`
- `secretsmanager:DescribeSecret`
- `rds:DescribeDBInstances`
- access to the RDS managed secret

## Final Runtime State

Read-only final check:

- SSM command ID: `ef7d8723-ab9b-4612-adc4-c343c1b3b767`

Result:

- `cluster-addons` remains deployed.
- Namespace `apps` exists.
- Secret `platform-runtime-secrets` exists.
- Secret has 8 expected keys.
- No app pods exist.
- No app deployments exist.
- No app jobs exist.
- No app ingress exists.
- `platform-services` is not installed.

## Helm Render

Local validation passed:

- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace apps --debug`

Rendered manifests include:

- ServiceAccounts.
- IRSA annotations for `shipment-service` and `tracking-service`.
- `auth-service` without IRSA.
- ConfigMaps.
- Services.
- Deployments.
- PodDisruptionBudgets.
- migration Jobs.
- internal ALB Ingress.
- image references by digest.
- Secret references to `platform-runtime-secrets`.

Rendered manifest:

- `/tmp/cloud-native-platform-runtime-k8s-bootstrap/platform-services-runtime-prereqs-rendered.yaml`

## API Gateway And Lambda

Read-only AWS checks returned no matching API Gateway or Lambda resources for the project.

## Remaining Work

Runtime prerequisites are now present.

The remaining blocker before app traffic exists is workload installation and validation:

- Install `platform-services` through the private management path.
- Let migration Jobs run.
- Validate pods, services, ingress, and internal ALB.
- Keep API Gateway integration blocked until the internal ALB exists and is validated.

## Recommended Next Phase

Proceed to:

`Fase 2.8 — Install platform-services through private management path`

Recommended scope:

- Package/render `platform-services` with current dev values.
- Install only the `platform-services` Helm release in namespace `apps`.
- Validate migration Jobs.
- Validate deployments and pods.
- Validate Services and internal ALB Ingress creation.
- Confirm AWS Load Balancer Controller reconciles the ALB.
- Do not apply API Gateway/Lambda yet.

## Explicit Non-Actions

- No Helm install was executed.
- No `kubectl apply` was executed.
- No `kubectl delete` was executed.
- No app workload was deployed.
- No Terraform/Terragrunt apply was run outside `infra/live/dev/management`.
- No Docker build or push was executed.
- No API Gateway or Lambda stack was applied.
- No EKS endpoint public access was changed.
- No Git push was performed.
