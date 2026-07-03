#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DEV_DIR="${REPO_ROOT}/infra/live/dev"
AUTO_APPROVE="${AUTO_APPROVE:-false}"

STACKS=(
  "vpc"
  "nat"
  "s3"
  "ecr"
  "sqs"
  "eventbridge"
  "rds"
  "eks"
  "api-gateway-authorizer"
  "notification-lambda"
  "iam"
  "apigateway-core"
)

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 1
  fi
}

terragrunt_apply() {
  local stack="$1"
  local stack_dir="${DEV_DIR}/${stack}"
  local cmd=(terragrunt apply)

  if [[ ! -d "${stack_dir}" ]]; then
    echo "Stack directory not found: ${stack_dir}" >&2
    exit 1
  fi

  if [[ "${AUTO_APPROVE}" == "true" ]]; then
    cmd+=(-auto-approve)
  fi

  echo "==> Applying ${stack}"
  (
    cd "${stack_dir}"
    "${cmd[@]}"
  )
}

require_command terragrunt

for stack in "${STACKS[@]}"; do
  terragrunt_apply "${stack}"
done

cat <<'EOF'
Base infrastructure bootstrap completed.

What this script intentionally did not do:
- apply apigateway-integration
- deploy cluster-wide Helm addons
- deploy application Helm workloads
- build or push container images

Next manual phase:
1. Deploy the cluster addons with ./scripts/deploy-cluster-addons.sh.
2. Build and push images.
3. Prepare runtime secrets.
4. Deploy the platform-services chart with ./scripts/deploy-platform-services.sh.
5. Apply infra/live/dev/apigateway-integration.
EOF
