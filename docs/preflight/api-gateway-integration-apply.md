# API Gateway Integration Apply

Date: 2026-07-05 22:39 CST

Branch: `chore/apigateway-integration-apply`

AWS account: `145023118802`

## Objective

Apply only `infra/live/dev/apigateway-integration`, validate API Gateway routes and integrations, and run controlled API Gateway smoke without printing response bodies or sensitive values.

## Baseline Runtime

Management instance:

- Instance ID: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- VPC: `vpc-0fe33938202034387`

Kubernetes runtime:

- EKS cluster: `logistics-platform-dev`
- Helm release: `platform-services`
- Namespace: `apps`
- Revision: `4`
- Status: `deployed`
- Deployments:
  - `auth-service`: `2/2`
  - `shipment-service`: `2/2`
  - `tracking-service`: `2/2`
- Pods: Running/Ready
- Ingress: `platform-services`
- Ingress annotation: `alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16`

Kubernetes Secret inspection was limited to metadata, key names, and byte sizes. No values were read or decoded.

## Baseline ALB And SG

ALB:

- Name: `cloud-native-platform-dev`
- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:loadbalancer/app/cloud-native-platform-dev/feabb93df9e991e0`
- Listener HTTP/80 ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`
- Scheme: `internal`
- State: `active`

VPC Link SG:

- `sg-0a2f21b748db94d8b`
- Egress TCP/80 only to:
  - `sg-079c5a1e99c17270a`
  - `sg-0dcd35733de8447ba`
- No HTTP `0.0.0.0/0`
- No all-traffic egress

ALB frontend SG:

- `sg-079c5a1e99c17270a`
- TCP/80 from `10.0.0.0/16`
- TCP/80 from `0.0.0.0/0` absent

ALB target groups:

- Target type: `ip`
- Health check: `/health`
- Two healthy targets per app service

Baseline ALB smoke:

- `/auth/swagger/v1/swagger.json`: `200`
- `/shipments/swagger/v1/swagger.json`: `200`
- `/tracking/swagger/v1/swagger.json`: `200`
- `/auth/me`: `401`
- `/shipments`: `401`
- `/tracking/00000000-0000-0000-0000-000000000000`: `401`
- No `HTTP 000`
- No `5xx`

## API Gateway Core Pre-State

HTTP API:

- API ID: `diluedb2k7`
- Endpoint: `https://diluedb2k7.execute-api.us-east-1.amazonaws.com`
- Protocol: `HTTP`

Stage:

- `$default`
- Auto deploy enabled
- Before integration apply, the stage showed no valid app routes.

Authorizer:

- ID: `evdkk3`
- Type: `REQUEST`
- Payload format: `2.0`
- Lambda: `cloud-native-platform-dev-api-jwt-authorizer`
- Identity source: `$request.header.Authorization`

VPC Link:

- ID: `c2au37`
- Status: `AVAILABLE`
- SG: `sg-0a2f21b748db94d8b`
- Subnets:
  - `subnet-03aa254292f6017e8`
  - `subnet-0d50cde4bff9b5154`
  - `subnet-0ecdf9e460a352dc6`

Before integration apply:

- Routes: empty
- Integrations: empty

## Integration IaC Review

Route contract:

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

Excluded:

- `/internal/*`
- `/admin/*`
- Swagger
- Root `/health`

Integration configuration:

- Type: `HTTP_PROXY`
- Connection type: `VPC_LINK`
- VPC Link: `c2au37`
- Listener URI: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`
- Protected routes use authorizer `evdkk3`
- Public routes use authorization `NONE`

## Integration Plan

Plan result:

- `10 to add, 0 to change, 0 to destroy`

Resources:

- 2 API Gateway HTTP proxy integrations
- 8 API Gateway routes

Plan was clean:

- No destroy
- No replacements
- No SG changes
- No IAM changes
- No routes outside the contract
- No `/internal/*`
- No `/admin/*`
- No swagger
- No root `/health`
- No secrets

## Integration Apply

Apply executed:

- Stack: `infra/live/dev/apigateway-integration`
- Result: `10 added, 0 changed, 0 destroyed`

Created integrations:

- Public integration ID: `v71ida2`
- Protected integration ID: `25mwhm4`

Created route IDs:

- `POST /auth/login`: `aaqu9hc`
- `POST /auth/register`: `a4wbxc8`
- `POST /auth/refresh`: `3tuqud3`
- `GET /auth/me`: `ff3gwe7`
- `GET /auth/validate`: `rshro9o`
- `ANY /shipments`: `bmz29ok`
- `ANY /shipments/{proxy+}`: `xah3qdj`
- `ANY /tracking/{proxy+}`: `16ieghl`

Final integration plan:

- `No changes`

Outputs contained only non-secret identifiers and ARNs.

## API Gateway Validation

Routes after apply:

- `POST /auth/login`: `NONE`, target `integrations/v71ida2`
- `POST /auth/register`: `NONE`, target `integrations/v71ida2`
- `POST /auth/refresh`: `NONE`, target `integrations/v71ida2`
- `GET /auth/me`: `CUSTOM`, authorizer `evdkk3`, target `integrations/25mwhm4`
- `GET /auth/validate`: `CUSTOM`, authorizer `evdkk3`, target `integrations/25mwhm4`
- `ANY /shipments`: `CUSTOM`, authorizer `evdkk3`, target `integrations/25mwhm4`
- `ANY /shipments/{proxy+}`: `CUSTOM`, authorizer `evdkk3`, target `integrations/25mwhm4`
- `ANY /tracking/{proxy+}`: `CUSTOM`, authorizer `evdkk3`, target `integrations/25mwhm4`

Integrations after apply:

- `v71ida2`: `HTTP_PROXY`, `VPC_LINK`, connection `c2au37`, payload `1.0`
- `25mwhm4`: `HTTP_PROXY`, `VPC_LINK`, connection `c2au37`, payload `1.0`
- Both point to the HTTP/80 ALB listener ARN.

Stage:

- `$default`
- Auto deploy enabled
- Deployment status: successful after route creation

Authorizer:

- `evdkk3`
- Request authorizer backed by `cloud-native-platform-dev-api-jwt-authorizer`

No excluded app routes were created.

## API Gateway Smoke

Endpoint:

- `https://diluedb2k7.execute-api.us-east-1.amazonaws.com`

Only status codes and response sizes were recorded. No response bodies were printed.

Public routes with empty JSON payload:

- `POST /auth/login`: `400`, 340 bytes
- `POST /auth/register`: `400`, 518 bytes
- `POST /auth/refresh`: `400`, 258 bytes

Interpretation:

- Public routes reached the backend and returned validation errors.
- No `HTTP 000`
- No `5xx`
- No API Gateway missing-route `404`

Protected routes without token:

- `GET /auth/me`: `401`, 26 bytes
- `GET /auth/validate`: `401`, 26 bytes
- `GET /shipments`: `401`, 26 bytes
- `GET /tracking/00000000-0000-0000-0000-000000000000`: `401`, 26 bytes

Interpretation:

- Protected routes fail closed without a token.
- No `HTTP 000`
- No `5xx`

Excluded route probes:

- `GET /auth/swagger/v1/swagger.json`: `404`, 23 bytes
- `GET /shipments/swagger/v1/swagger.json`: `401`, 26 bytes
- `GET /tracking/swagger/v1/swagger.json`: `401`, 26 bytes
- `GET /health`: `404`, 23 bytes
- `GET /internal/health`: `404`, 23 bytes
- `GET /admin/users`: `404`, 23 bytes

Interpretation:

- Swagger was not exposed as app swagger `200`.
- Root `/health`, `/internal/*`, and `/admin/*` were not exposed.
- Shipment/tracking swagger probes match protected proxy behavior and do not expose swagger content.
- No `5xx`

Optional invalid-token Lambda authorizer test was skipped because the route smoke already proved protected routes fail closed without using or printing any token.

## VPC Link And ALB Health After

VPC Link:

- `c2au37`
- Status: `AVAILABLE`

ALB:

- `cloud-native-platform-dev`
- State: `active`

Target groups:

- Auth target group: 2 healthy targets
- Shipment target group: 2 healthy targets
- Tracking target group: 2 healthy targets

## Runtime Final

Kubernetes runtime after API Gateway integration apply:

- `platform-services` revision `4`
- Status: `deployed`
- Deployments: `2/2`
- Pods: Running/Ready
- Ingress present
- Kubernetes Secret inspected only as metadata/key names and byte sizes; no values were read or decoded.

## Final Plans

`apigateway-core`:

- `No changes`

`apigateway-integration`:

- `No changes`

## Local Validations

Executed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh || true`
- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ...`
- `helm dependency build` for `cluster-addons`
- `helm lint` for `cluster-addons`
- `helm template` for `cluster-addons`

All blocking validations passed.

## Secret Review

No `.env`, `.pem`, `.key`, or `.tfvars` files were found.

Sensitive-pattern review was summarized by matching file only. Matches were expected configuration/reference locations. No secret values were printed.

This phase did not:

- Read or decode Kubernetes Secret values
- Run `aws secretsmanager get-secret-value`
- Run `aws secretsmanager put-secret-value`
- Run `aws ssm get-parameter --with-decryption`
- Print Secrets Manager value fields
- Print auth tokens
- Print response bodies

## Limitations

- Full token-auth happy path was not executed because safe test credentials/tokens were not available in scope.
- SG-to-SG ALB frontend hardening from the VPC Link SG remains a future improvement. Dev currently uses internal ALB plus VPC CIDR inbound hardening.

## Recommendation

Proceed to:

```text
Fase 2.22 - API Gateway authenticated path validation and evidence
```

Recommended scope:

- Decide a safe credential strategy for end-to-end token validation.
- Avoid printing credentials, tokens, or response bodies.
- Exercise `POST /auth/login` with approved non-sensitive test credentials or create an approved temporary test user.
- Validate `GET /auth/me`, `/shipments`, and `/tracking/{id}` through API Gateway with a real token.
- Inspect API Gateway/Lambda logs only for status and request IDs, not secrets.
- Keep SG-to-SG frontend hardening as a separate optional improvement.
