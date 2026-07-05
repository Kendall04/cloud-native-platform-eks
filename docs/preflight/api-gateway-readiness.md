# API Gateway Readiness Contract

Date: 2026-07-05

Branch: `chore/api-gateway-readiness`

AWS account: `145023118802`

## Purpose

Prepare API Gateway readiness without creating API Gateway, Lambda, VPC Link, or
integration resources.

This phase validates the internal route contract, reviews the existing API
Gateway IaC, confirms ALB compatibility, and records blockers before applying
API Gateway stacks.

## Runtime state

Cluster:

- EKS cluster: `logistics-platform-dev`
- Namespace: `apps`
- Helm release: `platform-services`
- Release status: `deployed`
- Release revision: `3`

Workloads:

- `auth-service`: deployment `2/2`, pods Running/Ready
- `shipment-service`: deployment `2/2`, pods Running/Ready
- `tracking-service`: deployment `2/2`, pods Running/Ready

Runtime prerequisites:

- Secret `platform-runtime-secrets` exists in namespace `apps`.
- Only Secret metadata and key names were validated.
- Secret values were not read, decoded, printed, or committed.

## Internal ALB

ALB:

- Name: `cloud-native-platform-dev`
- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:loadbalancer/app/cloud-native-platform-dev/feabb93df9e991e0`
- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- Scheme: `internal`
- State: `active`
- Type: `application`
- VPC: `vpc-0fe33938202034387`
- Security groups:
  - `sg-079c5a1e99c17270a`
  - `sg-0dcd35733de8447ba`

Listener:

- Protocol: HTTP
- Port: `80`
- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`
- Default action: fixed `404`

Target groups:

- `auth-service`: target type `ip`, health check `/health`, 2 healthy targets
- `shipment-service`: target type `ip`, health check `/health`, 2 healthy targets
- `tracking-service`: target type `ip`, health check `/health`, 2 healthy targets

## Ingress route contract

Ingress `platform-services` exposes these internal ALB paths:

| Path | Backend service | Notes |
| --- | --- | --- |
| `/auth` | `auth-service:8080` | Auth API and auth swagger prefix |
| `/admin/users` | `auth-service:8080` | Admin user API |
| `/shipments` | `shipment-service:8080` | Shipment API and shipment swagger prefix |
| `/admin/shipments` | `shipment-service:8080` | Admin shipment API |
| `/tracking` | `tracking-service:8080` | Tracking API and tracking swagger prefix |
| `/admin/tracking-events` | `tracking-service:8080` | Admin tracking event API |

Root `/health` is not an ingress route. Health checks use `/health` directly on
each backend target.

## Code route discovery

`auth-service`:

| Method | Internal path | Auth | Smoke-safe | API Gateway recommendation |
| --- | --- | --- | --- | --- |
| `POST` | `/auth/register` | anonymous | no | expose only if registration is desired |
| `POST` | `/auth/login` | anonymous | no | expose |
| `POST` | `/auth/refresh` | anonymous | no | expose |
| `GET` | `/auth/me` | user | yes | expose protected |
| `GET` | `/auth/validate` | user | yes | expose protected |
| `GET` | `/admin/users` | admin | yes | expose protected/admin only if required |
| `POST` | `/admin/users/{id}/disable` | admin | no | expose protected/admin only if required |
| `GET` | `/auth/swagger/v1/swagger.json` | anonymous | yes | expose only for demo/readiness or keep private |

`shipment-service`:

| Method | Internal path | Auth | Smoke-safe | API Gateway recommendation |
| --- | --- | --- | --- | --- |
| `POST` | `/shipments` | user | no | expose protected |
| `GET` | `/shipments` | user | yes | expose protected |
| `GET` | `/shipments/{id}` | user | yes | expose protected |
| `GET` | `/shipments/by-tracking/{trackingNumber}` | user | yes | expose protected |
| `PATCH` | `/admin/shipments/{id}` | admin | no | expose protected/admin only if required |
| `GET` | `/shipments/swagger/v1/swagger.json` | anonymous | yes | expose only for demo/readiness or keep private |
| `GET` | `/internal/shipments/*` | internal shared secret | no | do not expose |

