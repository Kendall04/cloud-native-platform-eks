# Runtime Namespace, Secrets, And IRSA Readiness

Date: 2026-07-04T04:34:00Z

Branch: `chore/runtime-readiness`

AWS account: `145023118802`

Region: `us-east-1`

## Goal

Prepare the runtime path for a future `platform-services` install without creating mutable Kubernetes resources, applying IAM, or deploying workloads.

This checkpoint inspected:

- Kubernetes namespace and Secret state.
- `platform-services` chart runtime requirements.
- Application runtime configuration.
- Service IRSA readiness.
- AWS runtime dependencies.
- Helm render output with real image digests.

No runtime resource was created.

## Cluster And Management Path

EKS:

- Cluster: `logistics-platform-dev`
- Status: healthy by read-only `kubectl` from the private management EC2.
- Nodes: 3 `Ready`.
- EKS public endpoint: disabled.

Management EC2:

- Instance ID: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- Public IP: none
- IAM instance profile: `cloud-native-platform-dev-management-profile`
- Security group: `cloud-native-platform-dev-management-sg`

Cluster addons:

- Helm release `cluster-addons` is deployed in `kube-system`.
- AWS Load Balancer Controller deployment is `2/2`.
- Cluster Autoscaler deployment is `2/2`.
- System pods are running.

Application state:

- `platform-services` Helm release is not installed.
- No application ingress exists.
- No application services or workloads exist.

## Namespace And Secret State

Current Kubernetes state:

- Namespace `apps`: not found.
- Secret `platform-runtime-secrets`: not found.

This is expected for this readiness phase.

## Platform Services Chart Runtime Summary

Chart path:

- `k8s/charts/platform-services`

Dev values path:

- `k8s/environments/dev/platform-services.values.yaml`

Rendered namespace:

- `apps`

Namespace behavior:

- The chart has `templates/namespace.yaml`.
- Dev values currently set `namespace.create: false`.
- A future phase must either pre-create `apps` or intentionally switch values to let the chart create it.

Rendered service accounts:

- `auth-service`
- `shipment-service`
- `tracking-service`

IRSA annotations:

- `auth-service`: none; `automountServiceAccountToken: false`.
- `shipment-service`: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-shipment-service-irsa`.
- `tracking-service`: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-tracking-service-irsa`.

Rendered runtime resources:

- ServiceAccounts
- ConfigMaps
- ClusterIP Services
- Deployments
- Migration Jobs
- PodDisruptionBudgets
- Internal ALB Ingress

Image references render by immutable digest:

- `auth-service`: `145023118802.dkr.ecr.us-east-1.amazonaws.com/auth-service@sha256:d6474872d5dd2db4394c6fd09be590e68600f47783fb75622ef2f66bb8170ab1`
- `shipment-service`: `145023118802.dkr.ecr.us-east-1.amazonaws.com/shipment-service@sha256:fe6e919ac6a5abee433050bde9931bb2b74057f1fa649c291dbc929118bee941`
- `tracking-service`: `145023118802.dkr.ecr.us-east-1.amazonaws.com/tracking-service@sha256:dda2e02c76f7d247de8ff51a1a6268bcc3eecd93e65d37216655641f30379265`

## Application Runtime Requirements

`auth-service`:

- Requires `ConnectionStrings__Postgres`.
- Requires `Jwt__Secret`.
- Requires bootstrap admin email and password if bootstrap admin should be created.
- Uses non-secret config for issuer, audience, expiration, DB schema, and ASP.NET environment.
- Supports explicit migration mode with `--migrate`.
- Health check endpoint is exposed through the chart probes.

`shipment-service`:

- Requires `ConnectionStrings__Postgres`.
- Requires `PlatformAuth__TrustedProxySecret`.
- Requires `PlatformAuth__InternalServiceSecret`.
- Uses non-secret config for AWS region, EventBridge bus name, SQS queue URL, DB schema, consumer settings, and ASP.NET environment.
- Consumes SQS shipment events and publishes EventBridge events.
- Supports explicit migration mode with `--migrate`.
- Needs IRSA for SQS receive/delete/visibility and EventBridge `PutEvents`.

`tracking-service`:

- Requires `ConnectionStrings__Postgres`.
- Requires `PlatformAuth__TrustedProxySecret`.
- Requires `PlatformAuth__InternalServiceSecret`.
- Uses non-secret config for AWS region, EventBridge bus name, DB schema, shipment service base URL, and ASP.NET environment.
- Publishes EventBridge events.
- Supports explicit migration mode with `--migrate`.
- Needs IRSA for EventBridge `PutEvents`.

## IAM Stack Inspection

Stack:

- `infra/live/dev/iam`

Module:

- `infra/modules/iam`

Expected service IRSA roles:

- `cloud-native-platform-dev-shipment-service-irsa`
- `cloud-native-platform-dev-tracking-service-irsa`

Service account subjects:

- `system:serviceaccount:apps:shipment-service`
- `system:serviceaccount:apps:tracking-service`

Trust:

- Uses the EKS OIDC provider for `logistics-platform-dev`.
- Audience condition: `sts.amazonaws.com`.

Policies:

- `shipment-service` inline policy:
  - `sqs:ReceiveMessage`
  - `sqs:DeleteMessage`
  - `sqs:GetQueueAttributes`
  - `sqs:ChangeMessageVisibility`
  - `events:PutEvents`
- `tracking-service` inline policy:
  - `events:PutEvents`

Resources:

- SQS shipment events queue ARN.
- EventBridge dev bus ARN.

Current AWS state:

- `cloud-native-platform-dev-shipment-service-irsa`: not found.
- `cloud-native-platform-dev-tracking-service-irsa`: not found.

Module outputs:

- `role_arns`
- `role_names`

Note:

- The `iam` stack also plans `cloud-native-platform-dev-ec2-ssm` with `AmazonSSMManagedInstanceCore`. This is additive IAM scope, but it is not directly required for `platform-services` runtime. Review whether to keep it before applying the stack.

## IAM Plan Result

Command:

- `terragrunt init`
- `terragrunt plan -no-color`

Logs:

- `/tmp/cloud-native-platform-runtime-readiness/iam-init.log`
- `/tmp/cloud-native-platform-runtime-readiness/iam-plan.log`

Result:

- Init: OK
- Plan: OK
- `6 to add, 0 to change, 0 to destroy`

Resources planned:

- 3 IAM roles:
  - `cloud-native-platform-dev-ec2-ssm`
  - `cloud-native-platform-dev-shipment-service-irsa`
  - `cloud-native-platform-dev-tracking-service-irsa`
- 2 inline IAM role policies:
  - `shipment-service`
  - `tracking-service`
- 1 managed policy attachment:
  - `AmazonSSMManagedInstanceCore` for `cloud-native-platform-dev-ec2-ssm`

No destroy or replacement was planned.

The plan appears safe for a future IAM-only apply if the extra EC2 SSM role is accepted as in-scope.

## AWS Runtime Dependencies

RDS:

- DB identifier: `cloud-native-platform-dev-postgres`
- Status: `available`
- Engine: PostgreSQL `15.18`
- Publicly accessible: false
- Storage encrypted: true
- Master username: `platform_admin`
- Endpoint exists in `us-east-1`
- Security group: `sg-004039e0daea1a704`
- Master user secret ARN is available from Terraform output.

SQS:

- `cloud-native-platform-dev-shipment-events-queue`
- `cloud-native-platform-dev-shipment-events-dlq`
- Additional dev queues and DLQs exist for analytics, events, jobs, and notification.

EventBridge:

- Bus: `cloud-native-platform-dev-bus`
- ARN: `arn:aws:events:us-east-1:145023118802:event-bus/cloud-native-platform-dev-bus`

ECR:

- `auth-service` image exists with tag `1d9604a7ef25`.
- `shipment-service` image exists with tag `1d9604a7ef25`.
- `tracking-service` image exists with tag `1d9604a7ef25`.
- All three digests match the dev values file.

Outputs captured locally:

- `/tmp/cloud-native-platform-runtime-readiness/rds-outputs.json`
- `/tmp/cloud-native-platform-runtime-readiness/sqs-outputs.json`
- `/tmp/cloud-native-platform-runtime-readiness/eventbridge-outputs.json`
- `/tmp/cloud-native-platform-runtime-readiness/ecr-outputs.json`
- `/tmp/cloud-native-platform-runtime-readiness/eks-outputs.json`

## Secrets Strategy

The chart expects one Kubernetes Secret:

- `platform-runtime-secrets`

Required keys:

| Secret key | Consumers | Recommended source | Required |
| --- | --- | --- | --- |
| `auth-connection-string` | `auth-service` deployment and migration job | RDS endpoint + managed DB credentials | Yes |
| `shipment-connection-string` | `shipment-service` deployment and migration job | RDS endpoint + managed DB credentials | Yes |
| `tracking-connection-string` | `tracking-service` deployment and migration job | RDS endpoint + managed DB credentials | Yes |
| `auth-jwt-secret` | `auth-service` deployment and migration job | New high-entropy runtime secret | Yes |
| `auth-bootstrap-admin-email` | `auth-service` deployment and migration job | Operator-provided bootstrap value | Required only if bootstrap admin should be created |
| `auth-bootstrap-admin-password` | `auth-service` deployment and migration job | Operator-provided high-entropy bootstrap value | Required only if bootstrap admin should be created |
| `platform-trusted-proxy-secret` | `shipment-service`, `tracking-service` | Shared high-entropy value matching API Gateway/authorizer integration | Yes before external traffic |
| `platform-internal-service-secret` | `shipment-service`, `tracking-service` | Shared high-entropy internal service value | Yes |

