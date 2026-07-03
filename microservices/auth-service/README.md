# auth-service

`auth-service` is the authentication provider for the logistics platform. It exposes registration, login, JWT issuance, refresh token rotation, user profile, token validation, and admin user management endpoints.

## Stack

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- ASP.NET Identity
- PostgreSQL
- JWT bearer authentication
- Docker

## Solution Layout

```text
auth-service/
  src/
    AuthService.Api/
    AuthService.Application/
    AuthService.Domain/
    AuthService.Infrastructure/
  tests/
    AuthService.Tests/
```

## Configuration

Set the following environment variables before running the service:

```bash
export ConnectionStrings__Postgres="Host=rds-endpoint;Database=platform;Username=platform_admin;Password=securepassword"
export Database__Schema="auth"
export Jwt__Issuer="logistics-platform"
export Jwt__Audience="logistics-platform-clients"
export Jwt__Secret="replace-with-a-32-byte-or-longer-secret"
export Jwt__ExpirationMinutes="60"
export ASPNETCORE_ENVIRONMENT="Development"
```

Optional:

```bash
export ASPNETCORE_URLS="http://+:8080"
export PORT="8080"
export BootstrapAdmin__Email="admin@logistics.local"
export BootstrapAdmin__Password="ChangeMe123!"
export BootstrapAdmin__FirstName="Platform"
export BootstrapAdmin__LastName="Admin"
```

`BootstrapAdmin__*` is optional, but it is the simplest way to create an initial operator with both `ADMIN` and `USER` roles.

## Local Run

```bash
cd microservices/auth-service
dotnet restore AuthService.sln
dotnet build AuthService.sln
dotnet run --project src/AuthService.Api/AuthService.Api.csproj -- --migrate
dotnet run --project src/AuthService.Api/AuthService.Api.csproj
```

Run the test project:

```bash
dotnet test AuthService.sln
```

The API listens on port `8080` and exposes:

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`
- `GET /auth/me`
- `GET /auth/validate`
- `POST /admin/users/{id}/disable`
- `GET /admin/users`
- `GET /health`

## Docker

Build the image:

```bash
docker build -t auth-service:latest .
```

Run locally:

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Postgres="$ConnectionStrings__Postgres" \
  -e Jwt__Issuer="$Jwt__Issuer" \
  -e Jwt__Audience="$Jwt__Audience" \
  -e Jwt__Secret="$Jwt__Secret" \
  -e Jwt__ExpirationMinutes="$Jwt__ExpirationMinutes" \
  auth-service:latest
```

## Push To Amazon ECR

```bash
aws ecr get-login-password --region us-east-1 | \
docker login --username AWS --password-stdin <account-id>.dkr.ecr.us-east-1.amazonaws.com

docker tag auth-service:latest <account-id>.dkr.ecr.us-east-1.amazonaws.com/auth-service:latest
docker push <account-id>.dkr.ecr.us-east-1.amazonaws.com/auth-service:latest
```

## Deploy Notes

- The service is intended to run behind API Gateway -> VPC Link -> ALB -> EKS.
- Use Kubernetes readiness and liveness probes against `GET /health`.
- Provide secrets through Kubernetes `Secret` objects or an external secrets manager.
- Run database migrations through the explicit `--migrate` path or the Helm pre-install / pre-upgrade migration job before rolling the Deployment.

## Authentication Model

- New users are assigned the `USER` role automatically.
- `USER` and `ADMIN` roles are seeded during the explicit migration/bootstrap path.
- An optional bootstrap admin user can be seeded through `BootstrapAdmin__*` environment variables.
- Access tokens contain `userId`, `email`, and `roles` claims.
- Refresh tokens are rotated and stored in PostgreSQL.

## Migrations

The repository includes an initial EF Core migration in `src/AuthService.Infrastructure/Persistence/Migrations`.

Normal web startup no longer applies migrations automatically.
Use:

```bash
dotnet run --project src/AuthService.Api/AuthService.Api.csproj -- --migrate
```
