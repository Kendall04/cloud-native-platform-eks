# Manual Migration Guide

This repository uses explicit EF Core migrations for the three EKS-hosted services.
Normal web startup does not apply schema changes automatically.

## How Migrations Work Here

- `auth-service`, `shipment-service`, and `tracking-service` all support `--migrate`.
- The entrypoint checks for that argument in `Program.cs`, resolves a `DatabaseInitializer`, runs it, and exits.
- Each initializer creates its PostgreSQL schema if it does not exist yet and then calls `Database.MigrateAsync()`.
- `auth-service` also seeds the `USER` and `ADMIN` roles and can create the bootstrap admin user if `BootstrapAdmin__*` values are present.
- Helm renders one hook `Job` per service with `pre-install,pre-upgrade`, so those jobs only run during `helm install` or `helm upgrade`.

PostgreSQL layout:

- logical database: `platform`
- schema per service:
  - `auth`
  - `shipment`
  - `tracking`

Tables created by the initial migrations:

- `auth`: ASP.NET Identity tables plus `RefreshTokens`
- `shipment`: `Shipments`, `ProcessedEvents`
- `tracking`: `TrackingEvents`

## Before You Run Anything

Make sure these are true first:

1. `kubectl` points to the correct EKS cluster.
2. The `apps` namespace exists.
3. `platform-runtime-secrets` exists in `apps`.
4. The Deployments are already running with the intended images and environment variables.

Useful checks:

```bash
kubectl config current-context
kubectl get ns apps
kubectl -n apps get secret platform-runtime-secrets
kubectl -n apps get deploy
kubectl -n apps get configmap
```

## Option 1: Most Manual And Best For Learning

This does not create a Kubernetes `Job`.
It launches a second process inside an already-running pod, using the same image and environment variables as the Deployment.

### auth-service

```bash
AUTH_POD="$(kubectl -n apps get pod -l app.kubernetes.io/name=auth-service -o jsonpath='{.items[0].metadata.name}')"

kubectl -n apps exec "$AUTH_POD" -- /bin/sh -lc 'cd /app && dotnet AuthService.Api.dll --migrate'
```

### shipment-service

```bash
SHIPMENT_POD="$(kubectl -n apps get pod -l app.kubernetes.io/name=shipment-service -o jsonpath='{.items[0].metadata.name}')"

kubectl -n apps exec "$SHIPMENT_POD" -- /bin/sh -lc 'cd /app && dotnet ShipmentService.Api.dll --migrate'
```

### tracking-service

```bash
TRACKING_POD="$(kubectl -n apps get pod -l app.kubernetes.io/name=tracking-service -o jsonpath='{.items[0].metadata.name}')"

kubectl -n apps exec "$TRACKING_POD" -- /bin/sh -lc 'cd /app && dotnet TrackingService.Api.dll --migrate'
```

Why this is useful:

- you verify that the application itself can reach RDS
- you reuse the exact runtime secret/config already mounted in the Deployment
- you see the same `--migrate` path that the Helm hook jobs use internally

## Option 2: Manual Kubernetes Job

This is the closest manual equivalent to the Helm migration hook.
It creates a one-off `Job` explicitly, lets you inspect it, follow logs, and delete it afterward.

### auth-service job

```bash
AUTH_IMAGE="$(kubectl -n apps get deploy auth-service -o jsonpath='{.spec.template.spec.containers[0].image}')"

cat <<EOF | sed "s#AUTH_IMAGE#$AUTH_IMAGE#g" | kubectl apply -f -
apiVersion: batch/v1
kind: Job
metadata:
  name: auth-service-migrate-manual
  namespace: apps
spec:
  backoffLimit: 1
  ttlSecondsAfterFinished: 300
  template:
    spec:
      restartPolicy: Never
      automountServiceAccountToken: false
      containers:
        - name: migrate
          image: AUTH_IMAGE
          imagePullPolicy: IfNotPresent
          args:
            - --migrate
          envFrom:
            - configMapRef:
                name: auth-service-config
          env:
            - name: ConnectionStrings__Postgres
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: auth-connection-string
            - name: Jwt__Secret
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: auth-jwt-secret
            - name: BootstrapAdmin__Email
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: auth-bootstrap-admin-email
            - name: BootstrapAdmin__Password
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: auth-bootstrap-admin-password
EOF

kubectl -n apps logs -f job/auth-service-migrate-manual
kubectl -n apps wait --for=condition=complete job/auth-service-migrate-manual --timeout=5m
kubectl -n apps delete job auth-service-migrate-manual
```

### shipment-service job

