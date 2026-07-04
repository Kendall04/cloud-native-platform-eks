# Platform Services Readiness Checkpoint

Date: 2026-07-04T01:39:42Z

Branch: `chore/platform-services-readiness`

AWS account: `145023118802`

Region: `us-east-1`

## Goal

Prepare a future deployment of the `platform-services` Helm release without installing workloads.

This checkpoint reviewed the chart, dev values, AWS runtime dependencies, image availability, Kubernetes cluster state, secrets/config strategy, migration jobs, and ALB readiness.

No workloads were installed.

## Cluster And Addons Status

EKS cluster:

- Name: `logistics-platform-dev`
- Status: `ACTIVE`
- Kubernetes: `1.35`
- Endpoint private access: enabled
- Endpoint public access: disabled
- VPC: `vpc-0fe33938202034387`
- OIDC provider: present

Node groups:

- `api-node-group`
- `worker-node-group`

Managed EKS addons:

- `aws-ebs-csi-driver`
- `coredns`
- `kube-proxy`
- `vpc-cni`

Private management path:

- Management EC2: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- Public IP: none
- SSM: Online

Read-only Kubernetes validation from the management EC2 succeeded:

- 3 nodes `Ready`
- `cluster-addons` Helm release is `deployed`
- AWS Load Balancer Controller deployment is `2/2`
- Cluster Autoscaler deployment is `2/2`
- No application ingress exists
- No application services or workloads exist

## Chart And Values

Chart path:

- `k8s/charts/platform-services`

Dev values path:

- `k8s/environments/dev/platform-services.values.yaml`

Rendered namespace:

- `apps`

Current dev values set:

- `namespace.create: false`
- `namespace.name: apps`
- `global.ingress.enabled: true`
- ALB ingress class: `alb`
- ALB scheme: `internal`
- ALB target type: `ip`
- ALB health check path: `/health`
- ALB load balancer name: `cloud-native-platform-dev`

Important issue:

- The `apps` namespace does not exist in the cluster.
- Because `namespace.create` is `false`, a future install would need the namespace created first or the values changed.

## Services Expected

The chart includes three enabled services:

- `auth-service`
- `shipment-service`
- `tracking-service`

Each service renders:

- ServiceAccount
- ConfigMap
- ClusterIP Service
- Deployment
- Migration Job
- PodDisruptionBudget

Ingress paths:

- `auth-service`: `/auth`, `/admin/users`
- `shipment-service`: `/shipments`, `/admin/shipments`
- `tracking-service`: `/tracking`, `/admin/tracking-events`

All services listen on port `8080` and expose `/health`.

## Microservices Runtime

The microservices are .NET 8 services.

Docker metadata:

- `microservices/auth-service/Dockerfile`
- `microservices/shipment-service/Dockerfile`
- `microservices/tracking-service/Dockerfile`

All Dockerfiles expose `8080`.

Migration behavior:

- `auth-service`: supports `--migrate`
- `shipment-service`: supports `--migrate`
- `tracking-service`: supports `--migrate`

The Helm chart renders pre-install/pre-upgrade migration Jobs for all three services.

## ECR And Images

ECR repositories exist in the active account:

- `145023118802.dkr.ecr.us-east-1.amazonaws.com/auth-service`
- `145023118802.dkr.ecr.us-east-1.amazonaws.com/shipment-service`
- `145023118802.dkr.ecr.us-east-1.amazonaws.com/tracking-service`

Image mutability:

- Repositories are `IMMUTABLE`.

Current image availability:

- `auth-service`: 0 images
- `shipment-service`: 0 images
- `tracking-service`: 0 images

Current dev values issue:

- Image repositories still point to account `795708473882`.
- Image tags are `bootstrap-20260328-011814`.
- Image digests are empty.

The deploy workflow requires exact image digests for manual deployment, so workload install is blocked until images are built, pushed, and dev values are updated with account-correct repositories and digests.

## AWS Runtime Dependencies

RDS:

- DB identifier: `cloud-native-platform-dev-postgres`
- Status: `available`
- Engine: PostgreSQL `15.18`
- Publicly accessible: false
- Storage encrypted: true
- Endpoint exists in `us-east-1`
- RDS managed master user secret ARN exists

SQS:

- `cloud-native-platform-dev-shipment-events-queue`
- `cloud-native-platform-dev-shipment-events-dlq`
- Additional dev queues and DLQs exist for analytics, notification, jobs, and events.

EventBridge:

- Bus name: `cloud-native-platform-dev-bus`
- Bus ARN: `arn:aws:events:us-east-1:145023118802:event-bus/cloud-native-platform-dev-bus`

ECR:

- Required service repositories exist.
- Required service images do not exist yet.

EKS/addons:

- EKS is healthy.
- AWS Load Balancer Controller is healthy.
- Cluster Autoscaler is healthy.

## IAM And IRSA

The `infra/live/dev/iam` stack defines service IRSA roles for:

