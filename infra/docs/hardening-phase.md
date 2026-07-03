# Hardening Phase

This hardening pass closes the main control-plane gaps without changing the service ownership model.

## API Edge

Implemented path:

```text
API Gateway HTTP API -> VPC Link -> internal ALB -> EKS services
```

Stacks involved:

- `infra/live/dev/api-gateway-authorizer`
- `infra/live/dev/apigateway-core`
- `infra/live/dev/apigateway-integration`

The HTTP API now includes:

- a VPC Link into the private subnets
- ALB listener integrations
- concrete routes for `/auth/*`, `/shipments/*`, `/tracking/*`, and `/admin/*`
- a Lambda request authorizer for protected routes

`apigateway-core` can be applied during the base infrastructure bootstrap.
`apigateway-integration` resolves the internal ALB after the EKS ingress exists, so it must be applied only after the Helm chart has created the ALB.

## JWT Trust Boundary

`auth-service` still issues HMAC-signed JWTs.

Because API Gateway's native HTTP API JWT authorizer is not the clean fit for the current symmetric-token model, the repository now uses a Lambda request authorizer that validates:

- `HS256` signature
- issuer
- audience
- `nbf`
- `exp`

After validation, API Gateway injects verified identity headers into protected requests and adds a shared proxy secret header.

`shipment-service` and `tracking-service` now trust only:

- API-Gateway-injected identity headers
- when accompanied by the configured trusted proxy secret

Direct ALB traffic that only forwards a bearer token no longer satisfies public authorization in those services outside Development mode.

## Internal Service Authentication

`tracking-service` no longer forwards the caller bearer token to `shipment-service`.

Instead it uses internal shipment endpoints:

- `GET /internal/shipments/{id}/exists`
- `GET /internal/shipments/{id}`
- `GET /internal/shipments/by-tracking/{trackingNumber}`

Those endpoints require `X-Platform-Internal-Secret` in non-development environments.

## Database Topology

The repository now standardizes on:

- one PostgreSQL RDS instance
- one logical database: `platform`
- one schema per service:
  - `auth`
  - `shipment`
  - `tracking`

Runtime behavior:

- each service sets `Database__Schema`
- each service forces the PostgreSQL `search_path` to its own schema
- each service stores EF migration history in its own schema
- migration initialization creates the schema before applying migrations

This keeps data ownership isolated without requiring a larger Terraform bootstrap redesign for multiple logical databases and users in this phase.

## Migrations

Normal app startup no longer runs EF Core migrations.

Safe migration path now:

- app images support `--migrate`
- Helm renders pre-install / pre-upgrade migration jobs per service
- the jobs run before the Deployments roll out

## Event Publication Reliability

Direct EventBridge publishing remains in place, but it is now hardened with:

- bounded retries
- explicit exception handling
- structured failure logging
- clear logging when publication fails after the database commit

The publisher interfaces were kept narrow so a future outbox-backed implementation can replace the current direct publishers cleanly.

## Deployment Inputs

Before applying the API edge stacks, export:

```bash
export AUTH_SERVICE_JWT_SECRET="<same secret used by auth-service>"
export PLATFORM_TRUSTED_PROXY_SECRET="<32-byte-or-longer shared proxy secret>"
```

The Kubernetes runtime secret also needs:

- `platform-trusted-proxy-secret`
- `platform-internal-service-secret`
