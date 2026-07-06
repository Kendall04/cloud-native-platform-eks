# Runtime Secret Sync and Authorizer Apply Readiness

Date: 2026-07-05 18:06:53 CST
Branch: `chore/runtime-secret-sync-authorizer-ready`
AWS account: `145023118802`
Region: `us-east-1`

## Objective

Synchronize the API Gateway authorizer secret source with the Kubernetes runtime secret source, validate runtime health, apply only the Lambda authorizer when safe, and leave API Gateway core/integration unapplied.

## Baseline

Runtime before synchronization:

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
- Kubernetes Secret check: metadata and key names only

Baseline ALB smoke from management EC2:

| Path | HTTP | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/auth/swagger/v1/swagger.json` | 200 | 12819 | Auth service reachable |
| `/shipments/swagger/v1/swagger.json` | 200 | 10618 | Shipment service reachable |
| `/tracking/swagger/v1/swagger.json` | 200 | 6817 | Tracking service reachable |
| `/auth/me` | 401 | 0 | Protected route reachable, token required |
| `/shipments` | 401 | 0 | Protected route reachable, token required |
| `/tracking/00000000-0000-0000-0000-000000000000` | 401 | 0 | Protected route reachable, token required |

## Secrets Manager Metadata Before

Only metadata was inspected.

| Secret | ARN | Previous AWSCURRENT version |
| --- | --- | --- |
| `cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret` | `arn:aws:secretsmanager:us-east-1:145023118802:secret:cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret-TZeeRB` | `b2052387-b6fa-4fff-85b8-4280e8c597c2` |
| `cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret` | `arn:aws:secretsmanager:us-east-1:145023118802:secret:cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret-lZoycC` | `2c390d9d-93d8-44f3-89a3-345fce2fe221` |

## Runtime Secret Consumption

The runtime Secret `platform-runtime-secrets` contains 8 keys.

Relevant key consumption:

| Deployment | Relevant keys |
| --- | --- |
| `auth-service` | `auth-jwt-secret` |
| `shipment-service` | `platform-trusted-proxy-secret` |
| `tracking-service` | `platform-trusted-proxy-secret` |

Rollout scope after patch:

- `auth-service`
- `shipment-service`
- `tracking-service`

## Temporary Management Permission Elevation

Temporary IaC support was added to `infra/modules/management-host`:

- `enable_apps_namespace_edit_access`
- `temporary_secret_write_arns`

Temporary live inputs were enabled only for the operation, then removed.

Elevation plan:

- `2 to add, 0 to change, 0 to destroy`
- Added `AmazonEKSEditPolicy` scoped to namespace `apps`
- Added temporary inline policy for metadata/update access to the two authorizer secret ARNs
- No EC2 replacement
- No security group changes
- No endpoint changes
- No broad IAM

Elevation apply:

- Executed only for `infra/live/dev/management`
- `2 added, 0 changed, 0 destroyed`
- Final plan: `No changes`

## Runtime Secret Synchronization

Strategy:

- Generated new values in memory on the management EC2.
- Wrote new current versions to the two Secrets Manager objects with metadata-only CLI output.
- Patched only the Kubernetes Secret keys:
  - `auth-jwt-secret`
  - `platform-trusted-proxy-secret`
- Removed the temporary patch file from memory-backed storage.
- Unset shell variables containing generated material.

Result:

- Kubernetes Secret `platform-runtime-secrets` patched.
- Secret key names remained the expected 8 keys.
- No secret values were printed.
- No Kubernetes Secret values were read or decoded.

New version metadata:

| Secret | New AWSCURRENT version | Version stages |
| --- | --- | --- |
| `cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret` | `ff313f25-23cf-48e7-93a2-35667f1daf95` | `AWSCURRENT` |
| `cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret` | `eec991eb-d15c-4dc7-8926-e2dda52a920b` | `AWSCURRENT` |

Previous versions moved to `AWSPREVIOUS`:

- `b2052387-b6fa-4fff-85b8-4280e8c597c2`
- `2c390d9d-93d8-44f3-89a3-345fce2fe221`

## Rollout

Triggered rollout restart for:

- `auth-service`
- `shipment-service`
- `tracking-service`

Rollout status:

- `auth-service`: successfully rolled out
- `shipment-service`: successfully rolled out
- `tracking-service`: successfully rolled out

Post-rollout deployments:

- `auth-service`: `2/2`
- `shipment-service`: `2/2`
- `tracking-service`: `2/2`

Pods were Running/Ready with zero restarts after rollout.

## Runtime Smoke After Sync

Post-sync ALB smoke from management EC2:

| Path | HTTP | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/auth/swagger/v1/swagger.json` | 200 | 12819 | Auth service reachable |
| `/shipments/swagger/v1/swagger.json` | 200 | 10618 | Shipment service reachable |
| `/tracking/swagger/v1/swagger.json` | 200 | 6817 | Tracking service reachable |
| `/auth/me` | 401 | 0 | Protected route reachable, token required |
| `/shipments` | 401 | 0 | Protected route reachable, token required |
| `/tracking/00000000-0000-0000-0000-000000000000` | 401 | 0 | Protected route reachable, token required |

No `HTTP 000` and no `5xx` were observed.

Token issuance validation:

- Skipped.
- Reason: no safe non-secret credential path was available, and reading bootstrap credential values is out of scope.

## Authorizer Plan and Apply

Stack:

- `infra/live/dev/api-gateway-authorizer`

Plan:

