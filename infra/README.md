# Infrastructure Repository

This repository uses reusable Terraform modules under `infra/modules` and Terragrunt stacks under `infra/live/dev`.

Backend state is centralized in S3 with DynamoDB locking. Bucket and table names are derived from the environment settings in `infra/live/dev/env.hcl`.

## Layout

```text
infra/
  modules/
  live/
    root.hcl
    dev/
      env.hcl
      <module>/terragrunt.hcl
```

Lambda source code lives under:

- `lambdas/notification-lambda`
- `lambdas/api-gateway-jwt-authorizer`

## Usage

For the initial infrastructure bootstrap, use the repository script:

```bash
./scripts/deploy.sh
```

That script applies the base AWS and EKS stacks, including `apigateway-core`, and intentionally excludes `apigateway-integration`.
The EKS stack now creates the IAM/OIDC prerequisites for cluster addons but does not install Helm charts inside the cluster.

If you want to work stack by stack, run Terragrunt from `infra/live/dev`:

```bash
terragrunt run-all init
terragrunt run-all plan
terragrunt run-all apply
```

The `dev` environment now includes:

- `eventbridge` for the shared custom bus and routing rules
- `sqs` for consumer queues and DLQs
- `iam` for IRSA roles
- `notification-lambda` for the SES-backed notification worker
- `api-gateway-authorizer` for upstream JWT validation
- `apigateway-core` for the HTTP API, VPC Link, authorizer, and stage
- `apigateway-integration` for the ALB lookup, API integrations, and routes

For the Phase A end-to-end event flow, see `infra/docs/phase-a-event-flow.md`.
For the Phase B EKS runtime packaging and ingress/IRSA model, see `infra/docs/phase-b-eks-runtime.md`.
For the hardening pass that closes the API edge, migrations, and database/runtime consistency gaps, see `infra/docs/hardening-phase.md`.
After the base Terragrunt bootstrap, install the cluster-wide addons with `./scripts/deploy-cluster-addons.sh` before deploying `k8s/charts/platform-services`.
Use `./scripts/deploy-platform-services.sh` with the environment overlays under `k8s/environments/` for application deployments.
Apply `apigateway-integration` only after the Helm ingress has created the internal ALB.

## EKS Access

To configure `kubectl` for the private EKS cluster, see `infra/docs/eks-kubectl-access.md`.
