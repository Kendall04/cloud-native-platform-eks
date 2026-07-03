#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

ENVIRONMENT="${ENVIRONMENT:-dev}"
CHART_DIR="${CHART_DIR:-${REPO_ROOT}/k8s/charts/platform-services}"
VALUES_FILE="${VALUES_FILE:-${REPO_ROOT}/k8s/environments/${ENVIRONMENT}/platform-services.values.yaml}"
RELEASE_NAME="${RELEASE_NAME:-platform-services}"
NAMESPACE="${NAMESPACE:-apps}"
HELM_TIMEOUT="${HELM_TIMEOUT:-10m}"
CREATE_NAMESPACE="${CREATE_NAMESPACE:-true}"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 1
  fi
}

require_command helm

if [[ ! -d "${CHART_DIR}" ]]; then
  echo "Chart directory not found: ${CHART_DIR}" >&2
  exit 1
fi

if [[ ! -f "${VALUES_FILE}" ]]; then
  echo "Values file not found: ${VALUES_FILE}" >&2
  exit 1
fi

create_namespace_args=()
if [[ "${CREATE_NAMESPACE}" == "true" ]]; then
  create_namespace_args+=(--create-namespace)
fi

echo "==> Deploying ${RELEASE_NAME} for environment ${ENVIRONMENT}"
echo "==> Values file: ${VALUES_FILE}"

helm upgrade --install "${RELEASE_NAME}" "${CHART_DIR}" \
  --namespace "${NAMESPACE}" \
  "${create_namespace_args[@]}" \
  --wait \
  --atomic \
  --timeout "${HELM_TIMEOUT}" \
  -f "${VALUES_FILE}"