- `5 to add, 0 to change, 0 to destroy`
- Resources:
  - Lambda function
  - IAM role
  - scoped inline policy
  - basic execution policy attachment
  - CloudWatch log group
- Environment variables contain only non-secret IDs and config names.
- IAM allows only `secretsmanager:GetSecretValue` for the two expected authorizer secret ARN patterns.
- No broad Secrets Manager action.
- No broad Secrets Manager resource.

Apply:

- Executed only for `api-gateway-authorizer`.
- `5 added, 0 changed, 0 destroyed`.
- Final plan: `No changes`.

Non-secret outputs:

| Output | Value |
| --- | --- |
| Function name | `cloud-native-platform-dev-api-jwt-authorizer` |
| Function ARN | `arn:aws:lambda:us-east-1:145023118802:function:cloud-native-platform-dev-api-jwt-authorizer` |
| Invoke ARN | `arn:aws:apigateway:us-east-1:lambda:path/2015-03-31/functions/arn:aws:lambda:us-east-1:145023118802:function:cloud-native-platform-dev-api-jwt-authorizer/invocations` |
| Role ARN | `arn:aws:iam::145023118802:role/cloud-native-platform-dev-api-jwt-authorizer-role` |
| Log group | `/aws/lambda/cloud-native-platform-dev-api-jwt-authorizer` |

Lambda validation:

- Runtime: `nodejs22.x`
- Handler: `src/index.handler`
- State: `Active`
- Last update status: `Successful`
- Timeout: `10`
- Memory: `128`
- Environment keys only:
  - `JWT_AUDIENCE`
  - `AUTH_SERVICE_JWT_SECRET_ID`
  - `JWT_ISSUER`
  - `PLATFORM_TRUSTED_PROXY_SECRET_ID`
  - `SECRET_CACHE_TTL_SECONDS`

## Management Permission Reduction

Temporary live inputs were removed after sync and authorizer apply.

Reduction plan:

- `0 to add, 0 to change, 2 to destroy`
- Removed namespace-scoped `AmazonEKSEditPolicy`
- Removed temporary Secrets Manager update inline policy

Reduction apply:

- Executed only for `infra/live/dev/management`
- `0 added, 0 changed, 2 destroyed`
- Final plan: `No changes`

Final management permissions:

- EKS access policy: `AmazonEKSAdminViewPolicy`
- No `AmazonEKSEditPolicy`
- No `AmazonEKSClusterAdminPolicy`
- Inline role policies:
  - `cloud-native-platform-dev-management-artifact-read-only`
  - `cloud-native-platform-dev-management-eks-read-only`
- Attached role policy:
  - `AmazonSSMManagedInstanceCore`
- Temporary secret update policy removed

## Core and Integration Status

No apply was executed for:

- `apigateway-core`
- `apigateway-integration`

Read-only plan status after authorizer apply:

- `apigateway-core`: `9 to add, 0 to change, 0 to destroy`
- `apigateway-integration`: `10 to add, 0 to change, 0 to destroy`

Core plan notes:

- Uses the real authorizer Lambda invoke ARN.
- VPC Link security group egress remains scoped to TCP/80 toward the internal ALB security groups.

Integration plan notes:

- Still uses mocked core outputs because `apigateway-core` has not been applied.
- Routes remain aligned with the current contract.
- No `/internal/*`, `/admin/*`, swagger, or root `/health` exposure.

## Final Runtime State

Final EKS state:

- `platform-services`: `deployed`, revision `3`
- `auth-service`: `2/2`
- `shipment-service`: `2/2`
- `tracking-service`: `2/2`
- Pods: Running/Ready
- Services and Ingress: present
- Runtime Secret: metadata/key names only validated

Final ALB state:

- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- Scheme: `internal`
- State: `active`
- VPC: `vpc-0fe33938202034387`
- Target groups: 2 healthy targets per service after rollout drain completed

API Gateway core and VPC Link:

- No HTTP API exists yet for the project.
- No VPC Link exists yet for the project.
- Lambda authorizer exists.

## Validations

Local validations:

- `terraform fmt -check -recursive infra`: passed
- `terragrunt hcl format --check infra/live`: passed
- `bash -n scripts/bootstrap-terraform-backend.sh`: passed
- `shellcheck scripts/bootstrap-terraform-backend.sh || true`: no blocking failure
- `helm lint ./k8s/charts/platform-services`: passed
- `helm template platform-services`: passed
- `helm dependency build` for `cluster-addons`: passed
- `helm lint` for `cluster-addons`: passed
- `helm template cluster-addons`: passed

Secret review:

- No `.env`, `.pem`, `.key`, or `.tfvars` files found.
- Pattern matches were reviewed by file path only.
- Matches are expected references, config names, code identifiers, or placeholder/example paths.
- No secret values were printed.

## Remaining Gaps

- API Gateway core has not been applied.
- API Gateway integration has not been applied.
- VPC Link does not exist yet.
- ALB SG ingress from the future VPC Link SG still needs to be validated/modeled before integration apply.
- Token issuance with a real credential was not validated in this phase.

## Recommendation

Proceed to `Fase 2.18 — API Gateway core apply and VPC Link readiness`.

Recommended scope:

- Plan and apply only `apigateway-core` if clean.
- Validate HTTP API, `$default` stage, authorizer wiring, VPC Link, and VPC Link SG.
- Validate VPC Link SG egress remains scoped to TCP/80 toward ALB SGs.
- Resolve ALB SG ingress from VPC Link SG before applying integration.
- Keep `apigateway-integration` out of apply until VPC Link and ALB SG path are validated.
