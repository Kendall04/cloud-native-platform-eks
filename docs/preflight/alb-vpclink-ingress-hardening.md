# ALB VPC Link Ingress Hardening Readiness

Date: 2026-07-05 21:47:50 CST
Branch: `chore/alb-vpclink-ingress-hardening`
AWS account: `145023118802`
Region: `us-east-1`

## Objective

Evaluate and decide the least-privilege path for ALB ingress from the API Gateway VPC Link security group before any `apigateway-integration` apply.

This phase did not apply `apigateway-integration`.

## Baseline Runtime and ALB

Runtime baseline:

- Cluster: `logistics-platform-dev`
- Helm release: `platform-services`
- Namespace: `apps`
- Release status: `deployed`
- Release revision: `3`
- `auth-service`: `2/2`
- `shipment-service`: `2/2`
- `tracking-service`: `2/2`
- Pods: Running/Ready
- Services and Ingress: present
- Kubernetes Secret check: metadata and key names only

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

ALB:

- Name: `cloud-native-platform-dev`
- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:loadbalancer/app/cloud-native-platform-dev/feabb93df9e991e0`
- Scheme: `internal`
- State: `active`
- VPC: `vpc-0fe33938202034387`
- VPC CIDR: `10.0.0.0/16`
- Listener: HTTP/80

Target groups:

| Target group | Target type | Health check | Health |
| --- | --- | --- | --- |
| `k8s-apps-authserv-58a4b90ba7` | ip | `/health` | 2 healthy |
| `k8s-apps-shipment-fb67f1d90f` | ip | `/health` | 2 healthy |
| `k8s-apps-tracking-9810f2e09e` | ip | `/health` | 2 healthy |

## API Gateway Core and VPC Link State

API Gateway core is already applied:

- HTTP API ID: `diluedb2k7`
- Endpoint: `https://diluedb2k7.execute-api.us-east-1.amazonaws.com`
- Stage: `$default`
- Authorizer ID: `evdkk3`

Lambda authorizer:

- Function: `cloud-native-platform-dev-api-jwt-authorizer`
- Runtime: `nodejs22.x`
- Handler: `src/index.handler`
- State: `Active`

VPC Link:

- ID: `c2au37`
- Status: `AVAILABLE`
- Security group: `sg-0a2f21b748db94d8b`
- Subnets:
  - `subnet-03aa254292f6017e8`
  - `subnet-0d50cde4bff9b5154`
  - `subnet-0ecdf9e460a352dc6`

VPC Link SG egress:

- TCP/80 to `sg-079c5a1e99c17270a`
- TCP/80 to `sg-0dcd35733de8447ba`
- No HTTP egress to `0.0.0.0/0`
- No all-traffic egress

## Current Ingress Annotations

Current `platform-services` Ingress annotations:

- `alb.ingress.kubernetes.io/backend-protocol: HTTP`
- `alb.ingress.kubernetes.io/group.name: cloud-native-platform`
- `alb.ingress.kubernetes.io/healthcheck-path: /health`
- `alb.ingress.kubernetes.io/healthcheck-port: traffic-port`
- `alb.ingress.kubernetes.io/listen-ports: [{"HTTP":80}]`
- `alb.ingress.kubernetes.io/load-balancer-name: cloud-native-platform-dev`
- `alb.ingress.kubernetes.io/scheme: internal`
- `alb.ingress.kubernetes.io/success-codes: 200-399`
- `alb.ingress.kubernetes.io/target-type: ip`

The chart renders annotations from values:

- `k8s/charts/platform-services/templates/ingress.yaml`
- `k8s/environments/dev/platform-services.values.yaml`

The current chart can add ALB annotations through values without template changes.

## Ownership Analysis

ALB tags:

- `ingress.k8s.aws/resource=LoadBalancer`
- `ingress.k8s.aws/stack=cloud-native-platform`
- `elbv2.k8s.aws/cluster=logistics-platform-dev`

ALB security groups:

| SG | Role | Ownership evidence |
| --- | --- | --- |
| `sg-079c5a1e99c17270a` | ALB frontend SG | LBC-managed, tagged `ingress.k8s.aws/resource=ManagedLBSecurityGroup` and `ingress.k8s.aws/stack=cloud-native-platform` |
| `sg-0dcd35733de8447ba` | shared backend SG | LBC-managed, tagged `elbv2.k8s.aws/resource=backend-sg` |
| `sg-0a2f21b748db94d8b` | API Gateway VPC Link SG | Terraform-managed by `apigateway-core` |

Current ALB ingress:

- `sg-079c5a1e99c17270a` allows TCP/80 from `0.0.0.0/0`.
- `sg-0dcd35733de8447ba` has no ingress rules.
- Neither ALB SG has explicit TCP/80 ingress from `sg-0a2f21b748db94d8b`.

Current classification:

- Functionally reachable because the ALB is internal and the LBC-managed frontend SG allows TCP/80 broadly.
- Not least-privilege because frontend ingress is `0.0.0.0/0`.
- No manual SG changes were made.

## Hardening Options Evaluated

### Option A: Custom Terraform-managed frontend ALB SG via Ingress annotation

Design:

- Create or reuse a controlled frontend ALB SG.
- Set `alb.ingress.kubernetes.io/security-groups`.
- Set `alb.ingress.kubernetes.io/manage-backend-security-group-rules` if needed.
- Allow TCP/80 from the VPC Link SG and any explicitly required internal smoke source.

Pros:

- Can reach true SG-to-SG least privilege.

Risks:

- Changes ALB frontend SG association.
- AWS Load Balancer Controller behavior around custom frontend SGs and backend rule management must be tested carefully.
- A bad annotation could interrupt ALB-to-pod traffic or management smoke.
- This phase did not have a clean, low-risk apply path for this change.

Decision:

- Not applied in this phase.

### Option B: LBC-managed `inbound-cidrs` annotation

Design:

- Add `alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16` through Helm values.
- Let AWS Load Balancer Controller update its managed frontend SG.

Pros:

- Keeps the frontend SG under LBC ownership.
- Avoids direct Terraform/manual mutation of an LBC-managed SG.
- Reduces ingress from `0.0.0.0/0` to VPC-only.
- Management EC2 and VPC Link ENIs are in the VPC, so existing internal paths should remain viable.

Risks:

- It is CIDR-based, not SG-to-SG least privilege.
- Still allows other VPC sources to reach ALB port 80.
- Applying requires a controlled Helm upgrade from an environment that can reach the private EKS API and has the chart/values available.

Operational finding:

- Local kubeconfig creation succeeded, but local `kubectl` did not reach the private API server in this environment.
- Management EC2 has Helm but does not currently have this repository/chart checked out.
- Applying this via an ad hoc chart transfer or direct `kubectl annotate` would create operational fragility or temporary Helm drift.

Decision:

- Selected as the recommended next hardening path.
- Not applied in this phase because the apply path was not clean enough.

### Option C: Terraform-managed ingress rule on LBC-managed ALB SG

Design:

- Add an external Terraform `aws_vpc_security_group_ingress_rule` to the existing LBC-managed frontend SG from the VPC Link SG.

Pros:

- Could add explicit VPC Link SG ingress.

Risks:

- Does not remove the existing `0.0.0.0/0` frontend rule.
- Removing or managing the broad LBC-created rule outside LBC risks reconciliation drift.
- Cross-owner SG management is harder to reason about.

Decision:

- Rejected for this phase.

### Option D: Accept current internal broad ALB ingress temporarily

Design:

- Accept the current state for dev:
  - ALB is internal.
  - VPC Link SG egress is least-privilege.
  - ALB frontend SG remains broad on TCP/80.

Pros:

- Lowest operational risk.
- `apigateway-integration` would likely be functionally able to connect.

Risks:

- Not least-privilege.
- Leaves a security hardening gap.

Decision:

- Accepted only as the current observed state, not as the final desired state.
- `apigateway-integration` apply remains deferred until the ALB ingress path is either hardened or explicitly accepted by a later phase.

## Selected Strategy

Selected strategy for this phase:

- Do not apply live hardening.
- Document Option B as the recommended low-risk next implementation:
  - add `alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16` through Helm values;
  - execute a normal Helm upgrade from an environment with both private EKS reachability and the repo/chart available;
  - validate ALB SG, targets, runtime, and smoke immediately after.

Justification:

- The current ALB frontend SG is clearly LBC-managed.
- Manual SG mutation would create ownership ambiguity.
- Custom SG attachment is a bigger behavioral change than needed for the immediate dev hardening step.
- A direct `kubectl annotate` would bypass Helm release state.
- Local Helm cannot reach the private API server; management EC2 does not have the repo/chart available.

Risk:

- Current dev ALB remains internally broad on TCP/80 until Option B or Option A is applied.

## Changes Made

No IaC, Helm values, Kubernetes manifests, security groups, or AWS resources were changed in this phase.

Files created:

- `docs/preflight/alb-vpclink-ingress-hardening.md`

## Hardening Plan/Apply Summary

Hardening apply:

- Skipped.

Reason:

- No clean apply path was available in this phase that avoided manual drift and avoided a risky custom frontend SG change.

Add/change/destroy:

- No hardening stack or Helm change was applied.

## SG State After

