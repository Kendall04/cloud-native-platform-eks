# API Gateway Authorizer/Core Apply Readiness

Date: 2026-07-05

Branch: `chore/apigateway-authorizer-core-apply`

AWS account: `145023118802`

## Objective

Validate whether the API Gateway Lambda authorizer and API Gateway core stacks are ready for controlled apply.

Approved apply scope for this phase was limited to:

- `infra/live/dev/api-gateway-authorizer`
- `infra/live/dev/apigateway-core`

`infra/live/dev/apigateway-integration` remained out of scope for apply.

## Runtime State

The existing runtime was validated read-only before any apply decision:

- EKS cluster: `logistics-platform-dev`
- Helm release: `platform-services`
- namespace: `apps`
- status: `deployed`
- revision: `3`
- deployments: `auth-service`, `shipment-service`, `tracking-service`
- deployment availability: `2/2` for all three services
- pods: Running/Ready
- services and ingress: present
- internal ALB: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`

The Kubernetes Secret `platform-runtime-secrets` was checked only by metadata and key names. No values were printed or decoded.

Validated key names:

- `auth-bootstrap-admin-email`
- `auth-bootstrap-admin-password`
- `auth-connection-string`
- `auth-jwt-secret`
- `platform-internal-service-secret`
- `platform-trusted-proxy-secret`
- `shipment-connection-string`
- `tracking-connection-string`

## ALB Smoke

Read-only smoke tests from the management EC2 to the internal ALB returned:

| Path | Status | Interpretation |
| --- | ---: | --- |
| `/auth/swagger/v1/swagger.json` | `200` | Auth backend reachable |
| `/shipments/swagger/v1/swagger.json` | `200` | Shipment backend reachable |
| `/tracking/swagger/v1/swagger.json` | `200` | Tracking backend reachable |
| `/auth/me` | `401` | Protected route reachable without token |
| `/shipments` | `401` | Protected route reachable without token |
| `/tracking/00000000-0000-0000-0000-000000000000` | `401` | Protected route reachable without token |

No `HTTP 000` and no `5xx` responses were observed.

## Authorizer Input Review

`infra/live/dev/api-gateway-authorizer/terragrunt.hcl` configures the Lambda authorizer with:

- `JWT_ISSUER`
- `JWT_AUDIENCE`
- `JWT_SECRET`
- `PLATFORM_TRUSTED_PROXY_SECRET`

`JWT_ISSUER` and `JWT_AUDIENCE` are non-secret environment constants.

`JWT_SECRET` is sourced from the deployment-time environment variable `AUTH_SERVICE_JWT_SECRET`.

`PLATFORM_TRUSTED_PROXY_SECRET` is sourced from the deployment-time environment variable `PLATFORM_TRUSTED_PROXY_SECRET`.

Both secret inputs are required by the Lambda source. The current local environment did not have those deployment-time variables set.

The shared Lambda Terraform module currently accepts `environment_variables` as a normal `map(string)` and passes it directly to `aws_lambda_function.environment`. Because those inputs are not modeled as sensitive values, planning with real secret values may expose them in local plan output or logs.

## Authorizer Source Review

Authorizer source exists at:

- `lambdas/api-gateway-jwt-authorizer/src/index.mjs`
- `lambdas/api-gateway-jwt-authorizer/package.json`

Runtime and handler configured by IaC:

- runtime: `nodejs22.x`
- handler: `src/index.handler`

The authorizer validates HS256 JWTs, issuer, audience, signature, `nbf`, and `exp`, and returns API Gateway authorizer context. It also forwards the trusted proxy secret through authorizer context for backend trust-boundary headers.

No secret values were found in source.

## Authorizer Plan Result

The authorizer plan was intentionally attempted without injecting placeholder or real secret values.

Result:

- plan did not produce Terraform resource changes
- Terragrunt failed before Terraform planning because required deployment-time environment variables were absent

Required missing inputs:

- `AUTH_SERVICE_JWT_SECRET`
- `PLATFORM_TRUSTED_PROXY_SECRET`

Evidence:

- `/tmp/cloud-native-platform-apigw-apply-readiness/authorizer-init.log`
- `/tmp/cloud-native-platform-apigw-apply-readiness/authorizer-plan.log`

Decision:

- authorizer apply was skipped
- no placeholders were used
- no secret values were read from Kubernetes or AWS secret sources
- no secret values were printed

## Authorizer Apply Result

Authorizer apply was not executed.

Reason:

- the authorizer requires real secret material
- required deployment-time secret env vars were not present
- the module does not currently prevent plan/log exposure of those env var values

No Lambda authorizer resources were created.

## Lambda Validation

Read-only AWS validation after the skipped apply confirmed no project API Gateway authorizer Lambda was created.

No Lambda environment values were queried or printed.

## Core Plan and Apply

`apigateway-core` depends on real authorizer outputs:

- Lambda function name
- Lambda invoke ARN

Because the authorizer was not applied, real authorizer outputs do not exist.

Core plan with real outputs was skipped.

Core apply was not executed.

No HTTP API, stage, VPC Link, VPC Link security group, API Gateway authorizer, or Lambda permission was created in this phase.

## API Gateway Validation

Read-only AWS validation after the skipped apply confirmed:

- no project HTTP API exists
- no project Lambda authorizer exists

`apigateway-integration` was not applied.

## VPC Link SG / ALB SG Readiness

Because `apigateway-core` was not applied, no real VPC Link security group exists yet.

The intended core plan from the previous hardening phase remains:

- VPC Link SG egress TCP/80 to ALB SGs only
- no HTTP egress to `0.0.0.0/0`
- no all-traffic egress

Remaining blocker before `apigateway-integration apply`:

- validate or model ALB SG ingress TCP/80 from the future VPC Link SG

The ALB SGs are managed by AWS Load Balancer Controller, so ingress should not be forced without an explicit, clean ownership model.

## ALB Target Health

Target groups remained healthy:

- auth target group: 2 healthy targets
- shipment target group: 2 healthy targets
- tracking target group: 2 healthy targets

## Final EKS Runtime

Final read-only runtime validation confirmed:

- `platform-services` remains `deployed`
- revision remains `3`
- deployments remain `2/2`
- pods remain Running/Ready
- ingress remains present

No Kubernetes resources were modified.

## Local Validations

Completed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh`
- `helm lint` for `platform-services`
- `helm template` for `platform-services`
- `helm dependency build` for `cluster-addons`
- `helm lint` for `cluster-addons`
- `helm template` for `cluster-addons`

