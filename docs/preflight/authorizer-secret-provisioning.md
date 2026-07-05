# Authorizer Secret Provisioning Evidence

Date: 2026-07-05
Branch: `chore/authorizer-secret-provisioning`
AWS account: `145023118802`
Region: `us-east-1`

## Objective

Provision the AWS Secrets Manager references required by the API Gateway Lambda authorizer without exposing secret values in Git, logs, Terraform state, or documentation.

## Baseline

Runtime before provisioning remained healthy:

- EKS cluster: `logistics-platform-dev`
- Helm release: `platform-services`
- Namespace: `apps`
- Release status: `deployed`
- Release revision: `3`
- Deployments:
  - `auth-service`: `2/2`
  - `shipment-service`: `2/2`
  - `tracking-service`: `2/2`
- Pods: Running/Ready
- Services and Ingress: present
- Runtime Secret validation: metadata and key names only, no values read or decoded

Baseline ALB smoke from management EC2:

| Path | HTTP | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/auth/swagger/v1/swagger.json` | 200 | 12819 | Auth service reachable |
| `/shipments/swagger/v1/swagger.json` | 200 | 10618 | Shipment service reachable |
| `/tracking/swagger/v1/swagger.json` | 200 | 6817 | Tracking service reachable |
| `/auth/me` | 401 | 0 | Protected route reachable, token required |
| `/shipments` | 401 | 0 | Protected route reachable, token required |
| `/tracking/00000000-0000-0000-0000-000000000000` | 401 | 0 | Protected route reachable, token required |

## Expected Authorizer References

The authorizer stack expects non-secret Secrets Manager IDs:

- `AUTH_SERVICE_JWT_SECRET_ID`: `cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret`
- `PLATFORM_TRUSTED_PROXY_SECRET_ID`: `cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret`

IAM is scoped to:

- `arn:aws:secretsmanager:us-east-1:145023118802:secret:cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret-*`
- `arn:aws:secretsmanager:us-east-1:145023118802:secret:cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret-*`

## Initial Inventory

Metadata-only Secrets Manager and SSM inventory was performed.

Initial result:

- No matching Secrets Manager secret objects existed for the authorizer references.
- No matching SSM parameters existed for `cloud-native-platform`.
- No `get-secret-value` was executed.
- No `get-parameter --with-decryption` was executed.

## Strategy

Chosen strategy: **rotation/generation with new Secrets Manager values**.

Rationale:

- Avoids reading or decoding existing Kubernetes Secret values.
- Avoids copying current values through ad hoc shell pipelines.
- Establishes AWS Secrets Manager as the source for the API Gateway authorizer.
- Keeps Terraform state free of secret values by managing only secret containers with Terraform.

Impact:

- The generated `AUTH_SERVICE_JWT_SECRET` equivalent does not match the current Kubernetes runtime secret used by `auth-service`.
- The generated `PLATFORM_TRUSTED_PROXY_SECRET` equivalent does not match the current Kubernetes runtime secret used by services.
- Therefore, authorizer apply was skipped in this phase.
- A later controlled phase must synchronize or rotate the Kubernetes runtime secret source before authorizer/API Gateway traffic is enabled.

## IaC Changes

Added a dedicated module:

- `infra/modules/authorizer-secrets`

Added a dev live stack:

- `infra/live/dev/api-gateway-authorizer-secrets`

The module creates only `aws_secretsmanager_secret` containers.

It intentionally does not create:

- `aws_secretsmanager_secret_version`
- `secret_string`
- `secret_binary`

Outputs are non-secret:

- secret names
- secret ARNs

## Secret Containers Plan and Apply

Stack:

- `infra/live/dev/api-gateway-authorizer-secrets`

Plan:

- `2 to add, 0 to change, 0 to destroy`
- Resources: two Secrets Manager secret containers
- No secret values in plan
- No unrelated changes

Apply:

- Executed only for `api-gateway-authorizer-secrets`
- `2 added, 0 changed, 0 destroyed`

Final plan:

- `No changes`

Secret names and ARNs:

| Logical key | Name | ARN |
| --- | --- | --- |
| `auth-service-jwt-secret` | `cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret` | `arn:aws:secretsmanager:us-east-1:145023118802:secret:cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret-TZeeRB` |
| `platform-trusted-proxy-secret` | `cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret` | `arn:aws:secretsmanager:us-east-1:145023118802:secret:cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret-lZoycC` |

## Secret Versions

Secret versions were populated outside Terraform using generated values.

Safety controls:

- Values generated in shell memory only.
- Values were not echoed.
- Values were not written to files.
- Values were not committed.
- CLI output was restricted to ARN, Name, VersionId, and VersionStages.
- No secret payload was printed.

Version metadata:

| Secret | VersionId | VersionStages |
| --- | --- | --- |
| `cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret` | `b2052387-b6fa-4fff-85b8-4280e8c597c2` | `AWSCURRENT` |
| `cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret` | `2c390d9d-93d8-44f3-89a3-345fce2fe221` | `AWSCURRENT` |

## Metadata Validation

`describe-secret` metadata was checked after provisioning.

Validated:

- Both secret objects exist.
- Both have an `AWSCURRENT` version.
- No secret values were read.
- No `get-secret-value` was executed.

## Authorizer Plan

Stack:

- `infra/live/dev/api-gateway-authorizer`

Plan result:

- `5 to add, 0 to change, 0 to destroy`
- Clean plan
- Lambda environment variables contain only secret IDs and non-secret config
- No secret values in plan
- IAM action is scoped to `secretsmanager:GetSecretValue`
- No `secretsmanager:*`
- No broad `Resource: "*"` for Secrets Manager

Authorizer apply:

- Skipped.

Reason:

- The new Secrets Manager values are not yet synchronized with the currently running Kubernetes runtime secrets.
- Applying the authorizer now would create a Lambda that can retrieve secrets, but those values would not validate current auth-service tokens until runtime secret synchronization is completed.

## Core and Integration

No apply was executed for:

- `apigateway-core`
- `apigateway-integration`

Read-only plan status:

- `apigateway-core`: `9 to add, 0 to change, 0 to destroy`, using mocked authorizer outputs because authorizer was not applied.
- `apigateway-integration`: `10 to add, 0 to change, 0 to destroy`, using mocked core outputs because core was not applied.

Remaining integration gap:

- ALB SG ingress from the future VPC Link SG still needs to be resolved before `apigateway-integration` apply.

## Runtime Smoke After Provisioning

Post-provisioning ALB smoke from management EC2:

| Path | HTTP | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/auth/swagger/v1/swagger.json` | 200 | 12819 | Auth service reachable |
| `/shipments/swagger/v1/swagger.json` | 200 | 10618 | Shipment service reachable |
| `/tracking/swagger/v1/swagger.json` | 200 | 6817 | Tracking service reachable |
| `/auth/me` | 401 | 0 | Protected route reachable, token required |
| `/shipments` | 401 | 0 | Protected route reachable, token required |
| `/tracking/00000000-0000-0000-0000-000000000000` | 401 | 0 | Protected route reachable, token required |

