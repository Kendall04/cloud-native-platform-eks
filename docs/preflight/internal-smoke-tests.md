# Internal Platform API Smoke Tests

Date: 2026-07-04

Branch: `chore/internal-smoke-tests`

AWS account: `145023118802`

## Scope

Fase 2.10 validated internal platform readiness before API Gateway/Lambda by using read-only checks and non-destructive HTTP smoke attempts from the private management EC2 path.

No infrastructure, Kubernetes, Helm, API Gateway, Lambda, Docker, or secret changes were made.

## Runtime State

Management EC2:

- instance: `i-0417796819b2e0f46`
- private IP: `10.0.134.40`
- public IP: none
- IAM instance profile: `cloud-native-platform-dev-management-profile`
- security group: `sg-0980b6aa338f1ef83`

Runtime state before and after smoke:

- `cluster-addons` deployed in `kube-system`
- `platform-services` deployed in `apps`
- release revision: `3`
- `auth-service`: deployment `2/2`, pods Running/Ready
- `shipment-service`: deployment `2/2`, pods Running/Ready
- `tracking-service`: deployment `2/2`, pods Running/Ready
- Services are `ClusterIP` on port `8080`
- Ingress `platform-services` is present with class `alb`
- Secret `platform-runtime-secrets` exists with 8 keys
- Secret validation listed metadata/key names only

Secret keys validated by name only:

- `auth-bootstrap-admin-email`
- `auth-bootstrap-admin-password`
- `auth-connection-string`
- `auth-jwt-secret`
- `platform-internal-service-secret`
- `platform-trusted-proxy-secret`
- `shipment-connection-string`
- `tracking-connection-string`

No Secret values were read, decoded, printed, or committed.

## Routes Discovered

Ingress:

- name: `platform-services`
- namespace: `apps`
- class: `alb`
- address: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`

Ingress paths and backends:

| Path | Backend |
| --- | --- |
| `/auth` | `auth-service:8080` |
| `/admin/users` | `auth-service:8080` |
| `/shipments` | `shipment-service:8080` |
| `/admin/shipments` | `shipment-service:8080` |
| `/tracking` | `tracking-service:8080` |
| `/admin/tracking-events` | `tracking-service:8080` |

Service-local health endpoint:

- `/health`

Swagger route prefixes from service code:

- `auth/swagger`
- `shipments/swagger`
- `tracking/swagger`

The Ingress does not currently publish a top-level `/health` path.

## ALB Baseline

ALB:

- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- name: `cloud-native-platform-dev`
- scheme: `internal`
- state: `active`
- type: `application`
- VPC: `vpc-0fe33938202034387`

Target groups:

| Target group | Target type | Health check | Matcher | Baseline health |
| --- | --- | --- | --- | --- |
| `k8s-apps-authserv-58a4b90ba7` | `ip` | `/health` | `200-399` | 2 healthy |
| `k8s-apps-shipment-fb67f1d90f` | `ip` | `/health` | `200-399` | 2 healthy |
| `k8s-apps-tracking-9810f2e09e` | `ip` | `/health` | `200-399` | 2 healthy |

## ALB Smoke From Management EC2

Smoke source:

- management EC2 `i-0417796819b2e0f46`

ALB:

- `http://internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`

All HTTP attempts from management EC2 to the internal ALB timed out at TCP connect and returned `HTTP 000`.

Bounded smoke results:

