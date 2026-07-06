# Runtime Secret Source Access Fix

Date: 2026-07-04

Branch: `chore/runtime-secret-source-access`

AWS account: `145023118802`

## Summary

Fase 2.7b added a temporary, scoped permission path for the private management EC2 to resolve the RDS runtime secret source, validated that access without printing secret values, and then removed the temporary AWS secret-source permissions.

The phase did not complete namespace/Secret creation because Kubernetes RBAC blocked namespace creation. The management role still has read-only cluster access through `AmazonEKSAdminViewPolicy`, which cannot create cluster-scoped namespaces.

No workload was installed, and no secret values were printed, committed, or stored in documentation.

## Starting State

- EKS cluster `logistics-platform-dev` was healthy.
- `cluster-addons` was deployed.
- Private management EC2 was available through SSM.
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

- SSM command ID: `a7b37c90-074e-465c-825e-664ca8259e6d`

## Temporary Permission

Temporary inline policy:

- `cloud-native-platform-dev-management-runtime-secret-bootstrap`

Actions:

- `rds:DescribeDBInstances`
- `secretsmanager:DescribeSecret`
- `secretsmanager:GetSecretValue`

Scope:

- `rds:DescribeDBInstances` used `Resource="*"` because this RDS describe API is used for instance metadata lookup.
- Secrets Manager actions were scoped only to the RDS managed secret ARN for `cloud-native-platform-dev-postgres`.

No wildcard Secrets Manager access was granted.

## Management Elevation

Applied only stack:

- `infra/live/dev/management`

Plan result:

- `1 to add`
- `0 to change`
- `0 to destroy`

Resource added:

- `aws_iam_role_policy.runtime_secret_bootstrap[0]`

Apply result:

- `1 added`
- `0 changed`
- `0 destroyed`

Final plan after elevation:

- `No changes`

Evidence:

- `/tmp/cloud-native-platform-runtime-secret-source-access/management-elevate-init.log`
- `/tmp/cloud-native-platform-runtime-secret-source-access/management-elevate-plan.log`
- `/tmp/cloud-native-platform-runtime-secret-source-access/management-elevate-apply.log`
- `/tmp/cloud-native-platform-runtime-secret-source-access/management-elevate-final-plan.log`

## Temporary Access Validation

Validated from the management EC2 through SSM without printing secret values:

- RDS instance status was readable.
- RDS managed secret ARN was resolvable.
- Secrets Manager `describe-secret` returned the ARN.
- Secrets Manager `get-secret-value` returned only `VersionId`.

No `SecretString`, password, JWT secret, bootstrap password, or connection string was printed.

Evidence:

- SSM command ID: `cb00ec17-ebe7-48de-82bc-c0bc48c4ee68`

## Runtime Secret Key Mapping

The chart still expects one Kubernetes Secret:

- Namespace: `apps`
- Secret: `platform-runtime-secrets`

Expected keys:

- `auth-connection-string`
- `auth-jwt-secret`
- `auth-bootstrap-admin-email`
- `auth-bootstrap-admin-password`
- `shipment-connection-string`
- `tracking-connection-string`
- `platform-trusted-proxy-secret`
- `platform-internal-service-secret`

Values were not generated or printed because namespace creation failed before Secret creation.

## Namespace Creation Attempt

Attempted command:

- `kubectl create namespace apps`

Result:

- Failed with Kubernetes RBAC `Forbidden`.

Reason:

- Principal `cloud-native-platform-dev-management-role` has read-only EKS access through `AmazonEKSAdminViewPolicy`.
- That policy does not allow creating namespace resources at cluster scope.

Evidence:

- SSM command ID: `9d17fa51-1f02-4536-b304-78f758f6ddb1`

## Secret Creation Result

Secret creation was not attempted after namespace creation failed.

Created resources:

- Namespace `apps`: not created.
- Secret `platform-runtime-secrets`: not created.

This avoided creating or passing secret material through an incomplete bootstrap path.

## Permission Reduction

The temporary live input was removed and the management stack was applied again.

Reduction plan:

- `0 to add`
- `0 to change`
- `1 to destroy`

Destroyed resource:

- `aws_iam_role_policy.runtime_secret_bootstrap[0]`

Reduction apply:

- `0 added`
- `0 changed`
- `1 destroyed`

Final plan after reduction:

- `No changes`

Evidence:

- `/tmp/cloud-native-platform-runtime-secret-source-access/management-reduce-plan.log`
- `/tmp/cloud-native-platform-runtime-secret-source-access/management-reduce-apply.log`
- `/tmp/cloud-native-platform-runtime-secret-source-access/management-reduce-final-plan.log`

## Final Management Role Permissions

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

- SSM command ID: `12d2e4b7-ec69-4fcc-83f3-15f8df65d817`

Result:

- `cluster-addons` remains deployed.
- Namespace `apps` does not exist.
- Secret `platform-runtime-secrets` does not exist.
- No app pods, deployments, jobs, or ingress exist.
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

- `/tmp/cloud-native-platform-runtime-secret-source-access/platform-services-runtime-bootstrap-rendered.yaml`

## API Gateway And Lambda

Read-only AWS checks returned no matching API Gateway or Lambda resources for the project.

## Remaining Blocker

The next blocker is Kubernetes write access for the bootstrap operation:

- Create namespace `apps`.
- Create Secret `platform-runtime-secrets`.

The management role currently has read-only cluster access and cannot create namespaces or Secrets.

## Recommended Next Phase

Proceed to:

`Fase 2.7c — Controlled Kubernetes bootstrap access for runtime prerequisites`

Recommended scope:

- Temporarily elevate management EKS access from `AmazonEKSAdminViewPolicy` to a policy that can create only the required bootstrap resources, or use a short-lived controlled cluster-admin path if fine-grained access is not available.
- Re-add the temporary scoped RDS/Secrets Manager source access.
- Create namespace `apps`.
- Create `platform-runtime-secrets` without printing values.
- Validate only namespace metadata and Secret key names.
- Reduce EKS access back to `AmazonEKSAdminViewPolicy`.
- Remove `secretsmanager:GetSecretValue` and any RDS describe bootstrap permission.
- Confirm no workloads are installed.

## Explicit Non-Actions

- No Helm install was executed.
- No `kubectl apply` was executed.
- No `kubectl delete` was executed.
- No workload was deployed.
- No Kubernetes Secret was created.
- No namespace was created.
- No Terraform/Terragrunt apply was run outside `infra/live/dev/management`.
- No Docker build or push was executed.
- No API Gateway or Lambda stack was applied.
- No EKS endpoint public access was changed.
- No Git push was performed.
