# API Gateway Plan Hardening

Date: 2026-07-05

Branch: `chore/apigateway-plan-hardening`

AWS account: `145023118802`

## Objective

Harden API Gateway plans before any API Gateway or Lambda resources are applied.

This phase updates the IaC plan shape for:

- Lambda authorizer planning
- API Gateway HTTP API core
- API Gateway VPC Link security group egress
- API Gateway integration to the internal ALB
- Public/protected route definitions

No Terraform or Terragrunt apply was run.

## Runtime state

Runtime was validated read-only before the plan hardening work.

Current state:

- EKS cluster: `logistics-platform-dev`
- Namespace: `apps`
- Helm release: `platform-services`
- Release status: `deployed`
- Release revision: `3`
- `auth-service`: deployment `2/2`, pods Running/Ready
- `shipment-service`: deployment `2/2`, pods Running/Ready
- `tracking-service`: deployment `2/2`, pods Running/Ready
- Services and Ingress are present.
- Secret `platform-runtime-secrets` exists with the expected key names.

Only Secret metadata and key names were validated. Secret values were not read,
decoded, printed, or committed.

## Real ALB

ALB:

- Name: `cloud-native-platform-dev`
- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:loadbalancer/app/cloud-native-platform-dev/feabb93df9e991e0`
- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- Scheme: `internal`
- State: `active`
- Type: `application`
- VPC: `vpc-0fe33938202034387`

ALB security groups:

- `sg-079c5a1e99c17270a`
- `sg-0dcd35733de8447ba`

Listener:

- Protocol: HTTP
- Port: `80`
- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`

Real AWS Load Balancer Controller tags:

- `ingress.k8s.aws/resource = LoadBalancer`
- `ingress.k8s.aws/stack = cloud-native-platform`
- `elbv2.k8s.aws/cluster = logistics-platform-dev`

## Previous ALB discovery problem

`apigateway-integration` previously attempted to discover the ALB with tags that
were not present on the controller-managed ALB:

- `kubernetes.io/namespace = apps`
- `kubernetes.io/ingress-name = platform-services`

That made the read-only integration plan fail before producing a resource plan.

## ALB discovery hardening

Changed:

- `infra/modules/apigateway-integration/main.tf`
- `infra/modules/apigateway-integration/variables.tf`
- `infra/modules/apigateway-integration/outputs.tf`
- `infra/live/dev/apigateway-integration/terragrunt.hcl`

Implemented:

- Optional explicit `alb_listener_arn`.
- Optional explicit `alb_arn`.
- Tag/name discovery remains available as fallback.
- When `alb_listener_arn` is provided, listener discovery is skipped.
- Dev now passes the known internal ALB ARN and HTTP/80 listener ARN.
- Dev tag fallback was updated to the real AWS Load Balancer Controller tags.

Tradeoff:

- Explicit listener ARN gives a stable plan now and removes fragile tag lookup.
- If the ALB is recreated, the listener ARN must be updated or replaced with a
  working dynamic lookup.

## VPC Link SG egress hardening

Changed:

- `infra/modules/apigateway-core/main.tf`
- `infra/modules/apigateway-core/variables.tf`
- `infra/modules/apigateway-core/outputs.tf`
- `infra/live/dev/apigateway-core/terragrunt.hcl`

Before:

- The VPC Link security group planned all-protocol egress to `0.0.0.0/0`.

After:

- The VPC Link security group plans only TCP/80 egress to the internal ALB
  security groups:
  - `sg-079c5a1e99c17270a`
  - `sg-0dcd35733de8447ba`

Confirmed by plan:

- No VPC Link HTTP egress to `0.0.0.0/0`.
- No all-traffic VPC Link egress.
- Two scoped egress rules are planned.

The module now also outputs `vpc_link_security_group_id` so later phases can use
the future VPC Link SG as a source for ALB ingress if a clean ownership path is
chosen.

## ALB SG ingress from VPC Link SG

Current ALB SG state is managed by AWS Load Balancer Controller.

Observed:

