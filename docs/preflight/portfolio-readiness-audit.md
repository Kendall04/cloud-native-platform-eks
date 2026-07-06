# Portfolio Readiness Audit

Date: 2026-07-06
Branch: `docs/portfolio-readiness-audit`
AWS account: `145023118802`

## Executive Verdict

This project is ready to show as a portfolio-grade cloud-native platform demo.

Score: 8.5 / 10.

Best fit:

- Platform Engineer
- Cloud Engineer
- DevOps Engineer
- Infrastructure Engineer with application delivery ownership
- Backend engineer moving toward platform/cloud roles

Do not describe it as enterprise production-ready. The honest claim is stronger:
this is a production-like dev platform with real AWS infrastructure, private EKS
runtime, controlled API Gateway exposure, validated authentication behavior, and
documented tradeoffs.

## Validated Architecture

AWS:

- VPC with public/private subnet separation.
- Private EKS API endpoint.
- Managed EKS node groups.
- Private encrypted RDS PostgreSQL with deletion protection.
- ECR repositories with scan-on-push.
- SQS queues and EventBridge bus for service event flow.
- Lambda-based API Gateway request authorizer.
- Secrets Manager-backed authorizer secret references.
- API Gateway HTTP API public edge.
- VPC Link from API Gateway into the private VPC.
- Internal AWS Load Balancer Controller ALB.
- ALB frontend TCP/80 restricted to `10.0.0.0/16`.
- VPC Link security group egress scoped to ALB security groups.

Kubernetes:

- `cluster-addons` Helm release for AWS Load Balancer Controller and cluster-autoscaler.
- `platform-services` Helm release in namespace `apps`, revision `4`.
- `auth-service`, `shipment-service`, and `tracking-service` running `2/2`.
- Internal ALB Ingress managed by AWS Load Balancer Controller.
- Per-service ServiceAccounts, including IRSA-backed service accounts for AWS-integrated workloads.
- Digest-pinned service images.
- Readiness, liveness, startup probes, resource requests/limits, PDBs, and migration job scaffolding.
- Runtime secret values are referenced from Kubernetes Secrets, not stored in Git.

CI/CD:

- PR validation workflow with title/commit policy.
- .NET restore/test matrix for the three services.
- Helm lint/template validation.
- Release metadata validation.
- OIDC-capable image build and deploy workflows gated by repo variables/secrets.
- Image build workflow is gated and not always-on.

## Evidence Chain

Integrated evidence by PR:

- PR #30: API Gateway core and VPC Link readiness.
- PR #32: ALB inbound CIDR hardening.
- PR #33: API Gateway integration apply.
- PR #34: Authenticated API Gateway smoke.
- PR #35: API Gateway operational handoff and runbook.

Read-only validation in this audit:

- EKS cluster `logistics-platform-dev` is `ACTIVE`, Kubernetes `1.35`, private endpoint enabled and public endpoint disabled.
- `cluster-addons` and `platform-services` are deployed.
- `platform-services` revision `4`.
- All app deployments are `2/2`.
- API Gateway `diluedb2k7` exists with expected routes.
- VPC Link `c2au37` is `AVAILABLE`.
- ALB `cloud-native-platform-dev` is internal and active.
- Target groups have two healthy targets per service.
- `apigateway-core`, `apigateway-integration`, and `api-gateway-authorizer` all plan `No changes`.

## Strong Selling Points

- Real private EKS runtime behind an internal ALB.
- Public API Gateway edge using VPC Link, not direct public service exposure.
- Lambda request authorizer wired to protected API routes.
- Authorizer secret references are sourced through Secrets Manager, with secret values excluded from Terraform state and Git.
- Clear route exposure contract: public auth routes, protected service routes, excluded internal/admin/swagger/health routes.
- Authenticated smoke validated through API Gateway into EKS services.
- No-token protected routes fail closed.
- ALB ingress reduced from broad public CIDR to VPC CIDR for dev.
- Modular Terraform/Terragrunt architecture with evidence-driven apply phases.
- Helm runtime with digest-pinned images and health probes.
- CI checks and operational runbooks make the project understandable and repeatable.

