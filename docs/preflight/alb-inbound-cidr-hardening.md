# API Gateway ALB Inbound CIDR Hardening

Date: 2026-07-05 22:21 CST

Branch: `chore/alb-inbound-cidr-hardening`

AWS account: `145023118802`

## Objective

Apply partial least-privilege hardening for the internal ALB frontend ingress used by the API Gateway VPC Link path, without applying `apigateway-integration`.

Selected hardening:

- Add `alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16`
- Apply only the existing `platform-services` Helm release
- Keep service images, digests, Secrets, and API Gateway integration untouched

## Baseline Runtime And ALB

Management instance:

- Instance ID: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- Role: `cloud-native-platform-dev-management-role`

Baseline runtime:

- Helm release: `platform-services`
- Namespace: `apps`
- Initial revision: `3`
- Status: `deployed`
- Deployments: `auth-service`, `shipment-service`, `tracking-service` all `2/2`
- Pods: Running/Ready
- Ingress: `platform-services`

Baseline smoke:

- `/auth/swagger/v1/swagger.json`: `200`
- `/shipments/swagger/v1/swagger.json`: `200`
- `/tracking/swagger/v1/swagger.json`: `200`
- `/auth/me`: `401`
- `/shipments`: `401`
- `/tracking/00000000-0000-0000-0000-000000000000`: `401`
- No `HTTP 000`
- No `5xx`

ALB:

- Name: `cloud-native-platform-dev`
- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:loadbalancer/app/cloud-native-platform-dev/feabb93df9e991e0`
- Scheme: `internal`
- State: `active`
- Listener: HTTP/80
- Listener ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`

Target groups remained `ip` target type with `/health` health checks and two healthy targets per app service.

## Baseline SG State

VPC Link SG:

- `sg-0a2f21b748db94d8b`
- Egress TCP/80 only to:
  - `sg-079c5a1e99c17270a`
  - `sg-0dcd35733de8447ba`
- No HTTP `0.0.0.0/0`
- No all-traffic egress

ALB SGs:

- Frontend SG: `sg-079c5a1e99c17270a`
- Backend SG: `sg-0dcd35733de8447ba`
- Both are AWS Load Balancer Controller managed.

Before hardening, frontend SG allowed TCP/80 from `0.0.0.0/0`.

## Chart Support And Render Review

The chart supports ALB annotations through `global.ingress.annotations`.

Change made in `k8s/environments/dev/platform-services.values.yaml`:

```yaml
alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16
```

Local render diff was limited to the Ingress annotation:

```diff
+    alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16
```

No image, digest, Secret, Service, Deployment, path, or route changes were introduced.

## Artifact Transfer

Because the EKS API is private, the chart package and values were uploaded temporarily to:

```text
s3://cloud-native-platform-145023118802-dev-us-east-1-artifacts/tmp/alb-inbound-cidr-hardening/9853e2a/
```

Artifacts:

- `platform-services-0.1.0.tgz`
- `platform-services.values.yaml`

The temporary S3 prefix was removed after the Helm upgrade.

## Temporary Management Permissions

Management initially could not read the temporary S3 prefix because its artifact policy was scoped to `cluster-addons/dev`.

Temporary management changes were applied through the `management` Terragrunt stack only:

- S3 read scope changed temporarily to `tmp/alb-inbound-cidr-hardening/9853e2a`
- `AmazonEKSEditPolicy` temporarily associated for namespace `apps`

Temporary plan:

- `1 to add, 1 to change, 0 to destroy`

Temporary apply:

- Executed only for `infra/live/dev/management`
- No EC2 replacement
- No SG changes
- No endpoint changes

Cleanup plan:

- `0 to add, 1 to change, 1 to destroy`

Cleanup apply:

- Restored S3 read scope to `cluster-addons/dev`
- Removed `AmazonEKSEditPolicy`

Final management plan:

- `No changes`

Final management access:

- `AmazonEKSAdminViewPolicy`
- Inline policies:
  - `cloud-native-platform-dev-management-artifact-read-only`
  - `cloud-native-platform-dev-management-eks-read-only`
- Attached policy:
  - `AmazonSSMManagedInstanceCore`
- No temporary `AmazonEKSEditPolicy`
- No temporary Secrets Manager write policy

## Helm Dry Run

Dry run was executed from the management host using the uploaded chart and values.

Result:

- Render included `alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16`
- Images and digests remained unchanged
- Only Secret key references were rendered; no Secret values were printed
- Release remained revision `3` before the real upgrade

## Helm Upgrade

Command class:

- `helm upgrade platform-services <packaged-chart> -n apps -f <values> --wait --timeout 10m`