| Path | HTTP code | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/health` | `000` | `0` | connection timed out before routing |
| `/auth/swagger` | `000` | `0` | connection timed out before routing |
| `/auth/swagger/v1/swagger.json` | `000` | `0` | connection timed out before routing |
| `/shipments/swagger` | `000` | `0` | connection timed out before routing |
| `/shipments/swagger/v1/swagger.json` | `000` | `0` | connection timed out before routing |
| `/tracking/swagger` | `000` | `0` | connection timed out before routing |
| `/tracking/swagger/v1/swagger.json` | `000` | `0` | connection timed out before routing |
| `/auth/me` | `000` | `0` | connection timed out before routing |
| `/shipments` | `000` | `0` | connection timed out before routing |
| `/tracking/00000000-0000-0000-0000-000000000000` | `000` | `0` | connection timed out before routing |

The longer smoke command was cancelled after repeated timeouts to avoid leaving SSM work running. It had also produced only `HTTP 000` timeout results before cancellation.

No response bodies with sensitive data were printed.

## Direct Service Smoke From Management EC2

ClusterIP direct health checks were attempted from management EC2 without creating debug pods.

Results:

| Service | ClusterIP | Path | Result |
| --- | --- | --- | --- |
| `auth-service` | `172.20.246.22` | `/health` | `HTTP 000`, connection timed out |
| `shipment-service` | `172.20.191.162` | `/health` | `HTTP 000`, connection timed out |
| `tracking-service` | `172.20.180.25` | `/health` | `HTTP 000`, connection timed out |

This is expected for ClusterIP access from a standalone EC2 host outside the node network path.

## Connectivity Finding

The ALB and targets are healthy, but management EC2 cannot establish HTTP connections to the ALB.

Read-only SG inspection found:

- management SG `sg-0980b6aa338f1ef83` allows egress only for:
  - TCP/443 to `0.0.0.0/0`
  - TCP/53 to `10.0.0.0/16`
  - UDP/53 to `10.0.0.0/16`
- ALB listens on HTTP/80.
- ALB SG `sg-079c5a1e99c17270a` allows TCP/80 ingress.

Likely blocker:

- management EC2 egress does not allow TCP/80 to the internal ALB.

No SG changes were made in this phase.

## Logs Post-Smoke

Sanitized logs were reviewed after smoke attempts.

Findings:

- `shipment-service` starts its SQS consumer and remains running.
- no `AWSSDK.SecurityToken` error appeared.
- no STS/IRSA credential errors appeared.
- no SQS/EventBridge credential errors appeared.
- no `NullReferenceException` from empty SQS polls appeared.
- `tracking-service` starts normally.
- `auth-service` still showed the known PostgreSQL health-check warning in one log tail.

Auth/Postgres observation:

- The warning persists in logs.
- The `auth-service` deployment remains `2/2`.
- ALB auth target group remains 2 healthy.
- Treat as a follow-up runtime observation, not as a deployment blocker.

No secret values were printed in logs.

## ALB After Smoke

ALB remained:

- `internal`
- `active`
- in VPC `vpc-0fe33938202034387`

Target health after smoke:

| Target group | Final health |
| --- | --- |
| `k8s-apps-authserv-58a4b90ba7` | 2 healthy |
| `k8s-apps-shipment-fb67f1d90f` | 2 healthy |
| `k8s-apps-tracking-9810f2e09e` | 2 healthy |

## Management Permissions

Final management role state:

- role: `cloud-native-platform-dev-management-role`
- EKS access policy: `AmazonEKSAdminViewPolicy`
- no `AmazonEKSEditPolicy`
- no `AmazonEKSClusterAdminPolicy`
- no Secrets Manager permissions
- no RDS permissions

Inline policies remain:

- S3 artifacts read-only
- `eks:DescribeCluster`

Attached policy:

- `AmazonSSMManagedInstanceCore`

## API Gateway And Lambda

Read-only checks returned no project resources for:

- API Gateway
- Lambda

`apigateway-core` and `apigateway-integration` were not applied.

## Local Validation

Local validations passed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh || true`
- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ... --namespace apps`
- `helm dependency build` for `cluster-addons`
- `helm lint` for `cluster-addons`
- `helm template` for `cluster-addons`

## Secret Review

No files matching these patterns were found:

- `*.env`
- `*.pem`
- `*.key`
- `*.tfvars`

Repository grep matched expected configuration/key names in IaC, Kubernetes values, examples, and app code. No secret values were printed or added to documentation.

## Evidence

Local evidence:

- `/tmp/cloud-native-platform-internal-smoke/alb-before.txt`
- `/tmp/cloud-native-platform-internal-smoke/target-groups-before.txt`
- `/tmp/cloud-native-platform-internal-smoke/target-health-before.txt`
- `/tmp/cloud-native-platform-internal-smoke/alb-after.txt`
- `/tmp/cloud-native-platform-internal-smoke/target-groups-after.txt`
- `/tmp/cloud-native-platform-internal-smoke/target-health-after.txt`
- `/tmp/cloud-native-platform-internal-smoke/alb-management-sgs.json`
- `/tmp/cloud-native-platform-internal-smoke/secret-file-scan.txt`
- `/tmp/cloud-native-platform-internal-smoke/secret-grep.txt`

SSM command IDs:

- initial runtime state: `bb9fb571-60c1-4218-b7f5-97e3eec533a5`
- ingress discovery: `c08627aa-1829-4661-ad17-abf3971ff3eb`
- long ALB smoke, cancelled after repeated timeouts: `a344827c-47ad-4758-b169-c6bf8f920bc6`
- bounded ALB smoke: `72769a86-a9af-4643-bd30-880726b6d21f`
- direct ClusterIP smoke: `5f0af299-a093-448e-8077-37662088d1c8`
- post-smoke logs: `d73dff3f-831d-4fb8-a790-dd401d5a0e2f`
- final runtime state: `01cd108f-4d16-4026-9c0b-888205cb5182`

## Readiness Decision

Not ready to proceed to API Gateway/Lambda integration yet.

Reason:

- platform workloads are healthy behind the ALB
- ALB targets are healthy
- but the approved private smoke source, management EC2, cannot connect to ALB HTTP/80 due to current egress restrictions
- internal route behavior through the ALB could not be validated with HTTP status responses

## Not Performed

- No Terraform/Terragrunt apply
- No Helm install/upgrade
- No `kubectl apply`
- No `kubectl create`
- No `kubectl delete`
- No Kubernetes Secret changes
- No Kubernetes Secret value reads
- No Secret base64 decoding
- No Docker build/push
- No API Gateway/Lambda apply
- No endpoint public access change
- No IAM/SG changes
- No Git push

## Recommendation

Proceed to `Fase 2.11 — Enable controlled internal ALB smoke path`.

Recommended scope:

- review management SG egress requirements for HTTP/80 to the internal ALB
- decide whether to add a narrow egress rule from management SG to the ALB SG on TCP/80
- apply only the required management/network change if the plan is clean
- rerun ALB smoke GETs from management EC2
- validate expected 200/401/403/404 route behavior
- keep API Gateway/Lambda unapplied until ALB route smoke passes