`tracking-service`:

| Method | Internal path | Auth | Smoke-safe | API Gateway recommendation |
| --- | --- | --- | --- | --- |
| `GET` | `/tracking/{shipmentId}` | user | yes | expose protected |
| `GET` | `/tracking/by-tracking-number/{trackingNumber}` | user | yes | expose protected |
| `POST` | `/admin/tracking-events` | admin | no | expose protected/admin only if required |
| `GET` | `/tracking/swagger/v1/swagger.json` | anonymous | yes | expose only for demo/readiness or keep private |

## Smoke evidence

Smoke tests were GET-only from the private management EC2 to the internal ALB.
No request bodies requiring mutation were sent, and response bodies were not
recorded in this document.

| Path | HTTP status | Interpretation |
| --- | --- | --- |
| `/health` | `404` | ALB reachable; root health route is not defined |
| `/auth` | `404` | ALB reachable; `GET /auth` is not implemented |
| `/auth/swagger` | `301` | Swagger UI redirect |
| `/auth/swagger/v1/swagger.json` | `200` | Auth swagger document reachable |
| `/auth/me` | `401` | Protected auth route reachable without token |
| `/admin/users` | `401` | Protected admin route reachable without token |
| `/shipments` | `401` | Protected shipment route reachable without token |
| `/shipments/swagger` | `301` | Swagger UI redirect |
| `/shipments/swagger/v1/swagger.json` | `200` | Shipment swagger document reachable |
| `/admin/shipments` | `404` | ALB reachable; `GET /admin/shipments` is not implemented |
| `/tracking` | `404` | ALB reachable; `GET /tracking` is not implemented |
| `/tracking/swagger` | `301` | Swagger UI redirect |
| `/tracking/swagger/v1/swagger.json` | `200` | Tracking swagger document reachable |
| `/admin/tracking-events` | `405` | ALB reachable; route exists but GET is not allowed |
| `/tracking/00000000-0000-0000-0000-000000000000` | `401` | Protected tracking route reachable without token |

No route returned `HTTP 000`.

No 5xx response was observed during route-contract smoke testing.

## Existing API Gateway IaC

Live stacks:

- `infra/live/dev/api-gateway-authorizer`
- `infra/live/dev/apigateway-core`
- `infra/live/dev/apigateway-integration`

Modules:

- `infra/modules/lambda`
- `infra/modules/apigateway-core`
- `infra/modules/apigateway-integration`

### Authorizer stack

`api-gateway-authorizer` uses the Lambda module and packages:

- Function name: `cloud-native-platform-dev-api-jwt-authorizer`
- Runtime: `nodejs22.x`
- Handler: `src/index.handler`

The stack expects JWT/proxy secret inputs through environment variables. Those
values were not read or printed in this phase.

### Core stack

`apigateway-core` is designed to create:

- HTTP API v2
- `$default` stage
- CloudWatch access log group
- API Gateway VPC Link
- VPC Link security group
- REQUEST Lambda authorizer
- Lambda invoke permission for API Gateway

Outputs expected by dependent stacks:

- `api_id`
- `api_endpoint`
- `execution_arn`
- `vpc_link_id`
- `authorizer_id`
- `stage_name`
- `stage_invoke_url`

### Integration stack

`apigateway-integration` is designed to create HTTP proxy integrations through
the VPC Link to the ALB listener.

It currently expects to discover the ALB by tags and uses the ALB listener ARN as
the private integration URI.

Configured route sets:

- Public integration routes
- Protected integration routes with Lambda authorizer
- Header injection for authenticated upstream calls

## Read-only plan results

No apply was run.

`apigateway-core` plan:

- Result: plan succeeded
- Summary: `8 to add, 0 to change, 0 to destroy`
- Expected additions:
  - HTTP API
  - `$default` stage
  - CloudWatch log group
  - VPC Link security group
  - VPC Link egress rule
  - VPC Link
  - REQUEST authorizer
  - Lambda permission

Core readiness gap:

- The planned VPC Link egress rule is broad: all protocols to `0.0.0.0/0`.
- Follow-up should consider replacing this with scoped egress to the internal ALB
  security groups on TCP/80, matching the management smoke path pattern.