Result:

- Release upgraded successfully
- New revision: `4`
- Status: `deployed`
- Deployments remained `2/2`
- Pods remained Running/Ready

One SSM command was marked failed after Helm succeeded because a follow-up `kubectl` JSONPath expression was incorrectly escaped. A follow-up read-only validation confirmed the release and annotation state.

## Ingress Annotation After

Ingress annotation confirmed:

```text
alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16
```

AWS Load Balancer Controller emitted a `SuccessfullyReconciled` event after the upgrade.

## SG State After

VPC Link SG:

- `sg-0a2f21b748db94d8b`
- Egress remains TCP/80 only to the two ALB SGs
- No HTTP `0.0.0.0/0`
- No all-traffic egress

ALB frontend SG:

- `sg-079c5a1e99c17270a`
- TCP/80 ingress is now from `10.0.0.0/16`
- TCP/80 ingress from `0.0.0.0/0` is absent

ALB backend SG:

- `sg-0dcd35733de8447ba`
- No explicit frontend TCP/80 ingress rule was needed for the requested inbound CIDR hardening.

Explicit SG-to-SG ingress from the VPC Link SG to the ALB frontend SG remains absent. This phase intentionally applied VPC CIDR hardening, not custom SG-to-SG frontend ownership.

## ALB And Target Health After

ALB:

- Name: `cloud-native-platform-dev`
- State: `active`
- Listener: HTTP/80 present

Target groups:

- `k8s-apps-authserv-58a4b90ba7`: two healthy targets
- `k8s-apps-shipment-fb67f1d90f`: two healthy targets
- `k8s-apps-tracking-9810f2e09e`: two healthy targets

## Runtime Smoke After

Post-hardening smoke:

- `/auth/swagger/v1/swagger.json`: `200`
- `/shipments/swagger/v1/swagger.json`: `200`
- `/tracking/swagger/v1/swagger.json`: `200`
- `/auth/me`: `401`
- `/shipments`: `401`
- `/tracking/00000000-0000-0000-0000-000000000000`: `401`
- No `HTTP 000`
- No `5xx`

Final runtime:

- Helm release: `platform-services`
- Revision: `4`
- Status: `deployed`
- Deployments: `2/2`
- Pods: Running/Ready
- Ingress present
- Kubernetes Secret inspected only as metadata/key names and byte sizes; no values were read or decoded.

## API Gateway Plans

`apigateway-core` read-only plan after hardening:

- `No changes`

`apigateway-integration` read-only plan after hardening:

- `10 to add, 0 to change, 0 to destroy`
- Uses real outputs:
  - API: `diluedb2k7`
  - VPC Link: `c2au37`
  - Authorizer: `evdkk3`
  - Listener ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`

Routes planned:

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

`apigateway-integration` was not applied.

## Readiness Classification

Classification: ready for dev integration readiness with accepted residual gap.

Reasoning:

- The previous broad frontend ingress `0.0.0.0/0` was reduced to the VPC CIDR `10.0.0.0/16`.
- The ALB is internal.
- VPC Link SG egress is scoped to ALB SGs on TCP/80.
- ALB remained active and target groups remained healthy.
- Integration plan is clean with real core outputs.

Residual gap:

- ALB frontend ingress is CIDR-scoped, not SG-to-SG scoped from `sg-0a2f21b748db94d8b`.
- A custom frontend SG strategy could tighten further, but should be done in a separate phase because the ALB SGs are LBC-managed.

## No-Apply Confirmation

This phase did not:

- Run `terragrunt run-all apply`
- Apply `apigateway-integration`
- Read or decode Kubernetes Secret values
- Run `aws secretsmanager get-secret-value`
- Run `aws secretsmanager put-secret-value`
- Run `aws ssm get-parameter --with-decryption`
- Build or push Docker images
- Change the EKS public/private endpoint
- Push Git

This phase did run:

- A controlled Helm upgrade for `platform-services`
- Temporary management stack applies to grant and then remove scoped operational access

## Recommendation

Proceed to:

```text
Fase 2.21 - API Gateway integration apply readiness and controlled apply
```

Recommended scope:

- Confirm product acceptance that dev ALB ingress scoped to `10.0.0.0/16` is sufficient for integration apply
- Re-plan `apigateway-integration` with real outputs
- Apply only `apigateway-integration` if the plan remains clean
- Validate API Gateway routes and integrations
- Smoke public auth routes through API Gateway
- Smoke protected routes without token expecting authorizer denial
- Obtain a valid token through the public login path if test credentials are explicitly approved
- Keep any SG-to-SG frontend hardening as a later optional improvement
