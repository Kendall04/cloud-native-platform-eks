# API Gateway Core and VPC Link Readiness Evidence

Date: 2026-07-05 19:08:31 CST
Branch: `chore/apigateway-core-vpc-link-ready`
AWS account: `145023118802`
Region: `us-east-1`

## Objective

Apply only `apigateway-core`, validate the HTTP API, `$default` stage, Lambda authorizer wiring, VPC Link, and VPC Link security group, then determine readiness for a later `apigateway-integration` apply.

## Baseline Runtime and ALB

Baseline EKS state before `apigateway-core` apply:

- Cluster: `logistics-platform-dev`
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
- Kubernetes Secret validation: metadata and key names only

Baseline ALB smoke from management EC2:

| Path | HTTP | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/auth/swagger/v1/swagger.json` | 200 | 12819 | Auth service reachable |
| `/shipments/swagger/v1/swagger.json` | 200 | 10618 | Shipment service reachable |
| `/tracking/swagger/v1/swagger.json` | 200 | 6817 | Tracking service reachable |
| `/auth/me` | 401 | 0 | Protected route reachable, token required |
| `/shipments` | 401 | 0 | Protected route reachable, token required |
| `/tracking/00000000-0000-0000-0000-000000000000` | 401 | 0 | Protected route reachable, token required |

No `HTTP 000` and no `5xx` were observed.

## Authorizer Pre-State

Existing Lambda authorizer:

- Function: `cloud-native-platform-dev-api-jwt-authorizer`
- Runtime: `nodejs22.x`
- Handler: `src/index.handler`
- State: `Active`
- Last update status: `Successful`
- Log group: `/aws/lambda/cloud-native-platform-dev-api-jwt-authorizer`
- Retention: `30`

Environment validation listed only keys:

- `JWT_AUDIENCE`
- `AUTH_SERVICE_JWT_SECRET_ID`
- `JWT_ISSUER`
- `PLATFORM_TRUSTED_PROXY_SECRET_ID`
- `SECRET_CACHE_TTL_SECONDS`

No environment values were printed.

## API Gateway and VPC Link Pre-State

Before `apigateway-core` apply:

- No project HTTP API existed.
- No project VPC Link existed.
- Lambda authorizer existed.

## Core IaC Review

Reviewed:

- `infra/live/dev/apigateway-core`
- `infra/modules/apigateway-core`

Expected ownership:

- HTTP API
- `$default` stage
- CloudWatch access log group
- Lambda authorizer binding
- Lambda invoke permission for API Gateway
- VPC Link
- VPC Link security group
- VPC Link SG egress TCP/80 to internal ALB SGs

Integration stack ownership remains separate:

- ALB HTTP proxy integrations
- API Gateway routes

The core module does not create ALB integration routes.

## Core Plan

Stack:

- `infra/live/dev/apigateway-core`

Plan result:

- `9 to add, 0 to change, 0 to destroy`

Planned resources:

- HTTP API: `cloud-native-platform-dev-http-api`
- `$default` stage
- API Gateway log group: `/aws/apigateway/cloud-native-platform-dev-http-api`
- REQUEST Lambda authorizer
- Lambda permission for API Gateway to invoke the authorizer
- VPC Link: `cloud-native-platform-dev-http-api-vpc-link`
- VPC Link security group
- Two VPC Link egress rules:
  - TCP/80 to `sg-079c5a1e99c17270a`
  - TCP/80 to `sg-0dcd35733de8447ba`

Plan review:

- No destroy.
- No replacement.
- No ALB integration routes.
- No broad VPC Link HTTP egress.
- No VPC Link all-traffic egress.
- No endpoint changes.
- No Kubernetes changes.

## Core Apply

Apply result:

- Executed only for `infra/live/dev/apigateway-core`.
- `9 added, 0 changed, 0 destroyed`.
- Final plan: `No changes`.

Non-secret outputs:

| Output | Value |
| --- | --- |
| API ID | `diluedb2k7` |
| API endpoint | `https://diluedb2k7.execute-api.us-east-1.amazonaws.com` |
| Execution ARN | `arn:aws:execute-api:us-east-1:145023118802:diluedb2k7` |
| Stage name | `$default` |
| Stage invoke URL | `https://diluedb2k7.execute-api.us-east-1.amazonaws.com` |
| Authorizer ID | `evdkk3` |
| VPC Link ID | `c2au37` |
| VPC Link SG ID | `sg-0a2f21b748db94d8b` |

## API Gateway Validation

HTTP API:

- Name: `cloud-native-platform-dev-http-api`
- API ID: `diluedb2k7`
- Endpoint: `https://diluedb2k7.execute-api.us-east-1.amazonaws.com`
- Protocol: `HTTP`

Stage:

- Stage: `$default`
- Auto deploy: `true`
- Access logs: configured
- Default route settings:
  - detailed metrics enabled
  - throttling burst `100`
  - throttling rate `50`

Stage note:

- `LastDeploymentStatusMessage` reports that deployment could not deploy valid routes because no routes exist yet.
- This is expected in this phase because `apigateway-integration` has not been applied.

Authorizer:

- Authorizer ID: `evdkk3`
- Type: `REQUEST`
- Payload format: `2.0`
- Simple responses: enabled
- Identity source: `$request.header.Authorization`
- Authorizer URI points to `cloud-native-platform-dev-api-jwt-authorizer`.

Routes and integrations:

- Routes: empty
- Integrations: empty

This matches the intended split: core owns the API shell and authorizer; integration owns routes and ALB proxy integrations.

## VPC Link Validation

VPC Link:

- Name: `cloud-native-platform-dev-http-api-vpc-link`
- ID: `c2au37`
- Status: `AVAILABLE`
- Security group: `sg-0a2f21b748db94d8b`
- Subnets:
  - `subnet-03aa254292f6017e8`
  - `subnet-0d50cde4bff9b5154`
  - `subnet-0ecdf9e460a352dc6`

## VPC Link SG Validation

Security group:

- ID: `sg-0a2f21b748db94d8b`
- Name: `cloud-native-platform-dev-http-api-vpc-link`
- VPC: `vpc-0fe33938202034387`

Ingress:

- No ingress rules.

Egress:

- TCP/80 to `sg-079c5a1e99c17270a`
- TCP/80 to `sg-0dcd35733de8447ba`

Broad egress validation:

- No HTTP egress to `0.0.0.0/0`.
- No all-traffic egress.

## ALB SG Ingress From VPC Link SG

ALB SGs:

- `sg-079c5a1e99c17270a`
- `sg-0dcd35733de8447ba`

Observed ingress:

- `sg-079c5a1e99c17270a` allows TCP/80 from `0.0.0.0/0`.
- `sg-0dcd35733de8447ba` has no ingress rules.
- No explicit TCP/80 ingress rule from `sg-0a2f21b748db94d8b` was present on either ALB SG.

Readiness classification:

- Functionally, the current ALB SG configuration allows HTTP/80 to the ALB through the broad managed ALB SG ingress.
- For least-privilege integration readiness, explicit ALB ingress from the VPC Link SG is absent.
- Treat this as a hardening gap before `apigateway-integration` apply.

No ALB SG changes were made in this phase.

## ALB, Listener, and Target Groups

ALB:

- Name: `cloud-native-platform-dev`
- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:loadbalancer/app/cloud-native-platform-dev/feabb93df9e991e0`
- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- Scheme: `internal`
- State: `active`
- Type: `application`
- VPC: `vpc-0fe33938202034387`

Listener:

- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`
- Protocol: `HTTP`
- Port: `80`

Target groups:

| Target group | Protocol | Target type | Health check |
| --- | --- | --- | --- |
| `k8s-apps-authserv-58a4b90ba7` | HTTP | ip | `/health` |
| `k8s-apps-shipment-fb67f1d90f` | HTTP | ip | `/health` |
| `k8s-apps-tracking-9810f2e09e` | HTTP | ip | `/health` |

Target health:

- Auth target group: 2 healthy targets
- Shipment target group: 2 healthy targets
- Tracking target group: 2 healthy targets

## ALB Smoke Post-Core

Post-core smoke from management EC2:

| Path | HTTP | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/auth/swagger/v1/swagger.json` | 200 | 12819 | Auth service reachable |
| `/shipments/swagger/v1/swagger.json` | 200 | 10618 | Shipment service reachable |
| `/tracking/swagger/v1/swagger.json` | 200 | 6817 | Tracking service reachable |
| `/auth/me` | 401 | 0 | Protected route reachable, token required |
| `/shipments` | 401 | 0 | Protected route reachable, token required |
| `/tracking/00000000-0000-0000-0000-000000000000` | 401 | 0 | Protected route reachable, token required |

No `HTTP 000` and no `5xx` were observed.

## Final Runtime State

Final EKS state:

- Helm release: `platform-services`
- Namespace: `apps`
- Release status: `deployed`
- Release revision: `3`
- `auth-service`: `2/2`
- `shipment-service`: `2/2`
- `tracking-service`: `2/2`
- Pods: Running/Ready
- Ingress: present

## Integration Plan After Core

Stack:

- `infra/live/dev/apigateway-integration`

Plan result:

- `10 to add, 0 to change, 0 to destroy`
- No apply.

The plan now uses real core outputs:

- API ID: `diluedb2k7`
- VPC Link ID: `c2au37`
- Authorizer ID: `evdkk3`
- ALB listener ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`

Planned route contract:

| Route | Auth |
| --- | --- |
| `POST /auth/login` | none |
| `POST /auth/register` | none |
| `POST /auth/refresh` | none |
| `GET /auth/me` | custom authorizer |
| `GET /auth/validate` | custom authorizer |
| `ANY /shipments` | custom authorizer |
| `ANY /shipments/{proxy+}` | custom authorizer |
| `ANY /tracking/{proxy+}` | custom authorizer |

Excluded:

- `/internal/*`
- `/admin/*`
- swagger routes
- root `/health`

Integration apply status:

- Skipped.
- Reason: integration is out of scope for this phase, and explicit ALB SG ingress from the VPC Link SG still needs to be modeled or accepted as a deliberate security decision.

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

- `apigateway-integration` has not been applied.
- API Gateway has no routes/integrations yet.
- `$default` stage exists but has no deployable routes until integration is applied.
- Explicit ALB SG ingress from VPC Link SG is absent.
- Decide whether to:
  - model a least-privilege ALB ingress rule from `sg-0a2f21b748db94d8b`, or
  - explicitly accept the current broad ALB managed SG ingress for dev.

## Recommendation

Proceed to `Fase 2.19 — ALB ingress hardening for API Gateway VPC Link and integration readiness`.

Recommended scope:

- Resolve ALB SG ingress from VPC Link SG before applying integration.
- Prefer a least-privilege TCP/80 rule from `sg-0a2f21b748db94d8b` to the ALB path if there is a clean IaC mechanism.
- Avoid manual mutation of AWS Load Balancer Controller managed SGs unless explicitly accepted.
- Re-plan `apigateway-integration` with real core outputs.
- Apply `apigateway-integration` only after the VPC Link-to-ALB SG path is intentionally approved.
