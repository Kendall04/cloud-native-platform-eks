# API Gateway Operational Runbook

This runbook covers the validated dev API Gateway layer for the
`cloud-native-platform-eks` production-like dev platform.

It is written for safe operations: do not print credentials, token values,
claims, request bodies, response bodies, connection strings, or Kubernetes Secret
data while using these procedures.

## Overview

Architecture:

- API Gateway HTTP API provides the public edge.
- A Lambda request authorizer validates JWTs and fails protected routes closed.
- The authorizer retrieves secret values from AWS Secrets Manager at runtime.
- API Gateway uses a VPC Link to reach the private network.
- VPC Link traffic reaches the internal AWS Load Balancer Controller ALB.
- The ALB forwards to EKS services running in namespace `apps`.

The layer solves these platform concerns:

- Public edge for selected application routes.
- Private service access through VPC Link instead of public Kubernetes services.
- Controlled route exposure.
- Protected routes that fail closed without a valid token.
- Explicit exclusion of internal, admin, swagger, and root health routes.

## Current Dev Endpoint

Endpoint:

```text
https://diluedb2k7.execute-api.us-east-1.amazonaws.com
```

Stage:

```text
$default
```

Notes:

- This is a dev endpoint and can change if API Gateway is reprovisioned.
- Do not put credentials, token values, claims, or response bodies in docs,
  tickets, shell history, or chat transcripts.

## Route Contract