## Secret Review

No `.env`, `.pem`, `.key`, or `.tfvars` files were found.

The repository secret-pattern scan returned references in expected code/config files. Matches were summarized by file only to avoid printing any potential sensitive values.

No secret values were added to documentation.

## Not Applied

This phase did not execute:

- `terragrunt run-all apply`
- `apigateway-integration` apply
- Helm install/upgrade
- `kubectl apply`
- `kubectl create`
- `kubectl delete`
- Docker build/push
- Kubernetes Secret reads/decodes
- EKS public endpoint changes
- Git push

## Blockers

Blocker for authorizer/core apply:

- the authorizer requires `AUTH_SERVICE_JWT_SECRET` and `PLATFORM_TRUSTED_PROXY_SECRET`
- those values were not available as safe deployment-time inputs
- the Lambda module does not currently model environment variables as sensitive, so planning with real values could expose them

## Recommendation

Do not proceed to API Gateway integration apply yet.

Recommended next phase:

`Fase 2.15 - Secure API Gateway authorizer secret injection plan`

Scope:

- decide the approved source for authorizer secret material
- avoid printing or committing secret values
- update IaC so authorizer secret inputs are modeled as sensitive
- consider using Secrets Manager or encrypted deployment inputs if appropriate
- obtain a clean authorizer plan without exposing secret values
- apply `api-gateway-authorizer` only after inputs are safe
- apply `apigateway-core` only after real authorizer outputs exist
- keep `apigateway-integration` out of scope until VPC Link to ALB SG ingress is resolved
