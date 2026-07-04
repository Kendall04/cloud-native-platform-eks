# Cluster Addons Install Attempt

Date: 2026-07-04T00:31:45Z

Branch: `feat/install-cluster-addons`

AWS account: `145023118802`

Region: `us-east-1`

## Goal

Install the `cluster-addons` Helm release through the private management EC2 path.

Expected addons:

- AWS Load Balancer Controller.
- Cluster Autoscaler.

No application workloads, API Gateway, Lambda, or API Gateway integration were in scope.

## Pre-Install Cluster State

EKS cluster:

- Name: `logistics-platform-dev`
- Status: `ACTIVE`
- Kubernetes: `1.35`
- Endpoint private access: enabled
- Endpoint public access: disabled
- VPC: `vpc-0fe33938202034387`
- OIDC issuer: `https://oidc.eks.us-east-1.amazonaws.com/id/C45613EAD1A4FB94AC9D9AA7D391C4FC`

Management EC2 before tooling update:

- Instance ID: `i-01b3101eaca85d043`
- `kubectl`: working
- Helm: not installed

Pre-install `kubectl` validation from the management EC2 succeeded:

- 3 nodes `Ready`
- Existing pods limited to managed `kube-system` components
- Existing deployments: `coredns`, `ebs-csi-controller`

## Management Tooling Changes

The management host module was updated to install Helm via user data.

Files changed:

- `infra/modules/management-host/user_data.sh.tftpl`
- `infra/modules/management-host/variables.tf`
- `infra/modules/management-host/main.tf`
- `infra/live/dev/management/terragrunt.hcl`

Added:

- `helm_version`, set to `v4.2.2`
- Helm binary installation from `https://get.helm.sh`
- Optional S3 artifact read policy
- Live stack wiring to the dev S3 artifacts bucket

S3 read-only scope for management role:

- Bucket: `cloud-native-platform-145023118802-dev-us-east-1-artifacts`
- Prefix: `cluster-addons/dev/*`
- Actions:
  - `s3:ListBucket` restricted to `cluster-addons/dev`
  - `s3:GetObject` restricted to `cluster-addons/dev/*`

## Management Apply Result

Only the management stack was applied.

Plan:

- `2 to add, 0 to change, 1 to destroy`

Expected changes:

- Create management role S3 artifact read-only inline policy.
- Replace the management EC2 instance because user data changed to install Helm.

Apply:

- Succeeded.
- New management instance: `i-0417796819b2e0f46`
- Private IP: `10.0.134.40`
- Public IP: none
- SSM status: `Online`

Post-apply tooling validation from management EC2:

- AWS CLI: available
- `kubectl v1.35.0`: available
- `helm v4.2.2`: available
- `kubectl` can read the private EKS cluster

Final management plan:

- `No changes`

## Artifact Strategy

Artifacts were generated locally and uploaded to the existing app artifacts bucket.

Bucket:

- `cloud-native-platform-145023118802-dev-us-east-1-artifacts`

Prefix:

- `cluster-addons/dev/20260704T002833Z-e67eaab/`

Artifacts:

- `cluster-addons-0.1.0.tgz`
- `values.yaml`
- `rendered.yaml`

Generated values include:

- Cluster name: `logistics-platform-dev`
- Region: `us-east-1`
- VPC ID: `vpc-0fe33938202034387`
- AWS Load Balancer Controller IRSA role ARN:
  - `arn:aws:iam::145023118802:role/logistics-platform-dev-aws-load-balancer-controller-role`
- Cluster Autoscaler IRSA role ARN:
  - `arn:aws:iam::145023118802:role/logistics-platform-dev-cluster-autoscaler-role`

No placeholders or secrets were present in generated values or rendered manifests.

## Helm Install Attempt

Command path:

```bash
aws s3 cp s3://cloud-native-platform-145023118802-dev-us-east-1-artifacts/cluster-addons/dev/20260704T002833Z-e67eaab/cluster-addons-0.1.0.tgz /tmp/cluster-addons-install/cluster-addons.tgz
aws s3 cp s3://cloud-native-platform-145023118802-dev-us-east-1-artifacts/cluster-addons/dev/20260704T002833Z-e67eaab/values.yaml /tmp/cluster-addons-install/values.yaml
aws eks update-kubeconfig --region us-east-1 --name logistics-platform-dev --kubeconfig /root/.kube/config
KUBECONFIG=/root/.kube/config helm upgrade --install cluster-addons /tmp/cluster-addons-install/cluster-addons.tgz --namespace kube-system -f /tmp/cluster-addons-install/values.yaml --wait --timeout 10m
```

Result:

- Artifact downloads from S3 succeeded.
- Kubeconfig update succeeded.
- Helm attempted to install release `cluster-addons`.
- Install failed before release creation.

Failure:

The management role can authenticate to the cluster and perform read-only validation, but it cannot create or patch CRDs.

The AWS Load Balancer Controller chart includes CRDs:

- `ingressclassparams.elbv2.k8s.aws`
- `targetgroupbindings.elbv2.k8s.aws`

The Kubernetes API rejected CRD patch/create operations for:

- API group: `apiextensions.k8s.io`
- Resource: `customresourcedefinitions`
- Scope: cluster

This is expected with the current `AmazonEKSAdminViewPolicy` access model, which is privileged read access but not write/admin access.

## Post-Failure State

Read-only checks after the failed install confirmed:

- No `cluster-addons` Helm release exists.
- No AWS Load Balancer Controller deployment exists.
- No Cluster Autoscaler deployment exists.
- No `elbv2` CRDs were found.
- Existing pods remain limited to managed `kube-system` components.
- No application workloads were deployed.

API Gateway/Lambda check:

- No matching API Gateway v2 APIs found.
- No matching Lambda functions found.

Ingress/workload check:

- No ingress resources found.
- Services remain limited to Kubernetes and managed system services.

## Controller Validation

AWS Load Balancer Controller:

- Not installed.
- Deployment not present.
- Pods not present.
- ServiceAccount not created by Helm.
- Logs unavailable.

Cluster Autoscaler:

- Not installed.
- Deployment not present.
- Pods not present.
- ServiceAccount not created by Helm.
- Logs unavailable.

## Blocker

The private management role has enough access for read-only cluster validation but not enough Kubernetes authorization for Helm installation of cluster-scoped resources and CRDs.

Blocked operation:

- `helm upgrade --install cluster-addons`

Required decision before retry:

- Define a controlled write/admin path for cluster addon installation.

Potential options:

1. Temporarily associate a stronger EKS access policy, such as `AmazonEKSClusterAdminPolicy`, to the management role for the install phase, then reduce it after validation.
2. Create a dedicated cluster-addons installer role with explicit lifecycle and audit documentation.
3. Split CRD installation into a separately approved admin phase, then install controllers with narrower permissions.

Option 1 is simplest for a portfolio lab, but it should be time-boxed and documented because it grants cluster admin permissions.

## Cost And Risk

Additional active cost from this phase:

- Management EC2 remains `t3.nano`.
- No new workloads or load balancers were created.
- No ALB/NLB was created.

Existing active costs remain:

- EKS control plane.
- EKS node groups.
- NAT EC2/public IPv4.
- RDS `db.t4g.micro`.
- EBS volumes.
- S3/ECR/SQS/EventBridge usage.

Risk:

- The chart is ready, but installation requires cluster-scoped write permissions.
- Granting cluster admin should be treated as an explicit controlled operation.

## Logs

Local evidence:

- `/tmp/cloud-native-platform-cluster-addons-install/management-init.log`
- `/tmp/cloud-native-platform-cluster-addons-install/management-plan.log`
- `/tmp/cloud-native-platform-cluster-addons-install/management-apply.log`
- `/tmp/cloud-native-platform-cluster-addons-install/management-final-plan.log`
- `/tmp/cloud-native-platform-cluster-addons-install/tooling-result.json`
- `/tmp/cloud-native-platform-cluster-addons-install/generated-values.yaml`
- `/tmp/cloud-native-platform-cluster-addons-install/rendered.yaml`
- `/tmp/cloud-native-platform-cluster-addons-install/helm-install-result.json`
- `/tmp/cloud-native-platform-cluster-addons-install/post-failed-install-check-result.json`
- `/tmp/cloud-native-platform-cluster-addons-install/no-app-workloads-result.json`

## Non-Actions

This phase did not execute:

- `kubectl apply`
- `kubectl delete`
- `terragrunt run-all apply`
- API Gateway apply
- Lambda apply
- `apigateway-core` apply
- `apigateway-integration` apply
- Platform services install
- Application workload deployment
- Endpoint public access changes
- NAT bastion conversion
- Git push

The only Terraform/Terragrunt apply was for the `management` stack.

## Recommendation

Do not proceed to application workloads yet.

Recommended next phase:

`Fase 2.1b - Controlled cluster-admin path for addons install`

Scope:

- Decide and implement the minimum acceptable temporary cluster write/admin path.
- Prefer a time-boxed installer access model.
- Re-run `helm upgrade --install cluster-addons`.
- Validate AWS Load Balancer Controller and Cluster Autoscaler.
- Remove or reduce elevated installer access if the chosen model is temporary.
