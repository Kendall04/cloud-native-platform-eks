# Platform Services Image Build And Runtime Values Preparation

Date: 2026-07-04T04:17:35Z

Branch: `feat/platform-service-images`

AWS account: `145023118802`

Region: `us-east-1`

## Goal

Build and push the dev platform service images to ECR in the active AWS account, capture immutable image digests, and prepare the dev `platform-services` values file for a future workload install.

No workloads were installed.

## Source Revision

Git SHA used for image tags:

- `1d9604a7ef25`

The same value was recorded in:

- `release.commitSha`
- each service image `tag`

No `latest` tag was used.

## Services Built

`auth-service`:

- Dockerfile: `microservices/auth-service/Dockerfile`
- Build context: `microservices/auth-service`
- ECR repository: `145023118802.dkr.ecr.us-east-1.amazonaws.com/auth-service`
- Tag: `1d9604a7ef25`
- Digest: `sha256:d6474872d5dd2db4394c6fd09be590e68600f47783fb75622ef2f66bb8170ab1`

`shipment-service`:

- Dockerfile: `microservices/shipment-service/Dockerfile`
- Build context: `microservices/shipment-service`
- ECR repository: `145023118802.dkr.ecr.us-east-1.amazonaws.com/shipment-service`
- Tag: `1d9604a7ef25`
- Digest: `sha256:fe6e919ac6a5abee433050bde9931bb2b74057f1fa649c291dbc929118bee941`

`tracking-service`:

- Dockerfile: `microservices/tracking-service/Dockerfile`
- Build context: `microservices/tracking-service`
- ECR repository: `145023118802.dkr.ecr.us-east-1.amazonaws.com/tracking-service`
- Tag: `1d9604a7ef25`
- Digest: `sha256:dda2e02c76f7d247de8ff51a1a6268bcc3eecd93e65d37216655641f30379265`

## Tests

.NET tests were run before image push:

- `auth-service`: 4 passed, 0 failed
- `shipment-service`: 7 passed, 0 failed
- `tracking-service`: 5 passed, 0 failed

Warnings observed:

- Nullable warnings in service code.
- EF Core `ExecuteSqlRawAsync` warning in database initializer code.
- AWS SDK obsolete attribute warning in shipment SQS consumer code.

No test failure blocked the image build.

## ECR Validation

Validated repositories:

- `auth-service`
- `shipment-service`
- `tracking-service`

All repositories are in registry/account `145023118802`.

All repositories are configured as `IMMUTABLE`.

Images were pushed only to:

- `145023118802.dkr.ecr.us-east-1.amazonaws.com`

No images were pushed to the previous account `795708473882`.

Digest evidence is stored locally under:

- `/tmp/cloud-native-platform-platform-images/auth-service-image.json`
- `/tmp/cloud-native-platform-platform-images/shipment-service-image.json`
- `/tmp/cloud-native-platform-platform-images/tracking-service-image.json`

Push logs are stored locally under:

- `/tmp/cloud-native-platform-platform-images/auth-service-push.log`
- `/tmp/cloud-native-platform-platform-images/shipment-service-push.log`
- `/tmp/cloud-native-platform-platform-images/tracking-service-push.log`

## Values Updated

Updated:

- `k8s/environments/dev/platform-services.values.yaml`

Changes:

- Replaced old ECR account `795708473882` with `145023118802`.
- Set all service image tags to `1d9604a7ef25`.
- Set all service image digests to ECR-confirmed `sha256:` digests.
- Updated service IRSA role ARNs in dev values to account `145023118802`.
- Updated shipment SQS queue URL and KEDA queue URL to account `145023118802`.
- Set `release.commitSha` to `1d9604a7ef25`.

The values still reference the planned runtime secret name:

- `platform-runtime-secrets`

No Kubernetes Secret was created.

## Helm Validation

Validated:

- `helm lint ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml`
- `helm template platform-services ./k8s/charts/platform-services -f ./k8s/environments/dev/platform-services.values.yaml --namespace apps --debug`

Result:

- lint OK
- template OK

Rendered manifest evidence:

- `/tmp/cloud-native-platform-platform-images/platform-services-rendered-with-digests.yaml`

Rendered image references use immutable digests:

- `145023118802.dkr.ecr.us-east-1.amazonaws.com/auth-service@sha256:d6474872d5dd2db4394c6fd09be590e68600f47783fb75622ef2f66bb8170ab1`
- `145023118802.dkr.ecr.us-east-1.amazonaws.com/shipment-service@sha256:fe6e919ac6a5abee433050bde9931bb2b74057f1fa649c291dbc929118bee941`
- `145023118802.dkr.ecr.us-east-1.amazonaws.com/tracking-service@sha256:dda2e02c76f7d247de8ff51a1a6268bcc3eecd93e65d37216655641f30379265`

Validation confirmed:

- No rendered image reference uses account `795708473882`.
- No rendered service image digest is empty.
- The chart renders `repository@digest` when a digest is provided.

## Cluster State Confirmation

Read-only validation from the private management EC2 confirmed:

- Namespace `apps` does not exist.
- Secret `platform-runtime-secrets` does not exist.
- Helm release `platform-services` is not installed.
- Existing Helm release `cluster-addons` remains deployed.

This is expected for this phase.

## Remaining Blockers

Workload install is still blocked by:

- Namespace `apps` has not been created.
- Kubernetes Secret `platform-runtime-secrets` has not been created.
- Runtime secrets strategy is not implemented yet.
- Service IRSA roles for `shipment-service` and `tracking-service` have not been applied yet.
- The `iam` stack has not been applied.
- Workload install has not been approved or executed.
- API Gateway integration must wait until the internal ALB exists and is validated.

## Validation Commands

Additional validation completed:

- `terraform fmt -check -recursive infra`
- `terragrunt hcl format --check infra/live`
- `bash -n scripts/bootstrap-terraform-backend.sh`
- `shellcheck scripts/bootstrap-terraform-backend.sh`
- `helm dependency build` for `cluster-addons`
- `helm lint` for `cluster-addons`
- `helm template` for `cluster-addons`

## Out Of Scope

Not executed:

- `helm upgrade --install`
- `kubectl apply`
- `kubectl delete`
- workload deployment
- namespace creation
- Kubernetes Secret creation
- Terraform/Terragrunt apply
- Terraform/Terragrunt destroy
- API Gateway apply
- Lambda apply
- Git push

## Recommendation

Do not proceed directly to workload install yet.

Recommended next phase:

- Apply or otherwise prepare the service IAM/IRSA path.
- Define and create runtime secrets through an approved secret strategy.
- Create or manage the `apps` namespace intentionally.
- Render final `platform-services` values again after those blockers are resolved.
- Keep API Gateway integration blocked until the internal ALB exists and is validated.
