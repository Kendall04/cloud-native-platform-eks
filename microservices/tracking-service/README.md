# tracking-service

`tracking-service` owns the tracking timeline/history for the logistics platform. It stores immutable tracking events, exposes shipment timelines, validates shipment visibility through `shipment-service`, and publishes `TrackingStatusUpdated` events to EventBridge after persistence.

## Stack

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- AWS SDK for .NET
- Amazon EventBridge
- Docker

## Solution Layout

```text
tracking-service/
  src/
    TrackingService.Api/
    TrackingService.Application/
    TrackingService.Domain/
    TrackingService.Infrastructure/
  tests/
    TrackingService.Tests/
```

## Configuration

Set the following environment variables before running the service:

```bash
export ConnectionStrings__Postgres="Host=rds-endpoint;Database=platform;Username=platform_admin;Password=securepassword"
export Database__Schema="tracking"
export AWS__Region="us-east-1"
export AWS__EventBusName="cloud-native-platform-dev-bus"
export ShipmentService__BaseUrl="http://shipment-service.apps.svc.cluster.local:8080"
export PlatformAuth__TrustedProxySecret="<32-byte-or-longer-shared-secret>"
export PlatformAuth__InternalServiceSecret="<32-byte-or-longer-internal-secret>"
export ASPNETCORE_ENVIRONMENT="Development"
```

Optional:

```bash
export ASPNETCORE_URLS="http://+:8080"
export PORT="8080"
```

## Local Run

```bash
cd microservices/tracking-service
dotnet restore TrackingService.sln
dotnet build TrackingService.sln
dotnet run --project src/TrackingService.Api/TrackingService.Api.csproj -- --migrate
dotnet run --project src/TrackingService.Api/TrackingService.Api.csproj
```

Run the tests:

```bash
dotnet test TrackingService.sln
```

## API

The API listens on port `8080` and exposes:

- `GET /tracking/{shipmentId}`
- `GET /tracking/by-tracking-number/{trackingNumber}`
- `POST /admin/tracking-events`
- `GET /health`

Authorization behavior:

- `USER` can query timelines only for shipments they are allowed to see.
- `ADMIN` can query any timeline and create tracking events manually.
- Shipment visibility and tracking-number resolution are delegated to `shipment-service` over internal DNS using the dedicated internal shipment endpoints and shared internal secret.
- Outside Development mode the public API trusts only API-Gateway-verified identity headers accompanied by the shared proxy secret.

## Eventing

Published directly to EventBridge:

- `TrackingStatusUpdated`

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

## Docker

Build the image:

```bash
docker build -t tracking-service:latest .
```

Run locally:

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Postgres="$ConnectionStrings__Postgres" \
  -e AWS__Region="$AWS__Region" \
  -e AWS__EventBusName="$AWS__EventBusName" \
  -e ShipmentService__BaseUrl="$ShipmentService__BaseUrl" \
  tracking-service:latest
```

## Push To Amazon ECR

```bash
aws ecr get-login-password --region us-east-1 | \
docker login --username AWS --password-stdin <account-id>.dkr.ecr.us-east-1.amazonaws.com

docker tag tracking-service:latest <account-id>.dkr.ecr.us-east-1.amazonaws.com/tracking-service:latest
docker push <account-id>.dkr.ecr.us-east-1.amazonaws.com/tracking-service:latest
```

## Deployment Notes

- The service is intended to run behind API Gateway -> VPC Link -> ALB -> EKS.
- It needs outbound connectivity to `shipment-service` for shipment visibility and existence checks.
- In EKS, set `ShipmentService__BaseUrl` to the internal DNS name such as `http://shipment-service.apps.svc.cluster.local:8080`.
- The pod should run with an IRSA-backed service account that can call `events:PutEvents`.
- Use Kubernetes readiness and liveness probes against `GET /health`.
- Run migrations through `--migrate` or the Helm pre-install / pre-upgrade migration job before rolling the Deployment.
