# Platform Services Install Evidence

Date: 2026-07-04
Branch: `feat/install-platform-services`
AWS account: `145023118802`
Region: `us-east-1`

## Summary

`platform-services` was installed through the private management path into the existing `apps` namespace.

Final release state:

- Release: `platform-services`
- Namespace: `apps`
- Status: `deployed`
- Revision: `1`
- Chart: `platform-services-0.1.0`

No API Gateway, Lambda, `apigateway-core`, or `apigateway-integration` was applied.

## Starting State

Validated before install:

- EKS cluster `logistics-platform-dev` reachable from the private management EC2.
- `cluster-addons` was already deployed in `kube-system`.
- Namespace `apps` existed.
- Secret `platform-runtime-secrets` existed with the expected 8 keys.
- No app pods, deployments, jobs, services, or ingress existed in `apps`.
- `platform-services` was not installed.

Management EC2:

- Instance ID: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- Public IP: none
- Instance profile: `cloud-native-platform-dev-management-profile`

Pre-install evidence:

- SSM command: `84732650-30c6-4dd3-bf75-36f6806a0c0d`

## Chart Render Validation

Local validation passed:

- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace apps --debug`

Render confirmed:

- Images render by immutable digest in account `145023118802`.
- Old account `795708473882` is absent.
- No empty image digest remains.
- Namespace is `apps`.
- Secret references point to `platform-runtime-secrets`.
- `shipment-service` and `tracking-service` render IRSA annotations.
- `auth-service` does not render IRSA.
- Ingress renders as internal ALB with target type `ip`.

Rendered resources include:

- ServiceAccounts
- ConfigMaps
- Services
- Deployments
- Migration hook Jobs
- PodDisruptionBudgets
- Ingress

## Artifacts

Artifacts were packaged locally and uploaded to S3.

Artifact bucket:

- `cloud-native-platform-145023118802-dev-us-east-1-artifacts`

Artifact prefix used by management EC2:

- `cluster-addons/dev/platform-services/20260704T151642Z-6f5a13c8023f/`

Files:

- `platform-services-0.1.0.tgz`
- `values.yaml`
- `rendered.yaml`

The prefix is under the already-authorized management artifact read path. No secrets were uploaded.

## Temporary Install Access

The management role was elevated through the `management` stack only.

Temporary Kubernetes access:

- Policy: `AmazonEKSEditPolicy`
- Scope: namespace `apps`
- Principal: `arn:aws:iam::145023118802:role/cloud-native-platform-dev-management-role`

This was chosen instead of cluster-admin because `platform-services` only needed namespaced resources and `apps` already existed.

Elevation plan:

- `1 to add`
- `0 to change`
- `1 to destroy`

The only planned action was replacement of `aws_eks_access_policy_association.view`:

- From `AmazonEKSAdminViewPolicy` cluster scope
- To `AmazonEKSEditPolicy` namespace scope for `apps`

Elevation apply:

- `1 added`
- `0 changed`
- `1 destroyed`

Final elevation plan:

- `No changes`

No EC2 replacement, SG change, endpoint change, NAT/RDS/EKS cluster change, API Gateway/Lambda change, or Secrets Manager/RDS permission was introduced.

Temporary access validation:

- SSM command: `bcde23de-80c3-4575-ac85-43c336a4fd3d`
- `kubectl auth can-i` returned `yes` for create operations needed by Helm in namespace `apps`.

## Helm Install

Install was executed from the private management EC2 through SSM.

SSM command:

- `1220345f-8233-48ce-9ebf-5f21f3ef2e7a`

Command summary:

```text
helm upgrade --install platform-services <platform-services-0.1.0.tgz> \
  -n apps \
  -f values.yaml \
  --wait \
  --timeout 15m \
  --atomic
```

Result:

- Status: `deployed`
- Revision: `1`
- Description: `Install complete`

Helm v4 emitted a warning that `--atomic` is deprecated in favor of `--rollback-on-failure`; the install still completed successfully.

## Migration Jobs

The chart renders migration Jobs as Helm hooks:

- Hook: `pre-install,pre-upgrade`
- Delete policy: `before-hook-creation,hook-succeeded`

Because the release installed successfully with `--wait`, the hook Jobs completed. They were not present after install because successful hook Jobs are deleted by the chart's hook delete policy.

Post-install `kubectl get jobs -n apps` returned no persisted jobs.

## Workload Status

Validation command:

- SSM command: `1168b774-28f1-4506-9c2a-e49933d5859a`

Deployments:

- `auth-service`: `2/2`
- `shipment-service`: `2/2`
- `tracking-service`: `2/2`

Pods:

- `auth-service`: 2 pods, `Running`, `1/1`, 0 restarts
- `shipment-service`: 2 pods, `Running`, `1/1`, 0 restarts
- `tracking-service`: 2 pods, `Running`, `1/1`, 0 restarts

Services:

- `auth-service`: `ClusterIP`, port `8080`
- `shipment-service`: `ClusterIP`, port `8080`
- `tracking-service`: `ClusterIP`, port `8080`

PodDisruptionBudgets:

- `auth-service-pdb`
- `shipment-service-pdb`
- `tracking-service-pdb`

## ServiceAccounts and IRSA

Rendered and live ServiceAccounts:

- `auth-service`
- `shipment-service`
- `tracking-service`

IRSA validation:

- `shipment-service` annotated with `arn:aws:iam::145023118802:role/cloud-native-platform-dev-shipment-service-irsa`
- `tracking-service` annotated with `arn:aws:iam::145023118802:role/cloud-native-platform-dev-tracking-service-irsa`
- `auth-service` intentionally has no IRSA annotation.

## Ingress and ALB

Ingress:

- Name: `platform-services`
- Namespace: `apps`
- Class: `alb`
- Address: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`