No `HTTP 000` and no `5xx` were observed.

## Validations

Completed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh || true`
- `helm lint` for `platform-services`
- `helm template` for `platform-services`
- `helm dependency build`, `helm lint`, and `helm template` for `cluster-addons`

## Security Review

No secret values were committed or documented.

Explicitly not executed:

- `aws secretsmanager get-secret-value`
- `aws ssm get-parameter --with-decryption`
- Kubernetes Secret decode
- `kubectl get secret -o yaml`
- `kubectl get secret -o json` including `.data`

## Remaining Gaps

- Synchronize or rotate Kubernetes runtime secrets to use the same source as the authorizer.
- Apply `api-gateway-authorizer` only after runtime secret alignment is planned and accepted.
- Apply `apigateway-core` only after authorizer outputs are real.
- Resolve ALB SG ingress from VPC Link SG before `apigateway-integration`.
- Do not enable API Gateway traffic until protected route validation is possible end to end.

## Recommended Next Phase

`Fase 2.17 — Runtime secret source synchronization and authorizer apply readiness`

Recommended scope:

- Decide whether to rotate Kubernetes runtime secrets to the new Secrets Manager values or perform a controlled migration from an approved source.
- Avoid reading or decoding Kubernetes Secrets casually.
- If rotating, update Kubernetes runtime Secret from the AWS source using a sanitized, explicitly approved procedure.
- Roll out affected services in a controlled way.
- Validate auth token issuance and protected route behavior.
- Apply `api-gateway-authorizer` only after secret alignment is safe.
- Keep `apigateway-core` and `apigateway-integration` out of apply until authorizer outputs are real and VPC Link to ALB SG ingress is resolved.
