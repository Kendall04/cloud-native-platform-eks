# Platform Services Runtime Fix

Date: 2026-07-04

Branch: `fix/platform-services-runtime`

AWS account: `145023118802`

## Scope

Fase 2.9 validated the installed `platform-services` runtime, fixed the AWS SDK/IRSA runtime issue, rebuilt only affected images, upgraded the Helm release through the private management path, and reduced management access back to read-only.

No API Gateway, Lambda, `apigateway-core`, or `apigateway-integration` resources were applied.

## Initial Runtime State

The private management EC2 instance `i-0417796819b2e0f46` was used through SSM.

Initial state:

- Helm release `platform-services` was deployed in namespace `apps`.
- Release revision was `1`.
- `auth-service`, `shipment-service`, and `tracking-service` deployments existed.
- Runtime Secret `platform-runtime-secrets` existed with 8 keys.
- Secret validation listed key names only; values were not read, decoded, printed, or committed.
- API Gateway and Lambda project resources were absent.

## Initial Diagnostics

Sanitized logs showed:

- `shipment-service` failed to poll SQS because `AWSSDK.SecurityToken` was missing at runtime.
- `tracking-service` used AWS SDK/EventBridge and IRSA, so it had the same latent dependency requirement.
- `auth-service` does not use IRSA/AWS SDK, but one log tail still showed an earlier PostgreSQL health-check warning.

No secret values were printed in logs or command output.

## Root Cause

AWS SDK credential resolution for IRSA requires STS support at runtime. `shipment-service` and `tracking-service` had AWS SDK packages for SQS/EventBridge, but did not include `AWSSDK.SecurityToken`.

After adding STS support, `shipment-service` exposed a second runtime issue: AWS SDK v4 can return `ReceiveMessageResponse.Messages` as `null` for an empty poll. The consumer iterated the property directly, causing a `NullReferenceException` and host shutdown when the queue was empty.

## Code Changes

Commits created locally:

- `e482a4e fix(app): add AWS STS dependency for IRSA workloads`
- `1b746fc fix(app): keep shipment consumer alive on empty polls`

Files changed by code commits:

- `microservices/shipment-service/src/ShipmentService.Infrastructure/ShipmentService.Infrastructure.csproj`
- `microservices/tracking-service/src/TrackingService.Infrastructure/TrackingService.Infrastructure.csproj`
- `microservices/shipment-service/src/ShipmentService.Infrastructure/Messaging/SqsTrackingEventsConsumer.cs`

Package added:

- `AWSSDK.SecurityToken` `4.0.100.2`

Services affected:

- `shipment-service`
- `tracking-service`

`auth-service` was not rebuilt or changed.

## Tests

Tests executed:

- `dotnet test microservices/auth-service/AuthService.sln --configuration Release`: 4 passed
- `dotnet test microservices/shipment-service/ShipmentService.sln --configuration Release`: 7 passed
- `dotnet test microservices/tracking-service/TrackingService.sln --configuration Release`: 5 passed

Warnings were limited to existing nullable/EF raw SQL/SQS obsolete API warnings.

## Images

No `latest` tags were used.

Images rebuilt and pushed:

| Service | Tag | Digest |
| --- | --- | --- |
| `tracking-service` | `e482a4e44f98` | `sha256:6608af769cc13247b593afaec082629dbdbf86460e9e2e173b499443bae9a6af` |
| `shipment-service` | `1b746fcc644f` | `sha256:f0e0368985c20a4765bbd20768355f58983408ea1825f4ed2b832d3a1abb8968` |

`auth-service` remained on:

- tag `1d9604a7ef25`
- digest `sha256:d6474872d5dd2db4394c6fd09be590e68600f47783fb75622ef2f66bb8170ab1`

## Values

Updated:

- `k8s/environments/dev/platform-services.values.yaml`

Changes:

- `shipment-service` tag/digest updated to the final empty-poll fix image.
- `tracking-service` tag/digest updated to the STS dependency image.
- `auth-service` tag/digest unchanged.
- `release.commitSha` intentionally stayed `1d9604a7ef25` to avoid an unnecessary global rollout.

Validation:

- no old account `795708473882`
- no empty digests
- images render as `repository@sha256:...`
- ServiceAccount IRSA annotations remain intact for shipment/tracking
- `auth-service` remains without IRSA

## Artifacts

Final artifact path:

- `s3://cloud-native-platform-145023118802-dev-us-east-1-artifacts/cluster-addons/dev/platform-services-runtime-fix/20260704T161448Z-1b746fcc644f/`

Files:

- `platform-services-0.1.0.tgz`
- `values.yaml`
- `rendered.yaml`

No secrets were uploaded.

## Temporary Upgrade Access

Management role was temporarily changed from:

- `AmazonEKSAdminViewPolicy`

to:

- `AmazonEKSEditPolicy`
- namespace scope: `apps`

Plan/apply scope:

- only `infra/live/dev/management`
- only `aws_eks_access_policy_association.view`
- no EC2 replacement
- no SG changes
- no EKS endpoint changes
- no Secrets Manager/RDS permissions

