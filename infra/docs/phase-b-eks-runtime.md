# Phase B EKS Runtime

Phase B prepares the three .NET services to run as EKS workloads while keeping the event-driven architecture from Phase A intact.

## Runtime Placement

Runs in EKS:

- `auth-service`
- `shipment-service`
- `tracking-service`

Remains outside EKS:

- `notification-lambda`

Deferred:

- analytics worker implementation

## Kubernetes Packaging

Helm packaging lives under:

- `k8s/charts/cluster-addons`
- `k8s/charts/platform-services`

`cluster-addons` installs the cluster-wide operators that the runtime depends on:

- `aws-load-balancer-controller`
- `cluster-autoscaler`

`platform-services` deploys:

- one `Deployment` per service
- one `Service` per service
- one `ServiceAccount` per service
- a shared internal ALB `Ingress`
- readiness, liveness, and startup probes
- `PodDisruptionBudget` resources
- optional KEDA scaffolding for `shipment-service`

## Routing Model

Prepared ingress paths:

- `/auth`
- `/admin/users`
- `/shipments`
- `/admin/shipments`
- `/tracking`
- `/admin/tracking-events`

This aligns with the target north-south path:

```text
API Gateway -> VPC Link -> ALB -> EKS
```

The Helm ingress is now matched by Terraform that provisions the HTTP API, Lambda authorizer, VPC Link, and ALB integration on the north-south path.
The EKS Terragrunt stack prepares the IAM/OIDC prerequisites for the addon service accounts, while the addon chart installs the operators inside the cluster.

## Internal Service Communication

`tracking-service` calls `shipment-service` over Kubernetes DNS:

```text
http://shipment-service.apps.svc.cluster.local:8080
```

Used contracts:

- `GET /internal/shipments/{id}`
- `GET /internal/shipments/by-tracking/{trackingNumber}`
- `GET /internal/shipments/{id}/exists`

The `/internal/*` path is intentionally not routed through ingress and is protected by a shared internal service secret.

## IAM And Pod Identity

IRSA roles are provisioned in:

- `infra/live/dev/iam/terragrunt.hcl`

Configured roles:

- `cloud-native-platform-dev-shipment-service-irsa`
- `cloud-native-platform-dev-tracking-service-irsa`

Expected permissions:

- `shipment-service`: SQS consume permissions and EventBridge `PutEvents`
- `tracking-service`: EventBridge `PutEvents`
- `auth-service`: no AWS permissions by default

## Queue Consumer Hardening

`shipment-service` continues to consume `shipment-events-queue` from inside Kubernetes.

Implemented runtime behavior:

- configurable SQS polling settings
- graceful shutdown logging
- message deletion only after successful processing
- retry-through-SQS semantics on failures

KEDA manifests are scaffolded but disabled by default so queue-based autoscaling can be enabled later without moving the consumer out of the service.

## What Remains After Phase B

- install and tune the KEDA operator before enabling queue autoscaling
- introduce GitOps or ArgoCD for continuous delivery
- add cluster-level metrics and tracing integrations if an observability stack is adopted