`apigateway-integration` plan:

- Result: plan failed before producing a resource plan
- Failure: the ALB data source returned zero results
- Cause: the stack searches for ALB tags that do not match the actual ALB tags

Actual ALB tags observed:

- `ingress.k8s.aws/resource = LoadBalancer`
- `ingress.k8s.aws/stack = cloud-native-platform`
- `elbv2.k8s.aws/cluster = logistics-platform-dev`

Integration readiness gap:

- Update ALB discovery to use tags that exist on the controller-managed ALB, or
  pass the ALB/listener ARN explicitly from evidence/outputs.

## ALB and API Gateway compatibility

The ALB is compatible with an API Gateway HTTP API private integration:

- It is an internal Application Load Balancer.
- It has an HTTP/80 listener.
- It has listener rules for the existing path prefixes.
- Target groups are `ip` type and healthy.
- The integration module is already modeled to use a VPC Link and ALB listener
  ARN.

Path compatibility:

- `/auth/{proxy+}` can route to the auth ingress path.
- `/shipments/{proxy+}` can route to the shipment ingress path.
- `/tracking/{proxy+}` can route to the tracking ingress path.
- `/admin/*` paths can route to the existing admin ingress paths if explicitly
  allowed.
- Root `/health` should not be exposed as a public API route because it is not
  defined at the ALB root.

## VPC Link and security group readiness

Required path:

- API Gateway HTTP API -> VPC Link ENIs -> internal ALB listener TCP/80

Security group follow-up:

- VPC Link security group should allow egress TCP/80 to ALB security groups:
  - `sg-079c5a1e99c17270a`
  - `sg-0dcd35733de8447ba`
- ALB security group ingress should allow TCP/80 from the VPC Link security
  group.

Current gap:

- `apigateway-core` plans broad VPC Link egress.
- The ALB security group path for the future VPC Link security group is not yet
  applied or validated.

## Recommended API Gateway contract

Recommended public auth routes:

| External route | Method | Internal ALB path | Auth | Expected unauthenticated response |
| --- | --- | --- | --- | --- |
| `/auth/login` | `POST` | `/auth/login` | none | endpoint handles credentials |
| `/auth/register` | `POST` | `/auth/register` | none or disabled by policy | endpoint handles registration |
| `/auth/refresh` | `POST` | `/auth/refresh` | none | endpoint handles refresh token flow |

Recommended protected user routes:

| External route | Method | Internal ALB path | Auth | Expected unauthenticated response |
| --- | --- | --- | --- | --- |
| `/auth/me` | `GET` | `/auth/me` | user | `401` |
| `/auth/validate` | `GET` | `/auth/validate` | user | `401` |
| `/shipments` | `GET,POST` | `/shipments` | user | `401` for GET without token |
| `/shipments/{proxy+}` | `GET` | `/shipments/{proxy+}` | user | `401` without token |
| `/tracking/{proxy+}` | `GET` | `/tracking/{proxy+}` | user | `401` without token |

Recommended admin routes:

| External route | Method | Internal ALB path | Auth | Recommendation |
| --- | --- | --- | --- | --- |
| `/admin/users/{proxy+}` | `GET,POST` as implemented | `/admin/users/{proxy+}` | admin | expose only if admin API is in scope |
| `/admin/shipments/{proxy+}` | `PATCH` as implemented | `/admin/shipments/{proxy+}` | admin | expose only if admin API is in scope |
| `/admin/tracking-events` | `POST` | `/admin/tracking-events` | admin | expose only if admin event injection is in scope |

Routes to exclude:

- `/internal/*`
- Root `/health`
- Broad public `ANY /auth/{proxy+}` if it would unintentionally bypass the
  Lambda authorizer for protected auth routes.

## Swagger and admin exposure decision

Swagger:

- Safe for readiness/demo if intentionally exposed.
- Recommended production posture: keep private or restrict exposure.
- If exposed temporarily, use explicit `GET` routes only:
  - `/auth/swagger`
  - `/auth/swagger/{proxy+}`
  - `/shipments/swagger`
  - `/shipments/swagger/{proxy+}`
  - `/tracking/swagger`
  - `/tracking/swagger/{proxy+}`