- `sg-079c5a1e99c17270a` is the LBC managed load balancer security group.
- `sg-0dcd35733de8447ba` is the LBC shared backend security group.
- The managed load balancer security group currently allows TCP/80 ingress from
  `0.0.0.0/0`.

Decision:

- Do not add Terraform-managed ALB SG ingress rules in this phase.

Reason:

- The ALB security groups are controller-managed.
- This phase is plan hardening only.
- Adding rules to LBC-managed security groups should be validated in the apply
  phase with explicit ownership expectations.

Prepared:

- `apigateway-core` now exposes the future VPC Link SG ID.
- Follow-up can either:
  - configure LBC/Ingress annotations for controlled SG ownership, or
  - add a carefully scoped Terraform-managed ingress rule if ownership is
    accepted.

Remaining gap:

- Validate and harden ALB SG ingress from the future VPC Link SG before applying
  `apigateway-integration`.

## Route contract hardening

Changed:

- `infra/live/dev/apigateway-integration/terragrunt.hcl`

Public routes now planned:

- `POST /auth/login`
- `POST /auth/register`
- `POST /auth/refresh`

Protected routes now planned:

- `GET /auth/me`
- `GET /auth/validate`
- `ANY /shipments`
- `ANY /shipments/{proxy+}`
- `ANY /tracking/{proxy+}`

Excluded:

- `/internal/*`
- `/admin/*`
- root `/health`
- broad public `ANY /auth`
- broad public `ANY /auth/{proxy+}`
- swagger routes

Swagger decision:

- Swagger is excluded from the first API Gateway route plan.
- It can be added later as explicit temporary/demo `GET` routes if needed.

Admin decision:

- Admin routes are excluded from the first API Gateway route plan.
- They should be added only in a phase that explicitly validates admin auth and
  role expectations.

## Authorizer plan

Stack:

- `infra/live/dev/api-gateway-authorizer`

Plan command used non-secret local placeholders for the two required secret
environment variables so no real values were read or printed.

Plan result:

- `4 to add`
- `0 to change`
- `0 to destroy`

Expected resources:

- Lambda log group
- Lambda IAM role
- Lambda basic execution policy attachment
- Lambda function

No apply was run.

Dependency:

- This stack should be applied before `apigateway-core` so core can consume real
  Lambda outputs instead of plan mocks.

## API Gateway core plan

Stack:

- `infra/live/dev/apigateway-core`

Plan result:

- `9 to add`
- `0 to change`
- `0 to destroy`

Expected resources:

- HTTP API
- `$default` stage
- API Gateway VPC Link
- VPC Link security group
- two scoped VPC Link egress rules to ALB SGs
- CloudWatch access log group
- REQUEST authorizer
- Lambda invoke permission

Confirmed:

- No broad VPC Link egress to `0.0.0.0/0`.
- No all-traffic VPC Link egress.
- No destroys.
- No replacements.

Planning note:

- Because `api-gateway-authorizer` is not applied yet, the core plan used
  Terragrunt mock outputs for the authorizer. After the authorizer stack is
  applied, core should be planned again with real outputs.

## API Gateway integration plan

Stack:

- `infra/live/dev/apigateway-integration`

Plan result:

- `10 to add`
- `0 to change`
- `0 to destroy`

Expected resources:

- Public HTTP proxy integration to the internal ALB listener
- Protected HTTP proxy integration to the internal ALB listener
- 3 public auth routes
- 5 protected app routes

Confirmed:

- Plan no longer fails on ALB discovery.
- Integration URI is the real ALB HTTP/80 listener ARN.
- Public routes have `authorization_type = NONE`.
- Protected routes have `authorization_type = CUSTOM`.
- No `/internal/*` routes are planned.
- No admin routes are planned.
- No root `/health` route is planned.
- No swagger routes are planned.
- No destroys.
- No replacements.

Planning note:

- Because `apigateway-core` is not applied yet, this plan used Terragrunt mock
  API Gateway outputs for `api_id`, `vpc_link_id`, and `authorizer_id`. After
  core is applied, integration should be planned again with real outputs.

## Runtime smoke

