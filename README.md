# cloud-native-platform-eks
Production-oriented cloud-native platform on AWS EKS with Terraform, Kubernetes, Helm, and CI/CD.

## API Gateway Private Integration

This repository now includes a portfolio-grade, production-like dev API Gateway
integration path:

- HTTP API public edge with a Lambda request authorizer.
- VPC Link into an internal AWS Load Balancer Controller ALB.
- Private EKS services behind controlled API Gateway routes.
- Public auth routes for register, login, and refresh.
- Protected auth, shipment, and tracking routes that fail closed without a valid
  token.
- Internal, admin, swagger, and root health routes intentionally excluded from
  API Gateway exposure.

The dev endpoint is:

```text
https://diluedb2k7.execute-api.us-east-1.amazonaws.com
```

Operational notes and safe smoke procedures are documented in
[`docs/operations/api-gateway-runbook.md`](docs/operations/api-gateway-runbook.md).
The current dev posture is validated and documented, with known tradeoffs such
as VPC CIDR ALB ingress hardening instead of SG-to-SG frontend least privilege.