Recommended bootstrap method for a later phase:

- Use the private management EC2 through SSM.
- Pass secret values as temporary environment variables or retrieve them from AWS Secrets Manager.
- Create the Kubernetes Secret with `kubectl create secret generic ... --from-literal=...`.
- Do not commit secret values.
- Do not print secret values in logs.

Future improvement:

- Consider External Secrets Operator or Secrets Store CSI Driver for AWS Secrets Manager integration. This should be designed explicitly in a later phase, not introduced silently before the first workload install.

## Namespace Strategy

Recommended short-term approach:

- Pre-create namespace `apps` in a controlled bootstrap phase before workload install.
- Keep `namespace.create: false` in `platform-services.values.yaml`.

Reasoning:

- Namespace lifecycle is a cluster operations concern.
- Pre-creating the namespace allows secrets, service account checks, labels, and policies to be prepared before Helm install.

Alternative:

- Set `namespace.create: true` for Helm-owned namespace creation. This is simpler, but it couples namespace lifecycle to the workload release and still does not solve secrets ordering.

## Migration Strategy

The chart renders migration Jobs:

- `auth-service-migrate`
- `shipment-service-migrate`
- `tracking-service-migrate`

Each job runs the service image with `--migrate`.

Ordering consideration:

- All three services use the same RDS database with distinct schemas.
- Migration jobs require the runtime Secret before install.
- Migration jobs should complete before Deployments are considered healthy.
- Workload install phase should use Helm wait/timeout and inspect failed jobs if any migration fails.

## Render Result

Command:

- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace apps --debug`

Result:

- Helm lint: OK
- Helm template: OK

Rendered manifest:

- `/tmp/cloud-native-platform-runtime-readiness/platform-services-runtime-rendered.yaml`

Rendered resources:

- 3 PodDisruptionBudgets
- 3 ServiceAccounts
- 3 ConfigMaps
- 3 Services
- 3 Deployments
- 1 Ingress
- 3 migration Jobs

Rendered readiness:

- Service accounts and IRSA annotations are present for `shipment-service` and `tracking-service`.
- Secret references point to `platform-runtime-secrets`.
- Ingress is internal ALB with target type `ip`.
- Images render by digest.
- Probes and resource blocks render.

Suspicious scan:

- No old account `795708473882`.
- No empty image digest.
- No AWS access key pattern.
- The string `password` appears only as a secret key name, not a secret value.

## Blockers

IAM/IRSA blocker:

- Service IRSA roles do not exist yet.
- `iam` plan is clean but has not been applied.
- The planned EC2 SSM role should be reviewed for scope before applying `iam`.

Namespace blocker:

- Namespace `apps` does not exist.

Secrets blocker:

- `platform-runtime-secrets` does not exist.
- Runtime secret creation strategy has not been executed.

Runtime config blocker:

- RDS connection strings must be constructed securely without printing passwords.
- Shared proxy/internal secrets must be generated or sourced securely.

Migration blocker:

- Migration Jobs will fail until namespace and runtime Secret exist.

ALB/API Gateway blocker:

- ALB will not exist until `platform-services` Ingress is installed and reconciled.
- API Gateway integration must remain blocked until the internal ALB exists and is validated.

## Recommendation

Proceed to Fase 2.6 only as an IAM-focused phase:

- Reconfirm `iam` plan.
- Decide whether `cloud-native-platform-dev-ec2-ssm` is acceptable in the `iam` stack or should be removed before apply.
- Apply only `iam` if the plan remains `6 to add, 0 to change, 0 to destroy`.
- Validate the service IRSA roles and trust policies.
- Do not create namespace, secrets, workloads, API Gateway, Lambda, or API Gateway integration in Fase 2.6.

After IAM is ready, use a later phase for namespace and runtime Secret bootstrap.

## Out Of Scope Confirmation

Not executed:

- Terraform/Terragrunt apply
- Terraform/Terragrunt destroy
- Helm install or upgrade
- `kubectl apply`
- `kubectl create`
- `kubectl delete`
- namespace creation
- Kubernetes Secret creation
- workload deployment
- Docker build
- Docker push
- API Gateway apply
- Lambda apply
- Git push
