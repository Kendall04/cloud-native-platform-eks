# Demo Narrative

## Project Summary

This repository is a portfolio-grade, production-like AWS EKS platform demo for a
logistics application. It combines Terraform/Terragrunt infrastructure, Helm
runtime delivery, .NET microservices, API Gateway private integration, a Lambda
request authorizer, and documented operational evidence.

It should be described as a serious platform engineering lab with real AWS
validation and documented tradeoffs, not as an enterprise production-ready
platform.

## Architecture Summary

Traffic enters through API Gateway HTTP API. Public auth routes are forwarded
through VPC Link to an internal ALB, while protected routes require the Lambda
request authorizer. The ALB routes to private EKS services running in the `apps`
namespace.

Core components:

- API Gateway HTTP API public edge.
- Lambda request authorizer.
- VPC Link into private subnets.
- Internal AWS Load Balancer Controller ALB.
- EKS-hosted .NET 8 services: auth, shipment, and tracking.
- Private encrypted RDS PostgreSQL.
- SQS and EventBridge for event flow.
- ECR repositories with scan-on-push.
- Helm releases for platform services and cluster addons.

## Validation Summary

Validated state:

- EKS cluster active with private endpoint.
- `platform-services` revision `4`.
- All app deployments running `2/2`.
- VPC Link `AVAILABLE`.
- ALB internal and active.
- Target groups have two healthy targets per service.
- API Gateway routes match the intended contract.
- Protected no-token routes return `401`.
- Excluded swagger, admin, internal, and root health routes are not exposed.
- Authenticated smoke was validated through API Gateway without printing tokens or response bodies.
- API Gateway core, integration, and authorizer plans return `No changes`.

## Tradeoffs

- This is a dev platform with production-like patterns, not a production system.
- ALB frontend ingress is restricted to VPC CIDR; SG-to-SG frontend hardening is a future improvement.
- No custom domain, WAF, API rate limiting, or production promotion yet.
- No full observability/SLO/DR suite is claimed.
- Authenticated smoke used a disposable dev/test user; cleanup depends on safe app endpoints.

## Interview-Ready Explanation

Short version:

I built a production-like AWS EKS platform where private .NET services are
exposed through API Gateway using VPC Link and a Lambda request authorizer. The
project includes modular Terraform/Terragrunt, Helm deployments, private runtime
networking, route exposure control, authenticated smoke validation, CI checks,
and operational runbooks.

Deep dive:

The infrastructure is split into composable Terragrunt stacks for VPC, EKS, RDS,
ECR, SQS, EventBridge, IAM, Lambda, and API Gateway. The runtime uses Helm to
deploy three services to EKS behind an internal ALB. API Gateway is split into
core and integration layers so the HTTP API, VPC Link, authorizer, and routes can
be planned and validated independently. I validated the system with read-only AWS
checks, Terragrunt final plans, ALB/API Gateway smoke tests, and an authenticated
happy path through the public API edge.

## CV Bullets

- Built a production-like AWS EKS platform using Terraform, Terragrunt, Helm, and GitHub Actions for a multi-service logistics application.
- Exposed private EKS services through API Gateway HTTP API, Lambda request authorizer, VPC Link, and an internal ALB.
- Validated public, protected, excluded, and authenticated API Gateway routes with status-code-only smoke tests and no secret/token output.
- Deployed .NET 8 microservices with digest-pinned images, Kubernetes probes, resource controls, PDBs, and IRSA-backed service accounts.
- Modeled AWS infrastructure including private EKS, encrypted RDS PostgreSQL, ECR, SQS, EventBridge, Lambda, API Gateway, and security groups.
- Created operational runbooks and evidence documentation covering rollout phases, validation, rollback notes, and known limitations.

## LinkedIn Headline

Built a production-like AWS EKS platform demo with API Gateway private
integration, Lambda authorization, Helm delivery, and validated authenticated
smoke tests.

## GitHub Project Description

Cloud-native logistics platform on AWS EKS using Terraform/Terragrunt, Helm,
.NET 8 microservices, API Gateway HTTP API, Lambda request authorizer, VPC Link,
internal ALB routing, and documented operational validation.

## Technical Tags

- AWS
- EKS
- Terraform
- Terragrunt
- Kubernetes
- Helm
- API Gateway
- VPC Link
- Lambda
- RDS PostgreSQL
- SQS
- EventBridge
- ECR
- GitHub Actions
- .NET 8

## Demo Flow

1. Start with the README architecture summary.
2. Show the API Gateway runbook.
3. Explain the route contract: public, protected, and excluded.
4. Show evidence docs for core, integration, authenticated smoke, and handoff.
5. Mention final `No changes` plans.
6. Explain tradeoffs plainly.

## Safe Claims

Use:

- production-like dev platform
- portfolio-grade platform engineering demo
- validated AWS/EKS platform
- private EKS services behind API Gateway and VPC Link
- documented operational tradeoffs

Avoid:

- enterprise production-ready
- fully locked down
- complete zero-trust platform
- full GitOps platform
- production SLO/DR/observability platform