Important annotations:

- `alb.ingress.kubernetes.io/scheme: internal`
- `alb.ingress.kubernetes.io/target-type: ip`
- `alb.ingress.kubernetes.io/healthcheck-path: /health`
- `alb.ingress.kubernetes.io/load-balancer-name: cloud-native-platform-dev`

Ingress reconciliation:

- Event: `SuccessfullyReconciled`

ALB:

- Name: `cloud-native-platform-dev`
- DNS: `internal-cloud-native-platform-dev-381059970.us-east-1.elb.amazonaws.com`
- Scheme: `internal`
- Type: `application`
- State: `active`
- VPC: `vpc-0fe33938202034387`

Target groups:

- `k8s-apps-authserv-58a4b90ba7`
- `k8s-apps-shipment-fb67f1d90f`
- `k8s-apps-tracking-9810f2e09e`

Target group properties:

- Protocol: `HTTP`
- Target type: `ip`
- Health check path: `/health`
- VPC: `vpc-0fe33938202034387`

Target health:

- `auth-service`: 2 healthy targets
- `shipment-service`: 2 healthy targets
- `tracking-service`: 2 healthy targets

## Logs Reviewed

Sanitized logs were reviewed with defensive redaction. No secret values, passwords, JWTs, internal secrets, or connection strings were printed in the evidence.

Findings:

- `auth-service` started successfully, but one log tail showed a Postgres health check marked unhealthy during startup.
- `shipment-service` is running and passing readiness, but logs show repeated SQS polling failures caused by missing runtime assembly `AWSSDK.SecurityToken`.
- `tracking-service` started successfully with standard ASP.NET Core DataProtection warnings.

The shipment runtime error should be treated as the main follow-up blocker before public/API integration.

## Permission Reduction

After install and validation, the temporary namespace-scoped write access was removed through the `management` stack only.

Reduction plan:

- `1 to add`
- `0 to change`
- `1 to destroy`

The only planned action was replacement of `aws_eks_access_policy_association.view`:

- From `AmazonEKSEditPolicy` namespace scope for `apps`
- To `AmazonEKSAdminViewPolicy` cluster scope

Reduction apply:

- `1 added`
- `0 changed`
- `1 destroyed`

Final reduction plan:

- `No changes`

Final management role EKS access:

- `AmazonEKSAdminViewPolicy`
- Scope: cluster
- No `AmazonEKSEditPolicy`
- No `AmazonEKSClusterAdminPolicy`

Final management role IAM policies:

- Inline: `cloud-native-platform-dev-management-artifact-read-only`
- Inline: `cloud-native-platform-dev-management-eks-read-only`
- Attached: `AmazonSSMManagedInstanceCore`

Confirmed absent:

- `secretsmanager:GetSecretValue`
- `secretsmanager:DescribeSecret`
- `rds:DescribeDBInstances`
- `secretsmanager:*`
- `rds:*`

## Final Validation

Final validation command after permission reduction:

- SSM command: `9f79b5e8-b5ac-489a-9144-2883a05f26c8`

Final state:

- `cluster-addons`: deployed
- `platform-services`: deployed
- `apps`: exists
- `platform-runtime-secrets`: exists with the expected 8 keys
- Secret values were not printed or decoded
- Pods remain `Running` and `Ready`
- Deployments remain `2/2`
- Ingress remains present with internal ALB address

## API Gateway and Lambda

Confirmed unchanged:

- API Gateway query returned no matching APIs.
- Lambda query returned no matching functions.

No API Gateway, Lambda, `apigateway-core`, or `apigateway-integration` was applied.

## Evidence Files

Local evidence was written under:

- `/tmp/cloud-native-platform-platform-services-install/management-elevate-init.log`
- `/tmp/cloud-native-platform-platform-services-install/management-elevate-plan.log`
- `/tmp/cloud-native-platform-platform-services-install/management-elevate-apply.log`
- `/tmp/cloud-native-platform-platform-services-install/management-elevate-final-plan.log`
- `/tmp/cloud-native-platform-platform-services-install/management-reduce-plan.log`
- `/tmp/cloud-native-platform-platform-services-install/management-reduce-apply.log`
- `/tmp/cloud-native-platform-platform-services-install/management-reduce-final-plan.log`
- `/tmp/platform-services-dev-install-render.yaml`

## Blockers and Risks

Active runtime blocker:

- `shipment-service` logs show `AWSSDK.SecurityToken` missing at runtime, preventing AWS web identity credentials from loading for SQS polling.

Operational risks:

- Workloads and internal ALB are now live and can incur cost.
- ALB target health is green, but runtime behavior still needs service-level validation.
- API Gateway integration must wait until the shipment runtime error is fixed and internal ALB behavior is validated end-to-end.

## Recommendation

Proceed to a follow-up phase before API Gateway/Lambda:

`Fase 2.9 — Platform services runtime validation and shipment IRSA SDK fix`

Recommended scope:

- Fix the missing `AWSSDK.SecurityToken` dependency in `shipment-service`.
- Rebuild and push only the affected image if needed.
- Update dev values with the new immutable digest.
- Upgrade only `platform-services`.
- Validate shipment SQS polling and EventBridge publishing behavior.
- Re-check auth Postgres health.
- Validate internal ALB paths from the private network.
- Do not apply API Gateway/Lambda until runtime logs are clean.