## Honest Limitations And Tradeoffs

- This is a dev environment, not a production environment.
- No custom domain.
- No WAF or API rate limiting.
- ALB frontend ingress is hardened to VPC CIDR, not strict SG-to-SG source.
- No production promotion has been performed.
- No full DR, SLO, error budget, or observability suite is present.
- Disposable authenticated smoke user cleanup depends on available safe application endpoints.
- Some application code has compiler warnings that should be handled before claiming production maturity.
- The Terraform S3 backend emits a deprecation warning for `dynamodb_table`; this is not a runtime issue but should be modernized.
- Prod Helm values are static placeholders and should not be represented as a deployed prod environment.

## Recommended Remaining Improvements

Low-risk final polish:

1. Keep the README wording focused on production-like dev and portfolio-grade validation.
2. Add or link a simple architecture diagram.
3. Add a short "How to demo this" section after this audit lands.
4. Consider a repo tag after the portfolio docs merge.
5. Triage compiler warnings in the three services.

Medium-value improvements:

1. Add a redacted synthetic smoke workflow for API Gateway status-code checks.
2. Add SG-to-SG frontend hardening for the ALB path if a clean LBC-safe design is chosen.
3. Add WAF/rate limiting/custom domain as a later edge-hardening phase.
4. Add metrics/logging dashboards or a lightweight observability runbook.
5. Add NetworkPolicies if a CNI/network-policy design is introduced.

Not worth doing now:

1. Rebuilding the platform around GitOps just for the claim.
2. Promoting to production without a real prod plan.
3. Adding broad feature work before polishing the portfolio story.
4. Manual security group edits that would drift from AWS Load Balancer Controller.

## Portfolio And README Narrative

Suggested short paragraph:

Built a production-like AWS EKS dev platform with Terraform/Terragrunt, Helm,
private services behind an internal ALB, and a public API Gateway HTTP API
integrated through VPC Link. The platform includes a Lambda request authorizer,
controlled route exposure, digest-pinned deployments, CI validation, operational
runbooks, and documented tradeoffs validated through authenticated API smoke
tests.

## CV Bullets

- Built a production-like AWS EKS platform using Terraform, Terragrunt, Helm, and GitHub Actions, with private workloads exposed through API Gateway and VPC Link.
- Implemented a Lambda request authorizer backed by Secrets Manager references and validated protected-route fail-closed behavior through API Gateway.
- Deployed three .NET 8 microservices to EKS with digest-pinned images, health probes, resource controls, internal ALB routing, and IRSA for AWS-integrated services.
- Modeled AWS infrastructure including VPC, private EKS endpoint, RDS PostgreSQL, ECR, SQS, EventBridge, Lambda, API Gateway, VPC Link, and security groups.
- Hardened the dev ingress posture by restricting the internal ALB frontend to the VPC CIDR and scoping VPC Link egress to ALB security groups.
- Created operational runbooks and evidence-driven rollout documentation covering plans, applies, smoke tests, rollback notes, and known limitations.

## LinkedIn And GitHub Project Description

One-liner:

Production-like AWS EKS platform demo with API Gateway private integration, Lambda authorization, Helm deployments, and documented operational evidence.

Two-sentence description:

This project demonstrates a cloud-native logistics platform running private .NET
microservices on EKS, exposed through API Gateway HTTP API, Lambda request
authorizer, VPC Link, and an internal ALB. It is portfolio-grade rather than
enterprise production-ready: the repo includes IaC, Helm, CI checks, authenticated
smoke evidence, runbooks, and documented tradeoffs.

Technical tags:

- AWS
- EKS
- Terraform
- Terragrunt
- Kubernetes
- Helm
- API Gateway
- VPC Link
- Lambda authorizer
- RDS PostgreSQL
- SQS
- EventBridge
- GitHub Actions
- .NET 8