```bash
SHIPMENT_IMAGE="$(kubectl -n apps get deploy shipment-service -o jsonpath='{.spec.template.spec.containers[0].image}')"

cat <<EOF | sed "s#SHIPMENT_IMAGE#$SHIPMENT_IMAGE#g" | kubectl apply -f -
apiVersion: batch/v1
kind: Job
metadata:
  name: shipment-service-migrate-manual
  namespace: apps
spec:
  backoffLimit: 1
  ttlSecondsAfterFinished: 300
  template:
    spec:
      restartPolicy: Never
      automountServiceAccountToken: false
      containers:
        - name: migrate
          image: SHIPMENT_IMAGE
          imagePullPolicy: IfNotPresent
          args:
            - --migrate
          envFrom:
            - configMapRef:
                name: shipment-service-config
          env:
            - name: ConnectionStrings__Postgres
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: shipment-connection-string
            - name: PlatformAuth__TrustedProxySecret
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: platform-trusted-proxy-secret
            - name: PlatformAuth__InternalServiceSecret
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: platform-internal-service-secret
EOF

kubectl -n apps logs -f job/shipment-service-migrate-manual
kubectl -n apps wait --for=condition=complete job/shipment-service-migrate-manual --timeout=5m
kubectl -n apps delete job shipment-service-migrate-manual
```

### tracking-service job

```bash
TRACKING_IMAGE="$(kubectl -n apps get deploy tracking-service -o jsonpath='{.spec.template.spec.containers[0].image}')"

cat <<EOF | sed "s#TRACKING_IMAGE#$TRACKING_IMAGE#g" | kubectl apply -f -
apiVersion: batch/v1
kind: Job
metadata:
  name: tracking-service-migrate-manual
  namespace: apps
spec:
  backoffLimit: 1
  ttlSecondsAfterFinished: 300
  template:
    spec:
      restartPolicy: Never
      automountServiceAccountToken: false
      containers:
        - name: migrate
          image: TRACKING_IMAGE
          imagePullPolicy: IfNotPresent
          args:
            - --migrate
          envFrom:
            - configMapRef:
                name: tracking-service-config
          env:
            - name: ConnectionStrings__Postgres
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: tracking-connection-string
            - name: PlatformAuth__TrustedProxySecret
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: platform-trusted-proxy-secret
            - name: PlatformAuth__InternalServiceSecret
              valueFrom:
                secretKeyRef:
                  name: platform-runtime-secrets
                  key: platform-internal-service-secret
EOF

kubectl -n apps logs -f job/tracking-service-migrate-manual
kubectl -n apps wait --for=condition=complete job/tracking-service-migrate-manual --timeout=5m
kubectl -n apps delete job tracking-service-migrate-manual
```

## Option 3: Re-Run The Helm Hooks Intentionally

This is the normal automated path for this repository.
The hook jobs do not run by themselves after the chart is already installed.
They run only during `helm install` or `helm upgrade`.

Example:

```bash
helm upgrade --install platform-services \
  ./k8s/charts/platform-services \
  --namespace apps \
  --create-namespace \
  -f ./k8s/environments/dev/platform-services.values.yaml
```

What to expect:

- Helm creates `auth-service-migrate`, `shipment-service-migrate`, and `tracking-service-migrate`
- those jobs run before the Deployments roll
- successful hook jobs are deleted because the chart uses `helm.sh/hook-delete-policy: before-hook-creation,hook-succeeded`
- even if the hook object disappears, the migration result remains in PostgreSQL

## How To Verify The Result

At the Kubernetes level:

```bash
kubectl -n apps get jobs
kubectl -n apps describe job auth-service-migrate-manual
kubectl -n apps logs job/auth-service-migrate-manual
```

At the database level, check the schemas and migration history:

```sql
SELECT schema_name
FROM information_schema.schemata
WHERE schema_name IN ('auth', 'shipment', 'tracking');

SELECT * FROM auth."__EFMigrationsHistory";
SELECT * FROM shipment."__EFMigrationsHistory";
SELECT * FROM tracking."__EFMigrationsHistory";
```

Expected initial migration ids:

- `auth`: `202603110001_InitialCreate`
- `shipment`: `202603110101_InitialCreate`
- `tracking`: `202603120001_InitialCreate`

## Recommended Learning Flow

1. Run `auth-service` first with `kubectl exec`.
2. Verify the `auth` schema and the seeded roles/admin behavior.
3. Run `shipment-service`.
4. Run `tracking-service`.
5. Repeat one service using the manual `Job` method so you can inspect lifecycle, pod logs, and cleanup.
6. Once that is clear, use `helm upgrade` to understand how hooks automate the same path.