Elevation apply result:

- `1 added`
- `0 changed`
- `1 destroyed`

Final elevation plan:

- `No changes`

## Helm Upgrade

Upgrade was executed from management EC2 via SSM using the uploaded artifacts.

Final command summary:

- `helm upgrade platform-services <chart> -n apps -f values.yaml --wait --timeout 15m --atomic`

Final result:

- release: `platform-services`
- namespace: `apps`
- status: `deployed`
- revision: `3`
- chart: `platform-services-0.1.0`

The first upgrade to the STS dependency image reached revision `2`, but `shipment-service` then revealed the empty-poll `NullReferenceException`. The final upgrade to revision `3` included the empty-poll fix.

## Runtime Validation

Final Kubernetes state:

- `auth-service`: deployment `2/2`
- `shipment-service`: deployment `2/2`
- `tracking-service`: deployment `2/2`
- all pods Running/Ready
- services remain `ClusterIP` on port `8080`
- Ingress `platform-services` remains present with class `alb`
- migration hook Jobs completed and were cleaned up by hook policy

ServiceAccounts:

- `shipment-service` has `cloud-native-platform-dev-shipment-service-irsa`
- `tracking-service` has `cloud-native-platform-dev-tracking-service-irsa`
- `auth-service` has no IRSA, expected

Secret validation:

- only Secret metadata and key names were listed
- no values were read, decoded, printed, or committed

## Post-Fix Logs

Sanitized post-fix logs showed:

- no `AWSSDK.SecurityToken` load error
- no STS credential resolution error
- no SQS credential error
- no EventBridge credential error
- `shipment-service` starts its SQS consumer and remains running on empty polls
- `tracking-service` starts normally

Observed warning:

- one older `auth-service` log tail still showed a PostgreSQL health-check warning
- the deployment remained available and ALB targets were healthy

This should remain a follow-up runtime observation rather than a blocker for this fix.

## ALB And Targets

ALB:

- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- scheme: `internal`
- state: `active`
- VPC: `vpc-0fe33938202034387`

Target groups:

| Target group | Target type | Health check | Final health |
| --- | --- | --- | --- |
| `k8s-apps-authserv-58a4b90ba7` | `ip` | `/health` | 2 healthy |
| `k8s-apps-shipment-fb67f1d90f` | `ip` | `/health` | 2 healthy |
| `k8s-apps-tracking-9810f2e09e` | `ip` | `/health` | 2 healthy |

## Permission Reduction

After the Helm upgrade, management access was reduced back to read-only.

Reduction plan:

- `1 to add`
- `0 to change`
- `1 to destroy`
- only EKS access policy association replacement

Reduction apply:

- `1 added`
- `0 changed`
- `1 destroyed`

Final plan:

- `No changes`

Final management role state:

- `AmazonEKSAdminViewPolicy` associated
- no `AmazonEKSEditPolicy`
- no `AmazonEKSClusterAdminPolicy`
- no `secretsmanager:GetSecretValue`
- no `secretsmanager:DescribeSecret`
- no `rds:DescribeDBInstances`
- no `secretsmanager:*`
- no `rds:*`

Remaining management role permissions:

- S3 artifacts read-only
- `eks:DescribeCluster`
- `AmazonSSMManagedInstanceCore`

## API Gateway And Lambda

Confirmed empty for project resources:

- API Gateway
- Lambda

`apigateway-integration` was not applied.

## Evidence

Local evidence is stored under:

- `/tmp/cloud-native-platform-runtime-fix/`

Important files:

- `shipment-service-image.json`
- `tracking-service-image.json`
- `shipment-service-empty-poll-image.json`
- `platform-services-runtime-fix-rendered-final.yaml`
- `management-elevate-plan.log`
- `management-elevate-apply.log`
- `management-elevate-final-plan.log`
- `management-reduce-plan.log`
- `management-reduce-apply.log`
- `management-reduce-final-plan.log`
- `final-eks-access-policies.json`
- `final-management-policy-docs.txt`
- `alb-load-balancers.txt`
- `alb-target-groups.txt`
- `alb-target-health-final.txt`
- `apigateway-apis.json`
- `lambda-functions.json`

## Not Performed

- No `kubectl apply`
- No `kubectl delete`
- No Terraform/Terragrunt applies outside `infra/live/dev/management`
- No Docker build/push for unaffected services
- No API Gateway/Lambda apply
- No EKS endpoint change
- No SG change
- No Kubernetes Secret value reads
- No Secret base64 decoding
- No Git push

## Recommendation

Proceed to `Fase 2.10 — Internal platform API smoke tests and ALB readiness evidence`.

Recommended scope:

- test internal ALB routes from the private path
- call service health endpoints and selected API endpoints through ALB routing
- verify auth, shipment, and tracking basic runtime behavior without exposing secrets
- validate shipment/tracking EventBridge/SQS paths with controlled non-secret test events if safe
- investigate the remaining auth/Postgres health warning if it persists
- keep API Gateway/Lambda unapplied until internal runtime behavior is proven