## Interview Explanation

60-second explanation:

I built a production-like dev platform for a logistics application on AWS. The
runtime is private on EKS, with services behind an internal ALB, and the public
edge is API Gateway HTTP API using a Lambda request authorizer and VPC Link. I
validated public, protected, excluded, and authenticated paths, kept secrets out
of Git and Terraform state, and documented the exact rollout evidence and
tradeoffs.

2-minute deep dive:

The platform starts with Terraform and Terragrunt modules for VPC, EKS, RDS,
ECR, SQS, EventBridge, IAM, management access, Lambda, and API Gateway. The app
layer is three .NET services deployed with a Helm chart using digest-pinned
images, service accounts, probes, resource settings, PDBs, and an internal ALB
Ingress. API Gateway is split into core and integration stacks: core creates the
HTTP API, stage, authorizer wiring, VPC Link, and VPC Link security group; the
integration stack adds only the route and HTTP proxy integrations to the ALB
listener. I validated that no-token protected routes fail closed, excluded routes
are not exposed, and a disposable authenticated user can reach the expected
protected paths without printing tokens or response bodies.

What tradeoffs did you make?

- I treated this as a production-like dev platform, not a production system.
- I hardened the ALB frontend to VPC CIDR for dev but left SG-to-SG frontend hardening as a future improvement.
- I kept API Gateway integration and authorizer applies phased to reduce blast radius.
- I prioritized evidence, runbooks, and reversibility over rushing all hardening into one phase.

What would you improve next?

- Add a diagram and final release tag.
- Triage compiler warnings.
- Add synthetic status-code smoke checks with redacted output.
- Add WAF, custom domain, and rate limits if the edge becomes more production-like.
- Evaluate a clean SG-to-SG ALB frontend strategy that does not fight AWS Load Balancer Controller.

How did you validate it?

- PR checks for .NET tests and Helm rendering.
- Terragrunt plans showing `No changes` after applies.
- Read-only AWS state validation.
- ALB and API Gateway smoke tests using status codes and byte counts only.
- Authenticated smoke through API Gateway with no token or body printing.
- Target group health and runtime checks through the management EC2 path.

## Claim Safety Table

| Claim | Safe? | Evidence | Suggested wording |
| --- | --- | --- | --- |
| Built an AWS EKS platform | Yes | EKS runtime active, Helm releases deployed | Built a production-like AWS EKS dev platform |
| Production-ready enterprise platform | No | Dev-only tradeoffs remain | Production-like dev platform with documented tradeoffs |
| Private services exposed through API Gateway | Yes | API Gateway, VPC Link, internal ALB validation | Exposed private EKS services through API Gateway and VPC Link |
| Fully locked-down network | Soften | ALB frontend uses VPC CIDR, not SG-to-SG | Hardened dev ingress posture with VPC CIDR and scoped VPC Link egress |
| Secrets are safely handled | Soften | No values in Git/docs; runtime still uses Kubernetes Secret | Kept secret values out of Git/docs and used Secrets Manager refs for the authorizer |
| End-to-end auth validated | Yes for dev smoke | Register/login/refresh/protected smoke evidence | Validated authenticated API Gateway smoke in dev |
| Full production observability | No | No full dashboards/SLOs documented | Operational checks and runbooks documented |
| GitOps platform | No | Workflows exist but no GitOps controller | CI/CD and manual gated deploy workflows are documented |

## Final Recommendation

Merge this audit and demo narrative to `develop` after review.

Promote to `main` after a short final docs PR/review, not before. The project is
ready for portfolio use now, but a final `main` promotion should include a quick
README pass, a clean status check, and a tag or release note.

Recommended next phase:

`Fase 3.1 - Final documentation PR integration and main promotion readiness`

Scope:

- Review and integrate this audit.
- Confirm README links and claim language.
- Confirm no secret/token/body content.
- Run final CI checks.
- Decide whether to squash merge to `develop` and then promote to `main`.
