# API Gateway Authenticated Smoke Evidence

Date: 2026-07-06
Branch: `chore/apigateway-authenticated-smoke`
AWS account: `145023118802`
Region: `us-east-1`

## Scope

This phase validated the authenticated happy path through API Gateway after the
integration layer was applied. The test used a disposable dev user created
through the public registration endpoint. No bootstrap/admin credentials were
read or used.

No infrastructure, Helm release, Kubernetes resource, Docker image, endpoint, or
secret was changed.

## Baseline Runtime, API, And Network

Runtime baseline:

- EKS cluster: `logistics-platform-dev`
- Helm release: `platform-services`
- Namespace: `apps`
- Release revision: `4`
- Deployments:
  - `auth-service`: `2/2`
  - `shipment-service`: `2/2`
  - `tracking-service`: `2/2`
- Pods: Running/Ready
- Ingress: `platform-services`
- Ingress annotation: `alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16`
- Kubernetes Secret check: metadata/key names and byte sizes only

API Gateway and network baseline:

- API ID: `diluedb2k7`
- Endpoint: `https://diluedb2k7.execute-api.us-east-1.amazonaws.com`
- Stage: `$default`
- Authorizer ID: `evdkk3`
- VPC Link ID: `c2au37`
- VPC Link status: `AVAILABLE`
- VPC Link SG: `sg-0a2f21b748db94d8b`
- VPC Link SG egress: TCP/80 only to ALB SGs
- ALB frontend ingress: TCP/80 from `10.0.0.0/16`
- ALB frontend ingress from `0.0.0.0/0`: absent

Routes and integrations were present before the authenticated smoke:

- Public:
  - `POST /auth/login`
  - `POST /auth/register`
  - `POST /auth/refresh`
- Protected:
  - `GET /auth/me`
  - `GET /auth/validate`
  - `ANY /shipments`
  - `ANY /shipments/{proxy+}`
  - `ANY /tracking/{proxy+}`

## Auth Contract Discovered From Code

Source files reviewed:

- `microservices/auth-service/src/AuthService.Api/Controllers/AuthController.cs`
- `microservices/auth-service/src/AuthService.Application/Contracts/Auth/RegisterUserRequest.cs`
- `microservices/auth-service/src/AuthService.Application/Contracts/Auth/LoginRequest.cs`
- `microservices/auth-service/src/AuthService.Application/Contracts/Auth/RefreshTokenRequest.cs`
- `microservices/auth-service/src/AuthService.Application/Contracts/Auth/AuthenticationResponse.cs`
- `microservices/auth-service/src/AuthService.Infrastructure/Services/AuthenticationService.cs`

Request contracts:

- `POST /auth/register`
  - `email`
  - `password`
  - `firstName`
  - `lastName`
- `POST /auth/login`
  - `email`
  - `password`
- `POST /auth/refresh`
  - `refreshToken`

Response token fields:

- `accessToken`
- `refreshToken`

Protected auth routes:

- `/auth/me` requires a bearer access token and `User` role.
- `/auth/validate` requires a bearer access token and `User` role.

Registration creates an active normal user with confirmed email and `User` role,
which is acceptable for a disposable dev/test smoke user.

## Credential Strategy

Strategy:

- Generated a unique disposable dev/test user.
- Generated the password in memory.
- Did not print the test email.
- Did not print the password.
- Did not read bootstrap/admin credentials.
- Did not use admin endpoints.
- Stored payloads/responses only in an ephemeral temp directory with restrictive
  permissions.
- Deleted temp files at the end.

Run identifier recorded for traceability only:

- `20260705230310-b1219438`

The test password, access token, refresh token, Authorization header, JWT
claims, and response bodies are intentionally not recorded.

## Registration Result

- Executed: yes
- Endpoint: `POST /auth/register`
- HTTP status: `201`
- Response bytes: `195`
- API Gateway request ID: `none`
- Response body printed: no

## Login Result

- Executed: yes
- Endpoint: `POST /auth/login`
- HTTP status: `200`
- Response bytes: `1274`
- API Gateway request ID: `none`
- Access token extracted: yes
- Access token length: `1043`
- Refresh token extracted: yes
- Refresh token length: `86`
- Token value printed: no
- Response body printed: no

## Authenticated Route Smoke

All authenticated calls used the captured access token without printing it.