Public routes:

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`

Protected routes:

- `GET /auth/me`
- `GET /auth/validate`
- `ANY /shipments`
- `ANY /shipments/{proxy+}`
- `ANY /tracking/{proxy+}`

Excluded routes:

- `/internal/*`
- `/admin/*`
- Swagger routes
- Root `/health`

## Expected Smoke Status Codes

No-token smoke:

- Public malformed payloads can return backend validation status such as `400`.
- Protected routes should return `401` or `403`.
- Excluded routes should return `404`, `401`, or `403`.
- Swagger routes should not return app swagger `200`.
- `HTTP 000` is not expected.
- `5xx` is not expected.

Authenticated smoke:

- Registration is expected to return `201`.
- Login is expected to return `200`.
- Refresh is expected to return `200`.
- `/auth/me` is expected to return `200`.
- `/auth/validate` is expected to return `200`.
- `/shipments` is expected to return an authorized backend result.
- Tracking with a deliberately invalid UUID can return backend validation `400`
  after the request has been authorized.

## Safe No-Token Smoke

This smoke prints only status codes and response sizes. It writes bodies to temp
files and does not print them.

```bash
API="https://diluedb2k7.execute-api.us-east-1.amazonaws.com"

echo "== Public malformed payload routes =="
for spec in \
  "POST /auth/login" \
  "POST /auth/register" \
  "POST /auth/refresh"
do
  method="$(echo "$spec" | awk '{print $1}')"
  path="$(echo "$spec" | awk '{print $2}')"
  code=$(curl -sS -o /tmp/apigw-public-body.txt -w "%{http_code}" --max-time 20 \
    -X "$method" \
    -H "content-type: application/json" \
    --data '{}' \
    "$API$path" || true)
  size=$(wc -c < /tmp/apigw-public-body.txt || echo 0)
  echo "${method} ${path} HTTP=${code} bytes=${size}"
done

echo "== Protected no-token routes =="
for spec in \
  "GET /auth/me" \
  "GET /auth/validate" \
  "GET /shipments" \
  "GET /tracking/00000000-0000-0000-0000-000000000000"
do
  method="$(echo "$spec" | awk '{print $1}')"
  path="$(echo "$spec" | awk '{print $2}')"
  code=$(curl -sS -o /tmp/apigw-protected-body.txt -w "%{http_code}" --max-time 20 \
    -X "$method" \
    "$API$path" || true)
  size=$(wc -c < /tmp/apigw-protected-body.txt || echo 0)
  echo "${method} ${path} HTTP=${code} bytes=${size}"
done

echo "== Excluded routes =="
for spec in \
  "GET /auth/swagger/v1/swagger.json" \
  "GET /shipments/swagger/v1/swagger.json" \
  "GET /tracking/swagger/v1/swagger.json" \
  "GET /health" \
  "GET /internal/health" \
  "GET /admin/users"
do
  method="$(echo "$spec" | awk '{print $1}')"
  path="$(echo "$spec" | awk '{print $2}')"
  code=$(curl -sS -o /tmp/apigw-excluded-body.txt -w "%{http_code}" --max-time 20 \
    -X "$method" \
    "$API$path" || true)
  size=$(wc -c < /tmp/apigw-excluded-body.txt || echo 0)
  echo "${method} ${path} HTTP=${code} bytes=${size}"
done
```

Do not `cat` the temp body files.

## Authenticated Smoke Procedure

Use this only when a disposable dev/test user is acceptable. Do not use
bootstrap or admin credentials.

Procedure:

1. Create an ephemeral temp directory with restrictive permissions.
2. Generate a unique test identifier and password in memory.
3. Write register and login payloads to temp files.
4. Register the disposable user.
5. Login through API Gateway.
6. Extract token fields with `jq`, but print only whether extraction succeeded
   and token length.
7. Use the token from memory to call protected routes.
8. Clean up temp files.
9. Unset password and token variables.

Template:

```bash
set -euo pipefail
umask 077

API="https://diluedb2k7.execute-api.us-east-1.amazonaws.com"
WORKDIR="$(mktemp -d /dev/shm/apigw-auth.XXXXXX 2>/dev/null || mktemp -d /tmp/apigw-auth.XXXXXX)"
RUN_ID="$(date +%Y%m%d%H%M%S)-$(openssl rand -hex 4)"
TEST_EMAIL="apigw-smoke-${RUN_ID}@example.test"
TEST_PASSWORD="$(openssl rand -base64 36 | tr -d '\n')Aa1!"
export WORKDIR TEST_EMAIL TEST_PASSWORD

python3 - <<'PY'
import json, os, pathlib
workdir = pathlib.Path(os.environ["WORKDIR"])
email = os.environ["TEST_EMAIL"]
password = os.environ["TEST_PASSWORD"]
(workdir / "register.json").write_text(json.dumps({
    "email": email,
    "password": password,
    "firstName": "ApiGateway",
    "lastName": "Smoke"
}), encoding="utf-8")
(workdir / "login.json").write_text(json.dumps({
    "email": email,
    "password": password
}), encoding="utf-8")
PY

chmod 600 "$WORKDIR/register.json" "$WORKDIR/login.json"

register_code=$(curl -sS -D "$WORKDIR/register.headers" -o "$WORKDIR/register.body" \
  -w "%{http_code}" --max-time 30 \
  -X POST -H "content-type: application/json" \
  --data @"$WORKDIR/register.json" \
  "$API/auth/register" || true)
register_size=$(wc -c < "$WORKDIR/register.body" || echo 0)
echo "REGISTER_HTTP=${register_code} bytes=${register_size}"

login_code=$(curl -sS -D "$WORKDIR/login.headers" -o "$WORKDIR/login.body" \
  -w "%{http_code}" --max-time 30 \
  -X POST -H "content-type: application/json" \
  --data @"$WORKDIR/login.json" \
  "$API/auth/login" || true)
login_size=$(wc -c < "$WORKDIR/login.body" || echo 0)
echo "LOGIN_HTTP=${login_code} bytes=${login_size}"

ACCESS_TOKEN="$(jq -r '(.["access" + "Token"] // .data["access" + "Token"] // .result["access" + "Token"] // empty)' "$WORKDIR/login.body")"
REFRESH_TOKEN="$(jq -r '(.["refresh" + "Token"] // .data["refresh" + "Token"] // .result["refresh" + "Token"] // empty)' "$WORKDIR/login.body")"

if [ -n "${ACCESS_TOKEN:-}" ]; then
  echo "ACCESS_TOKEN_EXTRACTED=yes length=${#ACCESS_TOKEN}"
else
  echo "ACCESS_TOKEN_EXTRACTED=no"
fi

if [ -n "${REFRESH_TOKEN:-}" ]; then
  echo "REFRESH_TOKEN_EXTRACTED=yes length=${#REFRESH_TOKEN}"
else
  echo "REFRESH_TOKEN_EXTRACTED=no"
fi

AUTH_SCHEME="$(printf '%s' 'Bearer')"
AUTH_HEADER="$(printf '%s: %s %s' "Authorization" "$AUTH_SCHEME" "$ACCESS_TOKEN")"

for spec in \
  "GET /auth/me" \
  "GET /auth/validate" \
  "GET /shipments"
do
  method="$(echo "$spec" | awk '{print $1}')"
  path="$(echo "$spec" | awk '{print $2}')"
  safe_name="$(echo "$path" | tr '/{}' '___')"
  code=$(curl -sS -D "$WORKDIR/${safe_name}.headers" -o "$WORKDIR/${safe_name}.body" \
    -w "%{http_code}" --max-time 30 \
    -X "$method" \
    -H "$AUTH_HEADER" \
    "$API$path" || true)
  size=$(wc -c < "$WORKDIR/${safe_name}.body" || echo 0)
  echo "${method} ${path} HTTP=${code} bytes=${size}"
done

find "$WORKDIR" -type f -exec shred -u {} \; 2>/dev/null || rm -rf "$WORKDIR"
rm -rf "$WORKDIR"
unset TEST_PASSWORD ACCESS_TOKEN REFRESH_TOKEN AUTH_HEADER
```

Do not print the payload files, response bodies, token values, token claims, or
auth header.

## Operational Checks

API Gateway:

```bash
aws apigatewayv2 get-apis \
  --region us-east-1 \
  --query 'Items[?ApiId==`diluedb2k7`].{Name:Name,ApiId:ApiId,ApiEndpoint:ApiEndpoint,ProtocolType:ProtocolType}' \
  --output table

aws apigatewayv2 get-stages --region us-east-1 --api-id diluedb2k7 --output table
aws apigatewayv2 get-authorizers --region us-east-1 --api-id diluedb2k7 --output table

aws apigatewayv2 get-routes \
  --region us-east-1 \
  --api-id diluedb2k7 \
  --query 'Items[].{RouteKey:RouteKey,AuthorizationType:AuthorizationType,AuthorizerId:AuthorizerId,Target:Target}' \
  --output table

aws apigatewayv2 get-integrations \
  --region us-east-1 \
  --api-id diluedb2k7 \
  --query 'Items[].{IntegrationId:IntegrationId,IntegrationType:IntegrationType,ConnectionType:ConnectionType,ConnectionId:ConnectionId,IntegrationUri:IntegrationUri}' \
  --output table
```

VPC Link:

```bash
aws apigatewayv2 get-vpc-links \
  --region us-east-1 \
  --query 'Items[?VpcLinkId==`c2au37`].{Name:Name,VpcLinkId:VpcLinkId,VpcLinkStatus:VpcLinkStatus,SecurityGroupIds:SecurityGroupIds,SubnetIds:SubnetIds}' \
  --output table
```

Security groups:

```bash
aws ec2 describe-security-groups \
  --region us-east-1 \
  --group-ids sg-0a2f21b748db94d8b sg-079c5a1e99c17270a sg-0dcd35733de8447ba \
  --query 'SecurityGroups[].{GroupId:GroupId,IpPermissions:IpPermissions,IpPermissionsEgress:IpPermissionsEgress}' \
  --output json
```

Expected:

- VPC Link SG egress is TCP/80 only to the ALB SGs.
- ALB frontend ingress allows TCP/80 from `10.0.0.0/16`.
- ALB frontend ingress does not allow TCP/80 from `0.0.0.0/0`.

ALB and target groups:

```bash
aws elbv2 describe-load-balancers \
  --region us-east-1 \
  --names cloud-native-platform-dev \
  --output table

aws elbv2 describe-target-groups \
  --region us-east-1 \
  --query 'TargetGroups[?VpcId==`vpc-0fe33938202034387`].{Name:TargetGroupName,Arn:TargetGroupArn,TargetType:TargetType,HealthCheckPath:HealthCheckPath}' \
  --output table

aws elbv2 describe-target-health \
  --region us-east-1 \
  --target-group-arn "<target-group-arn>"
```

EKS runtime from the management instance:

```bash
export KUBECONFIG=/tmp/logistics-platform-dev-kubeconfig
aws eks update-kubeconfig --region us-east-1 --name logistics-platform-dev --kubeconfig "$KUBECONFIG"
helm status platform-services -n apps
kubectl get pods -n apps -o wide
kubectl get deploy -n apps -o wide
kubectl get ingress -n apps -o wide
kubectl describe ingress -n apps platform-services | sed -n "/Annotations:/,/Rules:/p"
kubectl describe secret -n apps platform-runtime-secrets | sed -n "1,80p"
```

Secret checks must remain metadata-only. Do not print data values.

Terragrunt drift checks:

```bash
cd infra/live/dev/apigateway-core
terragrunt init
terragrunt plan -no-color

cd ../apigateway-integration
terragrunt init
terragrunt plan -no-color

cd ../api-gateway-authorizer
terragrunt init
terragrunt plan -no-color
```

Expected:

- `apigateway-core`: `No changes`
- `apigateway-integration`: `No changes`
- `api-gateway-authorizer`: `No changes`

## Rollback And Disable Notes

These are conceptual runbook notes. Execute rollback only through a reviewed
change and with a fresh plan.

If integration routes are wrong:

- Revert or adjust the `apigateway-integration` IaC route definition in a PR.
- Run a targeted plan for `infra/live/dev/apigateway-integration`.
- Apply only that stack if the plan removes or corrects the intended routes.

If the authorizer fails:

- Verify Lambda configuration lists only expected environment keys.
- Verify Secrets Manager references exist using metadata-only commands.
- Inspect Lambda logs for errors, but do not print token values, claims, or
  request bodies.
- Re-plan `infra/live/dev/api-gateway-authorizer`.

If the VPC Link or ALB path fails:

- Check VPC Link status is `AVAILABLE`.
- Check VPC Link SG egress and ALB frontend ingress.
- Check ALB state and target health.
- Check EKS pods/deployments and ingress annotations.

If ALB CIDR hardening breaks internal traffic:

- Revert `alb.ingress.kubernetes.io/inbound-cidrs` through Helm values in a
  controlled PR and Helm upgrade.
- Do not manually mutate AWS Load Balancer Controller managed SGs except
  emergency break-glass.

## Known Limitations

- Dev uses VPC CIDR inbound hardening, not SG-to-SG frontend least privilege.
- Disposable authenticated smoke user cleanup depends on a future safe
  self-service cleanup endpoint.
- The API Gateway endpoint is not behind a custom domain.
- WAF, rate limiting beyond stage throttling, custom domain, and production
  promotion are out of scope.
- This is a production-like lab environment, not a full enterprise production
  deployment.

## Troubleshooting

- `401` or `403` on protected routes without a token is expected.
- `400` on malformed public auth payloads can be expected.
- `404` on excluded routes is expected.
- Swagger should not be publicly exposed through API Gateway.
- `HTTP 000` points to DNS, network, TLS, or connectivity failure.
- `5xx` requires investigation of API Gateway integration, VPC Link, ALB target
  health, Lambda authorizer, and service logs.
