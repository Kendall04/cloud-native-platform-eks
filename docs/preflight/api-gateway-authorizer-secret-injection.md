# API Gateway Authorizer Secret Injection Plan

Date: 2026-07-05

Branch: `chore/apigateway-authorizer-secret-plan`

AWS account: `145023118802`

## Objective

Design and implement a safe authorizer secret injection path without applying API Gateway authorizer, core, or integration stacks.

This phase was plan-only for API Gateway infrastructure.

## Runtime / ALB Baseline

The existing platform runtime was validated read-only:

- EKS cluster: `logistics-platform-dev`
- Helm release: `platform-services`
- namespace: `apps`
- status: `deployed`
- revision: `3`
- deployments: `auth-service`, `shipment-service`, `tracking-service`
- deployment availability: `2/2`
- pods: Running/Ready
- services and ingress: present
- internal ALB: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`

The Kubernetes Secret `platform-runtime-secrets` was checked only by metadata and key names. No values were printed, decoded, or copied.

Validated key names:

- `auth-bootstrap-admin-email`
- `auth-bootstrap-admin-password`
- `auth-connection-string`
- `auth-jwt-secret`
- `platform-internal-service-secret`
- `platform-trusted-proxy-secret`
- `shipment-connection-string`
- `tracking-connection-string`

ALB smoke from the management EC2 returned:

| Path | Status | Interpretation |
| --- | ---: | --- |
| `/auth/swagger/v1/swagger.json` | `200` | Auth backend reachable |
| `/shipments/swagger/v1/swagger.json` | `200` | Shipment backend reachable |
| `/tracking/swagger/v1/swagger.json` | `200` | Tracking backend reachable |
| `/auth/me` | `401` | Protected route reachable without token |
| `/shipments` | `401` | Protected route reachable without token |
| `/tracking/00000000-0000-0000-0000-000000000000` | `401` | Protected route reachable without token |

No `HTTP 000` and no `5xx` responses were observed.

## Original Blocker

The Lambda authorizer previously required raw secret values as Lambda environment variables:

- `AUTH_SERVICE_JWT_SECRET`
- `PLATFORM_TRUSTED_PROXY_SECRET`

Those values were not available as safe deployment-time inputs. Passing them through normal Terraform variables or Lambda environment values could expose them in plan output, logs, or state.

The previous phase correctly stopped before apply.

## Authorizer Source Review

Source:

- `lambdas/api-gateway-jwt-authorizer/src/index.mjs`
- `lambdas/api-gateway-jwt-authorizer/package.json`
- `lambdas/api-gateway-jwt-authorizer/README.md`

Runtime:

- `nodejs22.x`

Handler:

- `src/index.handler`

Behavior:

- validates `Authorization: Bearer <token>`
- validates HS256 JWT issuer, audience, signature, `nbf`, and `exp`
- returns simple API Gateway authorizer context
- forwards a trusted proxy secret in authorizer context for backend trust headers

Logging risk review:

- secret values are not logged
- denied requests log only sanitized error metadata
- source does not contain secret values

## Strategy Chosen

Chosen strategy: **AWS Secrets Manager runtime retrieval**.

Design:

- Terraform passes only non-secret secret IDs to Lambda env vars:
  - `AUTH_SERVICE_JWT_SECRET_ID`
  - `PLATFORM_TRUSTED_PROXY_SECRET_ID`
- Lambda retrieves secret values at runtime with `secretsmanager:GetSecretValue`.
- Lambda caches resolved values in memory using `SECRET_CACHE_TTL_SECONDS`.
- IAM is scoped to the two expected Secrets Manager name patterns.
- Terraform plan/state contain names and ARN patterns only, not secret values.
- Secret values must be provisioned in a later dedicated phase.

Rejected strategies:

- raw Lambda environment values: rejected because values can appear in Terraform plan/state/logs.
- Terraform-managed secret values: rejected by default because values can live in state.
- copying from Kubernetes Secret in this phase: not allowed and not performed.

## Lambda Changes

Updated `lambdas/api-gateway-jwt-authorizer/src/index.mjs`:

- replaced raw `JWT_SECRET` and `PLATFORM_TRUSTED_PROXY_SECRET` env values with secret IDs
- added runtime Secrets Manager retrieval
- added in-memory cache with TTL
- validates retrieved values without logging them
- fails closed if config or retrieval is missing/invalid
- supports a plain string secret payload or JSON with a supported string field

Updated package metadata:

- added `@aws-sdk/client-secrets-manager`
- added `npm test`
- added unit tests for secret value parsing

Updated README:

- documents secret IDs instead of raw secret values
- documents runtime retrieval and cache behavior

## IaC Changes

Updated `infra/live/dev/api-gateway-authorizer/terragrunt.hcl`:

- removed `get_env("AUTH_SERVICE_JWT_SECRET")`
- removed `get_env("PLATFORM_TRUSTED_PROXY_SECRET")`
- added non-secret secret IDs:
  - `cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret`
  - `cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret`
- added scoped inline IAM policy:
  - action: `secretsmanager:GetSecretValue`
  - resources:
    - `arn:aws:secretsmanager:us-east-1:145023118802:secret:cloud-native-platform/dev/api-gateway-authorizer/auth-service-jwt-secret-*`
    - `arn:aws:secretsmanager:us-east-1:145023118802:secret:cloud-native-platform/dev/api-gateway-authorizer/platform-trusted-proxy-secret-*`

No broad `secretsmanager:*` permission was added.

No `Resource: "*"` was added.

No secret values were passed to Terraform.

## Metadata-Only Secret Inventory

Secrets Manager metadata-only inventory was executed with `list-secrets`.

SSM Parameter Store metadata-only inventory was executed with `describe-parameters`.

Result:

- no matching authorizer secret objects were found for the chosen names
- no secret values were read
- no `get-secret-value` was executed
- no `get-parameter --with-decryption` was executed
- no secret payload was printed

## Authorizer Plan Result

Authorizer plan was executed read-only.

Result:

- `5 to add, 0 to change, 0 to destroy`

Resources planned:

- Lambda function
- Lambda execution role
- managed basic execution role attachment
- scoped inline Secrets Manager read policy
- CloudWatch log group

Plan validation:

- no raw secret values in Lambda environment variables
- environment contains only secret IDs, issuer/audience, and cache TTL
- IAM action scoped to `secretsmanager:GetSecretValue`
- IAM resources scoped to the two expected secret name patterns
- no broad `secretsmanager:*`
- no broad resource wildcard

No apply was executed.

Evidence:

- `/tmp/cloud-native-platform-apigw-secret-plan/authorizer-init.log`
- `/tmp/cloud-native-platform-apigw-secret-plan/authorizer-plan.log`

## Core / Integration Plan Status

Core and integration were checked plan-only.

`apigateway-core`:

- plan result: `9 to add, 0 to change, 0 to destroy`
- used mock authorizer outputs because authorizer is not applied yet
- VPC Link SG egress remains scoped to TCP/80 toward ALB SGs
- no apply executed

`apigateway-integration`:

- plan result: `10 to add, 0 to change, 0 to destroy`
- used mock core outputs because core is not applied yet
- listener ARN remains the internal ALB HTTP/80 listener
- no apply executed

## Authorizer Tests / Lint

Executed:

- `npm install --package-lock-only`
- `npm test`

Result:

- 3 tests passed
- 0 failures

`npm run lint` was attempted, but no lint script exists in the package.

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

The repository secret-pattern scan returned references in expected code/config files. Matches were summarized by file only to avoid printing any potentially sensitive line.

No secret values were added to documentation, code, or IaC.

## Remaining Gaps

Before authorizer apply:

- create or identify the two Secrets Manager secret objects
- populate values through an approved secret-handling procedure
- do not copy values casually from Kubernetes Secret
- do not print or log values
- confirm authorizer plan still contains no values

Before core apply:

- apply authorizer first and use real outputs

Before integration apply:

- apply core and obtain the real VPC Link SG
- validate or model ALB SG ingress from the real VPC Link SG

## Not Executed

This phase did not execute:

- Terraform/Terragrunt apply
- `terragrunt run-all apply`
- Kubernetes Secret reads or decodes
- `aws secretsmanager get-secret-value`
- `aws ssm get-parameter --with-decryption`
- Helm install/upgrade
- `kubectl apply`
- `kubectl create`
- `kubectl delete`
- Docker build/push
- API Gateway/Lambda/core/integration apply
- Git push

## Recommendation

Recommended next phase:

`Fase 2.16 - Provision authorizer secret references safely`

Scope:

- create the two Secrets Manager secret objects without exposing values
- choose migration vs rotation for current JWT/proxy secrets
- prefer rotation to make Secrets Manager the source of truth if acceptable
- if migration is required, define a tightly controlled secret migration path
- obtain clean authorizer plan using existing secret references
- apply only `api-gateway-authorizer` after the secret source is ready
- keep `apigateway-core` and `apigateway-integration` out of scope until authorizer outputs are real
