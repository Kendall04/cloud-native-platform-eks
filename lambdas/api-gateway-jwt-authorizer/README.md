# API Gateway JWT Authorizer

This Lambda closes the platform trust boundary for the HTTP API edge.

It is used by API Gateway as a `REQUEST` authorizer for protected routes and validates the HMAC-signed JWTs issued by `auth-service`.

## Environment Variables

- `JWT_ISSUER`
- `JWT_AUDIENCE`
- `JWT_SECRET`
- `PLATFORM_TRUSTED_PROXY_SECRET`

## Behavior

- validates `Authorization: Bearer <token>`
- enforces `HS256`, issuer, audience, signature, `nbf`, and `exp`
- returns simple authorizer context for API Gateway request parameter mapping
- injects a shared proxy secret into the authorizer context so backend services can trust only API-Gateway-verified identity headers

## Deployment

This Lambda is packaged and deployed through Terragrunt:

- `infra/live/dev/api-gateway-authorizer/terragrunt.hcl`

The `JWT_SECRET` and `PLATFORM_TRUSTED_PROXY_SECRET` values are intentionally sourced from deployment-time environment variables rather than committed to the repository.
