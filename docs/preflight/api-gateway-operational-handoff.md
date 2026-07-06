# API Gateway Operational Handoff

Date: 2026-07-06
Branch: `docs/apigateway-operational-handoff`
AWS account: `145023118802`
Region: `us-east-1`

## Scope

This handoff records the final validated dev API Gateway state and points
operators to the operational runbook.

No infrastructure, Kubernetes resources, Helm releases, Docker images, endpoints,
or secrets were changed in this phase.

## Endpoint

Dev API Gateway endpoint:

```text
https://diluedb2k7.execute-api.us-east-1.amazonaws.com
```

Stage:

```text
$default
```

This endpoint is environment-specific and can change if the API Gateway stack is
reprovisioned.

## Final Architecture State

Validated path:

```text
client -> API Gateway HTTP API -> Lambda authorizer -> VPC Link -> internal ALB -> EKS services
```

Current resources:

- API ID: `diluedb2k7`
- API name: `cloud-native-platform-dev-http-api`
- Authorizer ID: `evdkk3`
- Authorizer Lambda: `cloud-native-platform-dev-api-jwt-authorizer`
- VPC Link ID: `c2au37`
- VPC Link status: `AVAILABLE`
- VPC Link SG: `sg-0a2f21b748db94d8b`
- Internal ALB: `cloud-native-platform-dev`
- ALB DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- ALB frontend ingress: TCP/80 from `10.0.0.0/16`
- ALB frontend ingress from `0.0.0.0/0`: absent

Runtime:

- EKS cluster: `logistics-platform-dev`
- Helm release: `platform-services`
- Namespace: `apps`
- Release revision: `4`
- `auth-service`: `2/2`
- `shipment-service`: `2/2`
- `tracking-service`: `2/2`
- Pods: Running/Ready
- Target groups: 2 healthy targets per service

## Validated Route Contract

Public:

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`

Protected:

- `GET /auth/me`
- `GET /auth/validate`
- `ANY /shipments`
- `ANY /shipments/{proxy+}`
- `ANY /tracking/{proxy+}`

Excluded:

- `/internal/*`
- `/admin/*`
- Swagger routes
- Root `/health`

## Safe No-Token Smoke Summary

The handoff smoke printed only status codes and byte sizes.

Public malformed payload routes:

- `POST /auth/login`: `400`
- `POST /auth/register`: `400`
- `POST /auth/refresh`: `400`

Protected no-token routes:

- `GET /auth/me`: `401`
- `GET /auth/validate`: `401`
- `GET /shipments`: `401`
- `GET /tracking/00000000-0000-0000-0000-000000000000`: `401`

Excluded routes:

- `GET /auth/swagger/v1/swagger.json`: `404`
- `GET /shipments/swagger/v1/swagger.json`: `401`
- `GET /tracking/swagger/v1/swagger.json`: `401`
- `GET /health`: `404`
- `GET /internal/health`: `404`
- `GET /admin/users`: `404`

No response bodies were printed. No `HTTP 000` or `5xx` was observed.

## Authenticated Smoke Evidence Summary

The authenticated smoke evidence is recorded in:

- `docs/preflight/api-gateway-authenticated-smoke.md`

Summary:

- Disposable dev/test user created through API Gateway.
- No bootstrap or admin credentials were read or used.
- Registration returned `201`.
- Login returned `200`.
- Refresh returned `200`.
- Token extraction succeeded, but token values were not printed or committed.
- `GET /auth/me` returned `200`.
- `GET /auth/validate` returned `200`.
- `GET /shipments` returned `200`.
- `GET /tracking/00000000-0000-0000-0000-000000000000` returned `400`, interpreted as an authorized request reaching backend validation.
- Temp files were cleaned up.

## Final Health Summary

Read-only validation confirmed:

- API Gateway HTTP API exists.
- `$default` stage exists.
- Lambda request authorizer exists.
- Expected routes and integrations exist.
- VPC Link `c2au37` is `AVAILABLE`.
- VPC Link SG egress remains scoped to ALB SGs on TCP/80.
- ALB is internal and active.
- ALB frontend ingress is restricted to `10.0.0.0/16`.
- Target groups are healthy.
- EKS deployments are `2/2`.
- Kubernetes Secret inspection remained metadata-only.

## Final Plans Summary

Read-only Terragrunt plans:

- `infra/live/dev/apigateway-core`: `No changes`
- `infra/live/dev/apigateway-integration`: `No changes`
- `infra/live/dev/api-gateway-authorizer`: `No changes`

No apply was executed in this phase.

## Operational Runbook

Primary runbook:

- `docs/operations/api-gateway-runbook.md`

The runbook covers:

- Route contract.
- No-token smoke.
- Authenticated smoke procedure.
- API Gateway, VPC Link, ALB, SG, EKS, and Terragrunt checks.
- Rollback and disable notes.
- Known limitations and troubleshooting.

## Known Limitations

- Dev uses VPC CIDR inbound hardening, not SG-to-SG frontend least privilege.
- Disposable smoke user cleanup is not automated because no safe non-admin
  self-delete endpoint is in scope.
- API Gateway has no custom domain yet.
- WAF and production promotion are out of scope.
- This is a production-like dev platform and portfolio-grade validation, not a
  full enterprise production deployment.

## Recommendation

Proceed to `Fase 2.24 — Documentation PR integration and final portfolio polish`.

Recommended scope:

- Integrate this handoff documentation via PR.
- Confirm docs stay secret-free.
- Optionally add a concise architecture diagram or README table.
- Keep future improvements tracked: custom domain, WAF/rate limits, SG-to-SG
  frontend hardening, and disposable test-user cleanup.
