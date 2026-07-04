# Runtime Namespace And Secrets Bootstrap

Date: 2026-07-04

Branch: `chore/runtime-bootstrap`

AWS account: `145023118802`

## Summary

Fase 2.7 attempted to bootstrap the runtime namespace and Kubernetes Secret required before installing `platform-services`.

The phase stopped before creating mutable Kubernetes resources because the private management EC2 role cannot read the RDS metadata/managed secret required to build the database connection strings safely from inside the private cluster access path.

No secret values were printed, written to the repository, or stored in documentation.

## Starting State

- EKS cluster `logistics-platform-dev` is healthy.
- `cluster-addons` is deployed in `kube-system`.
- AWS Load Balancer Controller and Cluster Autoscaler are running.
- Private management EC2 access through SSM works.
- `platform-services` is not installed.
- Namespace `apps` does not exist.
- Secret `platform-runtime-secrets` does not exist.
- API Gateway and Lambda are not applied.

Management EC2 used for validation:

- Instance ID: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- Public IP: none
- IAM profile: `cloud-native-platform-dev-management-profile`

## Required Secret Keys

The chart expects one Kubernetes Secret named `platform-runtime-secrets` in namespace `apps`.

| Key | Consumers | Source | Required |
| --- | --- | --- | --- |
| `auth-connection-string` | `auth-service`, `auth-service-migrate` | RDS endpoint, port, username, password | yes |
| `auth-jwt-secret` | `auth-service`, `auth-service-migrate` | generated high-entropy value | yes |
| `auth-bootstrap-admin-email` | `auth-service`, `auth-service-migrate` | operator/lab bootstrap value | yes for current chart reference |
| `auth-bootstrap-admin-password` | `auth-service`, `auth-service-migrate` | generated high-entropy value | yes for current chart reference |
| `shipment-connection-string` | `shipment-service`, `shipment-service-migrate` | RDS endpoint, port, username, password | yes |
| `tracking-connection-string` | `tracking-service`, `tracking-service-migrate` | RDS endpoint, port, username, password | yes |
| `platform-trusted-proxy-secret` | `shipment-service`, `tracking-service`, migration jobs | generated high-entropy value | yes |
| `platform-internal-service-secret` | `shipment-service`, `tracking-service`, migration jobs | generated high-entropy value | yes |

Non-secret runtime config is already rendered through ConfigMaps/values:

- `ASPNETCORE_ENVIRONMENT=Production`
- `AWS__Region=us-east-1`
- `AWS__EventBusName=cloud-native-platform-dev-bus`
- `AWS__ShipmentEventsQueueUrl`
- `ShipmentService__BaseUrl`
- per-service `Database__Schema`

## Runtime Metadata

Read-only metadata validation confirmed:

- RDS instance `cloud-native-platform-dev-postgres` is `available`.
- Engine/version: `postgres 15.18`.
- RDS is private and encrypted.
- RDS endpoint and port exist.
- RDS has an AWS-managed master user secret ARN.
- SQS queues exist, including `cloud-native-platform-dev-shipment-events-queue`.
- EventBridge bus `cloud-native-platform-dev-bus` exists.

No RDS password or SecretString was printed.

## Blocker

The management EC2 role failed a non-secret permission check before namespace/Secret creation:

- Command: SSM Run Command `a1483426-c260-40e4-b127-f0c89de53843`
- Operation attempted: `rds:DescribeDBInstances`
- Principal: `cloud-native-platform-dev-management-role`
- Result: `AccessDenied`

Because the management host cannot read RDS metadata, it also cannot safely fetch the RDS managed secret and construct connection strings inside the private path.

Passing secret values from the local machine into SSM command parameters was intentionally not used because that would expose secret material in command history/invocation metadata. No fallback path was used.

## Namespace And Secret Result

- Namespace `apps`: not created.
- Secret `platform-runtime-secrets`: not created.

This avoids leaving a partial runtime bootstrap while the Secret creation path is blocked.

## Render Validation

Local Helm validation passed:

- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace apps --debug`

Rendered manifests include:

- ServiceAccounts for `auth-service`, `shipment-service`, and `tracking-service`.
- IRSA annotations for `shipment-service` and `tracking-service`.
- No IRSA annotation for `auth-service`.
- ConfigMaps.
- Deployments.
- Services.
- PodDisruptionBudgets.
- Migration Jobs.
- Internal ALB Ingress.
- Image references by immutable digest in account `145023118802`.
- Secret references to `platform-runtime-secrets`.

The render did not include the old account `795708473882` or empty image digests.

## Post-Check

Read-only validation after stopping confirmed:

- `cluster-addons` remains the only Helm release.
- Namespace `apps` still does not exist.
- No app pods, deployments, jobs, or ingress exist in `apps`.
- API Gateway is not created.
- Lambda is not created.

## Remaining Blockers

- Management role needs a controlled, temporary bootstrap path to read the RDS managed secret without exposing values.
- Namespace `apps` still needs to be created.
- Secret `platform-runtime-secrets` still needs to be created.
- `platform-services` is not installed.
- API Gateway integration remains blocked until the internal ALB exists and is validated.

## Recommended Next Phase

Proceed to a focused follow-up before workload install:

`Fase 2.7b — Runtime secret source access fix`

Recommended scope:

- Add the minimum bootstrap-only IAM permissions to the management role, preferably scoped to:
  - `rds:DescribeDBInstances` for the dev RDS instance.
  - `secretsmanager:GetSecretValue` for the RDS managed secret ARN.
- Apply only the management stack if IaC owns that role.
- Create namespace `apps`.
- Create `platform-runtime-secrets` from inside the management EC2 without printing values.
- Remove or reduce any temporary secret-read permission after the Secret is created, if the permission is not needed for ongoing operations.
- Validate the Secret by listing keys only.
- Do not install `platform-services` yet.

## Explicit Non-Actions

- No Helm install was executed.
- No `kubectl apply` was executed.
- No `kubectl delete` was executed.
- No workloads were deployed.
- No namespace was created.
- No Kubernetes Secret was created.
- No Terraform/Terragrunt apply or destroy was executed.
- No Docker build or push was executed.
- No API Gateway or Lambda stack was applied.
- No EKS public endpoint change was made.
- No Git push was performed.