Because no hardening was applied, SG state remains unchanged.

VPC Link SG:

- `sg-0a2f21b748db94d8b`
- Egress TCP/80 only to:
  - `sg-079c5a1e99c17270a`
  - `sg-0dcd35733de8447ba`
- No HTTP `0.0.0.0/0`
- No all-traffic egress

ALB frontend SG:

- `sg-079c5a1e99c17270a`
- Ingress TCP/80 from `0.0.0.0/0`
- No explicit ingress from `sg-0a2f21b748db94d8b`

ALB backend SG:

- `sg-0dcd35733de8447ba`
- No ingress rules

## ALB, Listener, and Target Health After

ALB remains:

- Name: `cloud-native-platform-dev`
- Scheme: `internal`
- State: `active`

Target groups remain:

- target type `ip`
- health check `/health`
- 2 healthy targets per service

No target drain or health regression was observed.

## Runtime Smoke After

Post-decision ALB smoke from management EC2:

| Path | HTTP | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/auth/swagger/v1/swagger.json` | 200 | 12819 | Auth service reachable |
| `/shipments/swagger/v1/swagger.json` | 200 | 10618 | Shipment service reachable |
| `/tracking/swagger/v1/swagger.json` | 200 | 6817 | Tracking service reachable |
| `/auth/me` | 401 | 0 | Protected route reachable, token required |
| `/shipments` | 401 | 0 | Protected route reachable, token required |
| `/tracking/00000000-0000-0000-0000-000000000000` | 401 | 0 | Protected route reachable, token required |

No `HTTP 000` and no `5xx` were observed.

Runtime final:

- `platform-services`: deployed revision `3`
- `auth-service`: `2/2`
- `shipment-service`: `2/2`
- `tracking-service`: `2/2`
- Pods: Running/Ready
- Ingress: present

## Integration Plan After Hardening Decision

Stack:

- `infra/live/dev/apigateway-integration`

Plan result:

- `10 to add, 0 to change, 0 to destroy`
- No apply.

Uses real outputs:

- API ID: `diluedb2k7`
- VPC Link ID: `c2au37`
- Authorizer ID: `evdkk3`
- ALB listener ARN: `arn:aws:elasticloadbalancing:us-east-1:145023118802:listener/app/cloud-native-platform-dev/feabb93df9e991e0/f91fd1bd16e313b9`

Route contract:

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

## Core Plan After Hardening Decision

Stack:

- `infra/live/dev/apigateway-core`

Plan result:

- `No changes`

## Readiness Classification

Classification:

- Blocked for least-privilege `apigateway-integration` apply.

Reason:

- `apigateway-integration` plan is clean and uses real outputs.
- Runtime is healthy.
- VPC Link SG egress is least-privilege.
- ALB frontend SG remains broad on TCP/80 and lacks explicit ingress from the VPC Link SG.
- No clean live hardening apply path was available in this phase.

Functional note:

- The current internal ALB path would likely allow VPC Link traffic because the frontend SG allows TCP/80 broadly.
- Treating that as acceptable for integration apply requires an explicit risk acceptance in a later phase.

## Remaining Gaps

- Apply Option B through a controlled Helm path, or implement Option A with a tested custom SG design.
- Avoid direct manual mutation of LBC-managed SGs.
- Avoid direct `kubectl annotate` drift unless explicitly accepted.
- Re-plan `apigateway-integration` after ALB ingress hardening or risk acceptance.
- Keep `apigateway-integration` apply deferred until the ingress posture is intentionally approved.

## Recommendation

Proceed to `Fase 2.20 — Apply ALB inbound CIDR hardening and integration readiness`.

Recommended scope:

- Add `alb.ingress.kubernetes.io/inbound-cidrs: 10.0.0.0/16` to `k8s/environments/dev/platform-services.values.yaml`.
- Execute a controlled Helm upgrade from an environment that has:
  - private EKS API reachability;
  - this repository/chart and values file available;
  - no image/digest changes.
- Validate that LBC updates the managed frontend SG from TCP/80 `0.0.0.0/0` to TCP/80 `10.0.0.0/16`.
- Validate ALB active, target groups healthy, and ALB smoke unchanged.
- Re-plan `apigateway-integration` with real outputs.
- Decide whether VPC CIDR hardening is sufficient for dev integration apply, or whether Option A SG-to-SG hardening is required first.

References used:

- AWS Load Balancer Controller security group management: https://kubernetes-sigs.github.io/aws-load-balancer-controller/v2.5/deploy/security_groups/
- AWS Load Balancer Controller Ingress annotations: https://kubernetes-sigs.github.io/aws-load-balancer-controller/latest/guide/ingress/annotations/
