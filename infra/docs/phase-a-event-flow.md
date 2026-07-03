# Phase A Event Flow

Phase A closes the main business flow of the logistics platform:

1. `auth-service` issues JWTs.
2. `shipment-service` creates shipments and owns current shipment status.
3. `tracking-service` stores immutable tracking history.
4. `tracking-service` publishes `TrackingStatusUpdated` to the custom EventBridge bus.
5. EventBridge fans out the event to consumer-specific SQS queues.
6. `shipment-service` consumes from `shipment-events-queue` and updates current shipment status asynchronously.
7. `notification-lambda` consumes from `notification-events-queue` and sends an email through SES.
8. `analytics-events-queue` is provisioned and wired for a future analytics worker.

## Published Events

### TrackingStatusUpdated

Published by:

- `tracking-service`

Envelope:

```json
{
  "eventId": "uuid",
  "eventType": "TrackingStatusUpdated",
  "eventVersion": "1.0",
  "source": "tracking-service",
  "timestamp": "2026-03-12T12:00:00Z",
  "data": {
    "shipmentId": "uuid",
    "trackingEventId": "uuid",
    "status": "IN_TRANSIT",
    "location": "Los Angeles Hub",
    "eventOccurredAt": "2026-03-12T12:00:00Z"
  }
}
```

## Queues

### `cloud-native-platform-dev-shipment-events-queue`

Consumes:

- `shipment-service`

Purpose:

- applies asynchronous shipment status updates from tracking events
- enforces idempotency using `ProcessedEvents`
- rejects invalid or out-of-order state changes safely

DLQ:

- `cloud-native-platform-dev-shipment-events-dlq`

### `cloud-native-platform-dev-notification-events-queue`

Consumes:

- `notification-lambda`

Purpose:

- sends SES-backed email notifications for supported domain events

DLQ:

- `cloud-native-platform-dev-notification-events-dlq`

### `cloud-native-platform-dev-analytics-events-queue`

Consumes:

- no worker in Phase A

Purpose:

- reserved fan-out target for the future analytics worker

DLQ:

- `cloud-native-platform-dev-analytics-events-dlq`

## Consumers

### shipment-service

Source queue:

- `shipment-events-queue`

Processing behavior:

- deserializes `TrackingStatusUpdated`
- starts a DB transaction
- checks `ProcessedEvents`
- skips duplicates
- ignores missing shipments safely
- ignores out-of-order events
- rejects invalid backward transitions
- commits DB changes before deleting the SQS message

### notification-lambda

Source queue:

- `notification-events-queue`

Processing behavior:

- consumes SQS batches
- parses the shared envelope
- supports `TrackingStatusUpdated`
- sends SES email with minimal templating
- returns partial batch failures for retry/DLQ handling

## IAM

### shipment-service

IRSA permissions:

- `sqs:ReceiveMessage`
- `sqs:DeleteMessage`
- `sqs:GetQueueAttributes`
- `sqs:ChangeMessageVisibility`
- `events:PutEvents`

### tracking-service

IRSA permissions:

- `events:PutEvents`

### notification-lambda

Lambda role permissions:

- CloudWatch Logs write access
- SQS poll/delete via Lambda event source mapping policy
- SES email send permissions

## Not In Phase A Yet

- analytics worker implementation
- full API Gateway to EKS route/integration wiring
- Kubernetes manifests / rollout strategy
- customer-specific recipient resolution beyond Lambda environment configuration