| Route | HTTP | Bytes | Request ID | Interpretation |
| --- | ---: | ---: | --- | --- |
| `GET /auth/me` | `200` | `289` | `none` | Authenticated user profile succeeded. |
| `GET /auth/validate` | `200` | `140` | `none` | Token validation succeeded. |
| `GET /shipments` | `200` | `56` | `none` | Protected shipment route authorized and reached backend. |
| `GET /tracking/00000000-0000-0000-0000-000000000000` | `400` | `89` | `none` | Protected tracking route authorized and reached backend validation path. |

No authenticated route returned `HTTP 000` or `5xx`.

## Refresh Test

- Executed: yes
- Endpoint: `POST /auth/refresh`
- HTTP status: `200`
- Response bytes: `1274`
- API Gateway request ID: `none`
- Refresh token value printed: no
- Response body printed: no

## No-Token Fail-Closed Validation

| Route | HTTP | Bytes |
| --- | ---: | ---: |
| `GET /auth/me` | `401` | `26` |
| `GET /auth/validate` | `401` | `26` |
| `GET /shipments` | `401` | `26` |
| `GET /tracking/00000000-0000-0000-0000-000000000000` | `401` | `26` |

Protected routes still failed closed without a token.

## Excluded Routes Validation

| Route | HTTP | Bytes | Result |
| --- | ---: | ---: | --- |
| `GET /auth/swagger/v1/swagger.json` | `404` | `23` | App swagger was not exposed. |
| `GET /shipments/swagger/v1/swagger.json` | `401` | `26` | App swagger was not publicly exposed. |
| `GET /tracking/swagger/v1/swagger.json` | `401` | `26` | App swagger was not publicly exposed. |
| `GET /health` | `404` | `23` | Root health was not exposed. |
| `GET /internal/health` | `404` | `23` | Internal route was not exposed. |
| `GET /admin/users` | `404` | `23` | Admin route was not exposed. |

No excluded route returned app swagger `200`, `HTTP 000`, or `5xx`.

## Logs

Logs inspected: no.

Reason: registration, login, refresh, authenticated routes, no-token validation,
and excluded-route validation completed without `HTTP 000` or `5xx`.

## Temporary Files

- Temp storage: ephemeral `/dev/shm/apigw-auth-smoke.*` path
- Cleanup: completed
- Token/password variables: unset after use

## Final Health

Runtime final:

- `platform-services` revision: `4`
- Deployments: `2/2`
- Pods: Running/Ready
- Ingress: present
- Kubernetes Secret check: metadata/key names and byte sizes only

Network final:

- VPC Link `c2au37`: `AVAILABLE`
- ALB `cloud-native-platform-dev`: active/internal
- Target groups: `ip` target type, `/health` health check
- Target health: 2 healthy targets per service

## Final Plans

Read-only plans:

- `infra/live/dev/apigateway-core`: `No changes`
- `infra/live/dev/apigateway-integration`: `No changes`
- `infra/live/dev/api-gateway-authorizer`: `No changes`

No apply was executed.

## Local Validations

Executed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh`
- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ...`
- `helm dependency build` for `cluster-addons`
- `helm lint` for `cluster-addons`
- `helm template cluster-addons ...`

All completed successfully. Helm reported only the existing informational
chart-icon recommendation and the existing cluster-addons templates directory
warning.

## Secret Review

Secret review was performed by listing matching files only, not matching lines or
values. Findings were limited to expected docs, IaC variable names, examples,
configuration keys, and source code references.

No test password, token header, JWT, claim, response body, connection string, or
Kubernetes Secret value was added to Git or printed intentionally.

## Limitations

- The disposable dev/test user remains in the dev auth database because there is
  no safe non-admin self-delete endpoint in scope.
- No response body content was recorded.
- No JWT claims were decoded or recorded.
- SG-to-SG frontend hardening from VPC Link SG to ALB frontend SG remains a
  future improvement; current dev posture uses internal ALB plus VPC CIDR
  inbound restriction.

## Recommendation

Proceed to `Fase 2.23 — API Gateway documentation and operational handoff`.

Recommended scope:

- Record final API Gateway endpoint, route contract, auth expectations, and known
  limitations.
- Add an operator runbook for safe smoke testing without printing tokens or
  bodies.
- Decide whether to add automated synthetic checks that redact headers and
  bodies.
- Track future SG-to-SG frontend hardening as a follow-up improvement.