Read-only smoke against the internal ALB after plan hardening:

| Path | HTTP status | Interpretation |
| --- | --- | --- |
| `/auth/swagger/v1/swagger.json` | `200` | Auth service reachable |
| `/shipments/swagger/v1/swagger.json` | `200` | Shipment service reachable |
| `/tracking/swagger/v1/swagger.json` | `200` | Tracking service reachable |
| `/auth/me` | `401` | Protected route reachable without token |
| `/shipments` | `401` | Protected route reachable without token |
| `/tracking/00000000-0000-0000-0000-000000000000` | `401` | Protected route reachable without token |

No `HTTP 000` and no `5xx` were observed.

No response bodies were recorded.

## API Gateway and Lambda state

Read-only AWS checks returned no project API Gateway APIs and no project Lambda
functions.

Not applied:

- `api-gateway-authorizer`
- `apigateway-core`
- `apigateway-integration`
- API Gateway resources
- Lambda authorizer

## Validation

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

- Required validations passed.
- Helm lint reported only the existing chart icon recommendation.

## Secret review

Checked for local sensitive file extensions:

- `*.env`
- `*.pem`
- `*.key`
- `*.tfvars`

Result:

- No matching files were found.

Pattern scan result:

- Matches were expected code/config references such as environment variable
  names, placeholder/runtime key names, AWS SDK references, and service
  configuration identifiers.
- No secret values were identified or printed.

## Evidence

Local evidence files:

- `/tmp/cloud-native-platform-apigw-hardening/alb.json`
- `/tmp/cloud-native-platform-apigw-hardening/alb-tags.json`
- `/tmp/cloud-native-platform-apigw-hardening/alb-listeners.json`
- `/tmp/cloud-native-platform-apigw-hardening/alb-rules.json`
- `/tmp/cloud-native-platform-apigw-hardening/authorizer-init.log`
- `/tmp/cloud-native-platform-apigw-hardening/authorizer-plan.log`
- `/tmp/cloud-native-platform-apigw-hardening/apigateway-core-init.log`
- `/tmp/cloud-native-platform-apigw-hardening/apigateway-core-plan.log`
- `/tmp/cloud-native-platform-apigw-hardening/apigateway-integration-init.log`
- `/tmp/cloud-native-platform-apigw-hardening/apigateway-integration-plan.log`
- `/tmp/platform-services-dev-apigw-hardening.yaml`
- `/tmp/cluster-addons.yaml`

SSM command IDs:

- Runtime state: `9b93fb1d-58e9-4648-9900-70450905d9b8`
- Runtime smoke: `ff1bedd6-5eab-4992-81c4-b1a3e87fc979`

## Remaining blockers and gaps

Before apply:

1. Re-plan `api-gateway-authorizer` with real secret environment variables
   available in the execution environment.
2. Apply order should be authorizer first, then core, then integration.
3. Re-plan `apigateway-core` after authorizer outputs exist.
4. Re-plan `apigateway-integration` after core outputs exist.
5. Validate and harden ALB SG ingress from the real VPC Link SG.
6. Decide whether Swagger should remain excluded or be temporarily exposed later.
7. Decide whether admin routes should remain excluded or be added in a dedicated
   admin validation phase.

## No apply confirmation

No Terraform/Terragrunt apply was run.

No API Gateway, Lambda, Helm, Kubernetes, Docker, Secret, SG, IAM, or endpoint
resource was changed in AWS/Kubernetes.

## Recommended next phase

`Fase 2.14 — API Gateway authorizer/core apply readiness`

Recommended scope:

1. Provide real authorizer secret inputs through the approved private mechanism.
2. Plan `api-gateway-authorizer` with real inputs.
3. If clean, apply only `api-gateway-authorizer`.
4. Re-plan `apigateway-core` using real authorizer outputs.
5. Validate VPC Link SG egress remains scoped to ALB SGs.
6. Decide and implement ALB SG ingress hardening from the future VPC Link SG.
7. Do not apply `apigateway-integration` until core exists and integration has a
   clean plan with real outputs.
