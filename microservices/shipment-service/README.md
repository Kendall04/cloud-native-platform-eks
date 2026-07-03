# shipment-service

`shipment-service` owns the `Shipment` aggregate for the logistics platform. It exposes create and read APIs, maintains the current shipment status, consumes tracking updates from SQS, and publishes shipment domain events to EventBridge.

## Stack

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- AWS SDK for .NET
- Amazon EventBridge
- Amazon SQS background consumer
- Docker

## Solution Layout

```text
shipment-service/
  src/
    ShipmentService.Api/
    ShipmentService.Application/
    ShipmentService.Domain/
    ShipmentService.Infrastructure/
  tests/
    ShipmentService.Tests/
```

## Configuration

Set the following environment variables before running the service:

```bash
export ConnectionStrings__Postgres="Host=rds-endpoint;Database=platform;Username=platform_admin;Password=securepassword"
export Database__Schema="shipment"
export AWS__Region="us-east-1"
export AWS__EventBusName="cloud-native-platform-dev-bus"
export AWS__ShipmentEventsQueueUrl="https://sqs.us-east-1.amazonaws.com/<account-id>/cloud-native-platform-dev-shipment-events-queue"
export PlatformAuth__TrustedProxySecret="<32-byte-or-longer-shared-secret>"
export PlatformAuth__InternalServiceSecret="<32-byte-or-longer-internal-secret>"
export ASPNETCORE_ENVIRONMENT="Development"
```

Optional:

```bash
export ASPNETCORE_URLS="http://+:8080"
```

## Local Run

```bash
cd microservices/shipment-service
dotnet restore ShipmentService.sln
dotnet build ShipmentService.sln
dotnet run --project src/ShipmentService.Api/ShipmentService.Api.csproj -- --migrate
dotnet run --project src/ShipmentService.Api/ShipmentService.Api.csproj
```

Run the tests:

```bash
dotnet test ShipmentService.sln
```

## API

The API listens on port `8080` and exposes:

- `POST /shipments`
- `GET /shipments/{id}`
- `GET /shipments`
- `GET /shipments/by-tracking/{trackingNumber}`
- `PATCH /admin/shipments/{id}`
- `GET /internal/shipments/{id}/exists`
- `GET /internal/shipments/{id}`
- `GET /internal/shipments/by-tracking/{trackingNumber}`
- `GET /health`

Authorization behavior:

- `USER` can create and read only their own shipments.
- `ADMIN` can list all shipments and update shipment metadata.
- Outside Development mode the service trusts only API-Gateway-verified identity headers accompanied by the shared proxy secret.
- `/internal/*` is reserved for in-cluster service validation, is not exposed through ingress, and requires the shared internal service secret in non-development environments.

## Eventing

Outgoing events published directly to EventBridge:

- `ShipmentCreated`
- `ShipmentStatusChanged`

Incoming tracking events consumed from SQS:

- `TrackingStatusUpdated`

The worker is idempotent through the `ProcessedEvents` table and safely ignores:

- duplicate events
- out-of-order events
- invalid backward status transitions

## Docker

Build the image:

```bash
docker build -t shipment-service:latest .
```

Run locally:

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Postgres="$ConnectionStrings__Postgres" \
  -e AWS__Region="$AWS__Region" \
  -e AWS__EventBusName="$AWS__EventBusName" \
  -e AWS__ShipmentEventsQueueUrl="$AWS__ShipmentEventsQueueUrl" \
  shipment-service:latest
```

## Push To Amazon ECR

```bash
aws ecr get-login-password --region us-east-1 | \
docker login --username AWS --password-stdin <account-id>.dkr.ecr.us-east-1.amazonaws.com

docker tag shipment-service:latest <account-id>.dkr.ecr.us-east-1.amazonaws.com/shipment-service:latest
docker push <account-id>.dkr.ecr.us-east-1.amazonaws.com/shipment-service:latest
```

## Deployment Notes

- The service is intended to run behind API Gateway -> VPC Link -> ALB -> EKS.
- The pod should run with the IRSA-backed service account `apps/shipment-service`.
- Terraform now provisions the custom EventBridge bus, the shipment events queue and DLQ, and the IRSA role used by the service.
- `tracking-service` validates shipment visibility and existence through the internal DNS target `http://shipment-service.apps.svc.cluster.local:8080`.
- Use Kubernetes readiness and liveness probes against `GET /health`.
- Run migrations through `--migrate` or the Helm pre-install / pre-upgrade migration job before rolling the Deployment.
