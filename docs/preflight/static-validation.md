# Static Preflight Validation

This document captures the local-only checks for Phase 0.5. It does not prove
that AWS infrastructure exists, that an EKS cluster is reachable, or that any
runtime traffic path works.

## What This Phase Validates

- Terraform files are formatted.
- Terragrunt files are formatted.
- The `platform-services` Helm chart lints and renders with the dev overlay.
- The `platform-services` prod overlay renders as a static placeholder.
- The `cluster-addons` Helm chart can build dependencies and render in a
  temporary directory.
- The API Gateway JWT authorizer Terragrunt stack fails fast when required
  secret environment variables are missing.

## Commands

```bash
terraform fmt -check -recursive infra
terragrunt hcl format --check infra/live

helm lint ./k8s/charts/platform-services \
  -f ./k8s/environments/dev/platform-services.values.yaml

helm template platform-services \
  ./k8s/charts/platform-services \
  -f ./k8s/environments/dev/platform-services.values.yaml \
  >/tmp/platform-services-dev.yaml

helm template platform-services \
  ./k8s/charts/platform-services \
  -f ./k8s/environments/prod/platform-services.values.yaml \
  >/tmp/platform-services-prod.yaml

tmp="$(mktemp -d)"
cp -R k8s/charts/cluster-addons/. "$tmp/"
helm dependency build "$tmp"
helm template cluster-addons "$tmp" >/tmp/cluster-addons.yaml
```

## Image Digests

The dev and prod overlays intentionally still contain `digest: ""`.

This is expected until Phase 3, where CI builds real ECR images and records
their immutable digests. Manual GitHub deployments must continue to reject empty
digests before deploying.

## Prod Overlay

The prod overlay is not a real production environment yet. It exists so the
chart can be statically rendered and reviewed.

Current prod values include placeholder image tags and placeholder infrastructure
values such as IRSA role ARNs and queue URLs. These values must be replaced by
real release metadata, image digests, and infrastructure outputs before any prod
deployment attempt.

## Cluster Addons

`cluster-addons` depends on upstream charts:

- `aws-load-balancer-controller`
- `cluster-autoscaler`

Do not vendor chart packages into the repository for this phase. Use
`helm dependency build` in a temporary directory for local validation.

The chart default values contain static preflight placeholders for required
subchart fields such as `clusterName`, `region`, and `vpcId`. The real deploy
script overrides these values from Terragrunt outputs before installing anything
in a cluster.

## Required Runtime Follow-Up

Phase 1 and later must still validate:

- AWS credentials and account identity.
- Terragrunt plans against the target AWS account.
- EKS private endpoint access and kubeconfig.
- Runtime secrets in the `apps` namespace.
- ECR image digests from real builds.
- Helm install/upgrade behavior against a live cluster.
- API Gateway to VPC Link to internal ALB to Service connectivity.
- IRSA behavior from running pods.
- SQS/EventBridge end-to-end event flow.
