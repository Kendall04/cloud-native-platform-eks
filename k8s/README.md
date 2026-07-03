# Kubernetes Runtime Packaging

`k8s/` contains the Phase B deployment packaging for the EKS-hosted services:

- cluster-wide addons required by the platform
- `auth-service`
- `shipment-service`
- `tracking-service`

Notification remains Lambda and is not deployed through these charts.

## Structure

```text
k8s/
  charts/
    cluster-addons/
    platform-services/
  environments/
    dev/
    prod/
  examples/
    platform-runtime-secrets.example.yaml
```

## Cluster Addons

`k8s/charts/cluster-addons` is the shared Helm chart for cluster-wide operators that should not live in the application chart.

It currently installs:

- `aws-load-balancer-controller`
- `cluster-autoscaler`

The chart expects IAM/IRSA prerequisites to already exist from the EKS Terragrunt stack and is normally deployed through:

```bash
./scripts/deploy-cluster-addons.sh
```

## What The Platform Chart Covers

`k8s/charts/platform-services` deploys:

- one `Deployment` per service
- one `Service` per service
- one `ServiceAccount` per service
- per-service `ConfigMap` for non-secret environment variables
- one pre-install / pre-upgrade migration `Job` per service
- a shared internal ALB `Ingress`
- per-service `startupProbe` and `PodDisruptionBudget`
- optional KEDA `ScaledObject` / `TriggerAuthentication` scaffold for `shipment-service`

## Internal Service Communication

`tracking-service` is expected to call `shipment-service` using Kubernetes DNS, for example:

```text
http://shipment-service.apps.svc.cluster.local:8080
```

That value is exposed through Helm values as `ShipmentService__BaseUrl`.

`tracking-service` uses that base URL in two ways:

- `GET /internal/shipments/{id}` for shipment visibility checks
- `GET /internal/shipments/by-tracking/{trackingNumber}` for tracking-number resolution
- `GET /internal/shipments/{id}/exists` for admin-side existence validation during tracking event creation

The shared ingress does not expose `/internal/*`, so that path remains cluster-only.
In non-development environments those endpoints also require `X-Platform-Internal-Secret`.

## Secrets

The chart intentionally does not hardcode runtime secrets.
Use Kubernetes `Secret` objects or an external secrets controller.
ConfigMaps for non-secret runtime configuration are created by the chart from the Helm values.

A placeholder manifest is available at:

- `k8s/examples/platform-runtime-secrets.example.yaml`

## Environment Overlays

Environment-specific release data lives outside the chart so the chart stays reusable and the deployable state stays explicit.

Current overlays:

- `k8s/environments/dev/platform-services.values.yaml`
- `k8s/environments/prod/platform-services.values.yaml`

Each overlay stores:

- release metadata (`release.version`, `release.gitTag`, `release.commitSha`)
- per-service image repository
- per-service image tag for readability
- per-service image digest for the real deployable reference

The Helm chart will deploy `repository@digest` whenever a digest is provided.

## Example Deployment Flow

1. Apply the base Terragrunt infrastructure with `./scripts/deploy.sh`.
2. Deploy the cluster-wide addons:

```bash
./scripts/deploy-cluster-addons.sh
```

3. Build and push images to ECR.
4. Prepare runtime secrets in the `apps` namespace.
5. Update `k8s/environments/<env>/platform-services.values.yaml` with the exact image digest and release metadata for each service.
6. Ensure the runtime secret includes:
   - database connection strings against the shared `platform` database
   - `platform-trusted-proxy-secret`
   - `platform-internal-service-secret`
7. Deploy:

```bash
ENVIRONMENT=dev ./scripts/deploy-platform-services.sh
```

## Ingress Model

The chart prepares a single internal ALB ingress compatible with:

```text
API Gateway -> VPC Link -> ALB -> EKS
```

Implemented path routing:

- `/auth`
- `/admin/users`
- `/shipments`
- `/admin/shipments`
- `/tracking`
- `/admin/tracking-events`

The API Gateway/VPC Link wiring now lives in Terraform/Terragrunt under `infra/live/dev/apigateway-core` and `infra/live/dev/apigateway-integration`.
`cluster-addons` provides the controller that reconciles the ingress, while `platform-services` owns the application-side ingress manifest.

## KEDA

The chart includes an optional KEDA scaffold for `shipment-service`, disabled by default.

What is implemented:

- values structure for SQS queue-based scaling
- `TriggerAuthentication` using EKS pod identity
- `ScaledObject` targeting the `shipment-service` deployment

What remains for activation:

- KEDA operator installation in the cluster
- final queue URL and scaling thresholds review in the target environment

## Observability

All three services already expose `/health`, and the chart configures readiness/liveness probes.
`startupProbe` is enabled so cold starts do not trip liveness too early.
Pod annotations are left configurable for future Prometheus integration.

## Runtime Hardening

Implemented now:

- `shipment-service` gets a longer termination grace period for SQS worker shutdown
- `auth-service` does not mount a service account token by default
- `shipment-service` and `tracking-service` are ready for IRSA-backed pod identity
- `shipment-service` and `tracking-service` trust only API-Gateway-verified identity headers in non-development environments
- migrations run only through explicit `--migrate` jobs and not during normal web-app startup
- `PodDisruptionBudget` resources are generated for the three services

Still intentionally left for later phases:

- KEDA operator installation and tuning before enabling queue-based scaling
- GitOps or ArgoCD reconciliation
- richer metrics scraping objects such as `ServiceMonitor` if a Prometheus operator is added
