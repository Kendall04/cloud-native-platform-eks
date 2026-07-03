# notification-lambda

`notification-lambda` is the Phase A notification worker. It is triggered by the `notification-events-queue`, parses the shared domain-event envelope, and sends email through Amazon SES.

## Runtime

- AWS Lambda `nodejs22.x`
- AWS SDK for JavaScript v3

## Supported Events

- `TrackingStatusUpdated`

## Environment Variables

- `AWS_REGION`
- `SES_FROM_EMAIL`

Optional:

- `SES_TO_EMAIL`
  If omitted, the Lambda sends to `SES_FROM_EMAIL`. This is useful for early-phase operational notifications until customer recipient resolution is implemented.

## Local Notes

Install dependencies only if you want to run the handler locally with a vendored SDK:

```bash
cd lambdas/notification-lambda
npm install
```

The Terraform module packages the source directory directly. Lambda `nodejs22.x` provides AWS SDK v3 in the runtime, so the function can run without vendoring `node_modules`, but production pipelines may still choose to vendor dependencies for reproducibility.

## Deployment Notes

- The Terraform stack `infra/live/dev/notification-lambda` packages and deploys this function.
- SQS is connected through an event source mapping with partial batch failure reporting enabled.
- SES identities for the configured sender/recipient must be verified in the target AWS account.