- `cloud-native-platform-dev-shipment-service-irsa`
- `cloud-native-platform-dev-tracking-service-irsa`

Expected permissions:

- `shipment-service`: SQS consume permissions for the shipment events queue and EventBridge `PutEvents`.
- `tracking-service`: EventBridge `PutEvents`.

Current AWS state:

- The service IRSA roles are not present yet.
- The `iam` stack has not been applied.

Current dev values issue:

- `shipment-service` and `tracking-service` service account annotations point to account `795708473882`.
- They must point to account `145023118802` after the `iam` stack is applied.

`auth-service` currently has `automountServiceAccountToken: false` and no AWS pod access.

## Secrets And Config Strategy

The chart expects an existing Kubernetes Secret:

- `platform-runtime-secrets`

Expected keys:

- `auth-connection-string`
- `auth-jwt-secret`
- `auth-bootstrap-admin-email`
- `auth-bootstrap-admin-password`
- `shipment-connection-string`
- `tracking-connection-string`
- `platform-trusted-proxy-secret`
- `platform-internal-service-secret`

Current cluster state:

- Namespace `apps` does not exist.
- Secret `platform-runtime-secrets` does not exist.

The repository does not currently render an ExternalSecret, Secrets Store CSI resource, or SecretProviderClass for these app runtime secrets.

Recommendation:

- Do not hardcode secrets into values.
- Add an explicit secret provisioning phase before workload install.
- Prefer a controlled path using AWS Secrets Manager or a one-time Kubernetes Secret creation runbook that never commits secret values.

## Helm Lint And Template

Command:

```bash
helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml
helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace platform --debug
```

Result:

- Lint: passed.
- Template: passed.

Rendered resources:

- 3 PodDisruptionBudgets
- 3 ServiceAccounts
- 3 ConfigMaps
- 3 ClusterIP Services
- 3 Deployments
- 1 Ingress
- 3 migration Jobs

Rendered blockers:

- Images point to account `795708473882`.
- IRSA annotations point to account `795708473882`.
- Shipment SQS URL points to account `795708473882`.
- Image digests are empty.
- `platform-runtime-secrets` is referenced but not rendered.
- Namespace `apps` is referenced but not created by dev values.

## Ingress And ALB Readiness

Rendered Ingress:

- Name: `platform-services`
- Namespace: `apps`
- Class: `alb`
- Scheme: internal
- Target type: `ip`
- Listener: HTTP 80
- Health check path: `/health`
- Health check port: traffic-port
- Success codes: `200-399`
- Load balancer name: `cloud-native-platform-dev`

AWS Load Balancer Controller readiness:

- Deployment: `2/2`
- Pods: Running/Ready
- Logs show normal startup, webhook registration, leader election, and controllers running.

ALB install readiness:

- Controller is ready.
- Ingress is structurally valid.
- No ALB should be created until workload install.
- API Gateway integration must wait until the internal ALB exists and is validated.

## Blockers

Image blocker:

- ECR repos exist but contain no images.

Digest/tag blocker:

- Dev values contain empty digests and bootstrap tags from an old account.
- Deploy workflow requires exact digests.

Config blocker:

- Dev values still reference old account `795708473882` for ECR, IRSA, and SQS.

Secret blocker:

- `platform-runtime-secrets` does not exist.
- Secret provisioning strategy is not implemented in Kubernetes manifests.

Namespace blocker:

- `apps` namespace does not exist and dev values do not create it.

IRSA blocker:

- Service IRSA roles for shipment/tracking are defined in Terraform but not applied.

Migration blocker:

- Migration Jobs render correctly, but cannot run until images and secrets exist.

RDS connectivity blocker:

- RDS is available and private, but connection strings must be generated and stored securely before install.

ALB/Ingress blocker:

- ALB controller is ready, but Ingress should not be created until services are deployable.

## Recommendation

Do not proceed to workload install yet.

Recommended next phase:

`Fase 2.4 - Platform services image build and runtime values preparation`

Suggested scope:

- Build and push images for `auth-service`, `shipment-service`, and `tracking-service`.
- Capture immutable image digests.
- Apply or plan/apply the `iam` stack for service IRSA roles.
- Generate account-correct dev values for:
  - ECR repositories and digests.
  - Shipment SQS URL.
  - EventBridge bus name.
  - IRSA role ARNs.
- Decide namespace creation strategy for `apps`.
- Decide and implement runtime secret provisioning without committing secret values.
- Render `platform-services` again with corrected values.

Only after those blockers are resolved should a later phase install `platform-services`.

## Non-Actions

This phase did not execute:

- `helm upgrade --install`
- `kubectl apply`
- `kubectl delete`
- Terraform/Terragrunt apply
- Terraform/Terragrunt destroy
- API Gateway apply
- Lambda apply
- `apigateway-core` apply
- `apigateway-integration` apply
- App workload deployment
- Endpoint public access changes
- Git push