Admin routes:

- Existing admin routes are reachable through the ALB.
- They should remain protected by the Lambda authorizer and upstream role
  checks.
- Do not expose admin routes publicly unless the phase explicitly includes admin
  API validation and role-based authorization checks.

## Auth/Postgres warning decision

Auth logs still show an intermittent PostgreSQL health check warning in one pod
tail.

Decision:

- Not a blocker for API Gateway readiness planning.

Justification:

- `auth-service` deployment remains `2/2`.
- Auth pods are Running/Ready with no restarts.
- Auth target group has 2 healthy targets.
- Auth swagger and protected auth routes return expected HTTP responses through
  the ALB.

Follow-up:

- Keep the warning documented.
- Recheck during the next API Gateway readiness phase.
- Treat it as a blocker if it becomes persistent readiness failure, 5xx, target
  unhealthy state, migration failure, or live DB connectivity failure.

## API Gateway and Lambda current state

Read-only checks returned no project API Gateway HTTP APIs and no project Lambda
functions.

Not applied:

- `api-gateway-authorizer`
- `apigateway-core`
- `apigateway-integration`
- API Gateway routes/integrations
- Lambda authorizer

## Local validation

Executed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh`
- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace apps`
- `helm dependency build` for `cluster-addons`
- `helm lint` for `cluster-addons`
- `helm template` for `cluster-addons`

Results:

- All required validations passed.
- `shellcheck` produced no blocking output.
- Helm lint reported only the existing icon recommendation.

## Secret review

Checked for local sensitive file extensions:

- `*.env`
- `*.pem`
- `*.key`
- `*.tfvars`

Result:

- No matching files found.

Pattern scan was run across docs, infra, scripts, GitHub workflow files, k8s
configuration, and microservices excluding generated/build directories and
Markdown files.

Result:

- Matches were limited to expected code/config references such as environment
  variable names, placeholder/runtime secret keys, AWS SDK references, and
  service configuration identifiers.
- No secret values were identified or printed.

## Evidence logs

Local evidence files:

- `/tmp/cloud-native-platform-apigw-readiness/apigateway-core-init.log`
- `/tmp/cloud-native-platform-apigw-readiness/apigateway-core-plan.log`
- `/tmp/cloud-native-platform-apigw-readiness/apigateway-integration-init.log`
- `/tmp/cloud-native-platform-apigw-readiness/apigateway-integration-plan.log`
- `/tmp/platform-services-dev-apigw-readiness.yaml`
- `/tmp/cluster-addons.yaml`

SSM command IDs:

- Runtime state: `263eef94-13da-4ff3-95c0-738105978fa8`
- Route contract smoke: `1128a423-1b0b-4f8e-be77-b8cb5f50fbcb`
- Auth warning check: `cc8720b0-3ece-4fbf-83a8-91c2cffa78d6`
- Final runtime check: `1bb9f114-6836-4773-aa5c-875c6d671d7f`

## Blockers and gaps

Before applying API Gateway:

1. Fix or adjust ALB discovery in `apigateway-integration`.
2. Review VPC Link security group egress in `apigateway-core` and scope it to
   the internal ALB TCP/80 path if feasible.
3. Ensure ALB security group ingress allows TCP/80 from the VPC Link security
   group after it exists.
4. Avoid broad public `ANY /auth/{proxy+}` if it can overlap protected auth
   routes.
5. Decide whether swagger and admin routes are in-scope for first exposure.
6. Recheck the auth/Postgres warning during API Gateway implementation.

## Recommended next phase

`Fase 2.13 — API Gateway core and integration plan hardening`

Recommended scope:

1. Adjust ALB discovery for the AWS Load Balancer Controller tags or pass the
   ALB listener ARN explicitly.
2. Tighten VPC Link security group egress to the internal ALB TCP/80 path where
   practical.
3. Validate route priority so protected auth routes are not shadowed by public
   auth proxy routes.
4. Decide swagger/admin exposure for the first API Gateway deployment.
5. Run clean plans for `api-gateway-authorizer`, `apigateway-core`, and
   `apigateway-integration`.
6. Do not apply API Gateway/Lambda until the plans and route contract are clean.
