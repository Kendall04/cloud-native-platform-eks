# Management ALB Smoke Path

Date: 2026-07-04

Branch: `chore/management-alb-smoke-path`

AWS account: `145023118802`

## Purpose

Enable the private management EC2 host to run HTTP smoke tests against the internal
`platform-services` ALB without broad outbound access.

Previous internal smoke attempts from the management host reached `HTTP 000` with
zero bytes. The runtime and ALB targets were healthy, so the blocker was isolated
to the management host security group egress path.

## Security groups

Management EC2:

- Instance: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- Security group: `sg-0980b6aa338f1ef83`
- Security group name: `cloud-native-platform-dev-management-sg`

Internal ALB:

- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- Security groups:
  - `sg-079c5a1e99c17270a`
  - `sg-0dcd35733de8447ba`

Initial management egress:

- TCP/443 to `0.0.0.0/0`
- TCP/53 to VPC CIDR
- UDP/53 to VPC CIDR
- No TCP/80 egress to the internal ALB security groups

ALB ingress:

- TCP/80 was already allowed on the ALB managed security group.
- No ALB security group changes were required.

## IaC change

Changed:

- `infra/modules/management-host/main.tf`
- `infra/modules/management-host/variables.tf`
- `infra/live/dev/management/terragrunt.hcl`

Added a scoped management security group egress rule:

- Protocol: TCP
- Port: `80`
- Source security group: `sg-0980b6aa338f1ef83`
- Destination security groups:
  - `sg-079c5a1e99c17270a`
  - `sg-0dcd35733de8447ba`

The rule does not allow HTTP egress to `0.0.0.0/0` and does not allow all traffic.

## Management plan and apply

Stack applied:

- `infra/live/dev/management`

Plan before apply:

- `2 to add`
- `0 to change`
- `0 to destroy`

Resources added:

- `aws_vpc_security_group_egress_rule.internal_alb_http["sg-079c5a1e99c17270a"]`
- `aws_vpc_security_group_egress_rule.internal_alb_http["sg-0dcd35733de8447ba"]`

No replacements were planned.

No EC2, IAM, endpoint, ALB security group, API Gateway, Lambda, or workload changes
were planned.

Apply result:

- `2 added`
- `0 changed`
- `0 destroyed`

Final plan:

- `No changes`

Evidence logs:

- `/tmp/cloud-native-platform-management-alb-smoke-path/management-init.log`
- `/tmp/cloud-native-platform-management-alb-smoke-path/management-plan.log`
- `/tmp/cloud-native-platform-management-alb-smoke-path/management-apply.log`
- `/tmp/cloud-native-platform-management-alb-smoke-path/management-final-plan.log`

## SG validation after apply

The management security group now has TCP/80 egress to the two ALB security groups.

Confirmed:

- No HTTP egress to `0.0.0.0/0`
- No all-traffic egress was added for the management host
- No ingress was added to the management host
- ALB security group ingress remained unchanged

## ALB and target health before smoke

ALB:

- Name: `cloud-native-platform-dev`
- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- Scheme: `internal`
- State: `active`
- Type: `application`
- VPC: `vpc-0fe33938202034387`

Target groups:

- `k8s-apps-authserv-58a4b90ba7`
- `k8s-apps-shipment-fb67f1d90f`
- `k8s-apps-tracking-9810f2e09e`

All target groups:

- Protocol: `HTTP`
- Target type: `ip`
- Health check path: `/health`
- Targets healthy before the change

## Broad ALB smoke results

Smoke source:

- Management EC2 `i-0417796819b2e0f46`

Base URL:

- `http://internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`

Results:

| Path | HTTP code | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/health` | `404` | `0` | ALB reachable; no root health route |
| `/auth` | `404` | `0` | ALB/backend reachable; route not defined |
| `/auth/swagger` | `301` | `0` | ALB/backend reachable; swagger redirect |
| `/auth/swagger/v1/swagger.json` | `200` | `12819` | Auth backend reachable |
| `/shipments` | `401` | `0` | Shipment backend reachable; auth required |
| `/shipments/swagger` | `301` | `0` | ALB/backend reachable; swagger redirect |
| `/shipments/swagger/v1/swagger.json` | `200` | `10618` | Shipment backend reachable |
| `/tracking` | `404` | `0` | Tracking backend reachable; route not defined |
| `/tracking/swagger` | `301` | `0` | ALB/backend reachable; swagger redirect |
| `/tracking/swagger/v1/swagger.json` | `200` | `6817` | Tracking backend reachable |
| `/auth/me` | `401` | `0` | Auth backend reachable; auth required |
| `/tracking/00000000-0000-0000-0000-000000000000` | `401` | `0` | Tracking backend reachable; auth required |

The previous `HTTP 000` timeout was resolved.

The broad smoke command attempted to sanitize and print a short response preview,
but the local `sed` expression failed. No response bodies are included in this
document.

## Focused smoke results

Focused smoke avoided response-body output and recorded only HTTP code and bytes.

| Path | HTTP code | Bytes | Interpretation |
| --- | ---: | ---: | --- |
| `/auth/swagger` | `301` | `0` | Swagger redirect |
| `/shipments/swagger` | `301` | `0` | Swagger redirect |
| `/tracking/swagger` | `301` | `0` | Swagger redirect |
| `/auth/swagger/v1/swagger.json` | `200` | `12819` | Auth OpenAPI reachable |
| `/shipments/swagger/v1/swagger.json` | `200` | `10618` | Shipment OpenAPI reachable |
| `/tracking/swagger/v1/swagger.json` | `200` | `6817` | Tracking OpenAPI reachable |

## Logs post-smoke

Logs were reviewed through SSM with redaction filters. No Kubernetes Secret values
were read or decoded.

Findings:

- No `AWSSDK.SecurityToken` error observed.
- No STS or IRSA credential errors observed.
- No SQS/EventBridge credential errors observed.
- No shipment SQS empty-poll `NullReferenceException` observed.
- `auth-service` still showed the historical PostgreSQL health check warning in
  the sampled log tail.
- Deployment readiness and ALB target health remained healthy.

## ALB and targets after smoke

ALB remained:

- `internal`
- `active`
- in VPC `vpc-0fe33938202034387`

All three app target groups remained healthy with two targets each.

## Runtime final state

`platform-services` remained deployed:

- Namespace: `apps`
- Revision: `3`
- Status: `deployed`

Deployments:

- `auth-service`: `2/2`
- `shipment-service`: `2/2`
- `tracking-service`: `2/2`

Pods remained Running/Ready.

Services and Ingress remained present.

`platform-runtime-secrets` remained present with eight expected keys. Only Secret
metadata and key names were validated.

## API Gateway and Lambda

Confirmed no matching project API Gateway or Lambda resources are applied.

`apigateway-integration` remains out of scope until internal readiness is accepted.

## Local validation

Executed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh || true`
- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace apps`
- `helm dependency build` for `cluster-addons`
- `helm lint` for `cluster-addons`
- `helm template` for `cluster-addons`

All required validations completed successfully.

## Secret review

No `.env`, `.pem`, `.key`, or `.tfvars` files were found.

Repository secret grep produced expected references to configuration keys and
code identifiers. No secret values were printed in the phase summary.

## Not performed

- No Terraform/Terragrunt apply outside `infra/live/dev/management`
- No `terragrunt run-all apply`
- No Helm install or upgrade
- No `kubectl apply`
- No `kubectl create`
- No `kubectl delete`
- No Kubernetes Secret modification
- No Kubernetes Secret value reads or decoding
- No Docker build
- No Docker push
- No API Gateway apply
- No Lambda apply
- No public EKS endpoint change

## Remaining blockers and observations

- Internal ALB smoke path from management EC2 now works.
- API Gateway/Lambda integration remains unapplied.
- `auth-service` PostgreSQL health check warning should be monitored or addressed
  before external integration is considered complete.

## Recommendation

Proceed to:

`Fase 2.12 — API Gateway readiness planning and internal route contract validation`

Recommended scope:

- Review internal ALB route contract and expected HTTP statuses.
- Confirm which routes API Gateway should expose.
- Keep API Gateway/Lambda unapplied until the route contract is explicit.
- Decide whether to fix or formally accept the auth/Postgres health warning before
  gateway integration.
