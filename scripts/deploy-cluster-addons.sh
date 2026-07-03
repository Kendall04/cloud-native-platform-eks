#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENVIRONMENT="${ENVIRONMENT:-dev}"
EKS_DIR="${EKS_DIR:-${REPO_ROOT}/infra/live/${ENVIRONMENT}/eks}"
CHART_DIR="${CHART_DIR:-${REPO_ROOT}/k8s/charts/cluster-addons}"
VALUES_FILE="${VALUES_FILE:-${CHART_DIR}/values-${ENVIRONMENT}.yaml}"
RELEASE_NAME="${RELEASE_NAME:-cluster-addons}"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 1
  fi
}

terragrunt_output_raw() {
  local output_name="$1"
  (
    cd "${EKS_DIR}"
    terragrunt output -raw "${output_name}"
  )
}

require_output() {
  local output_name="$1"
  local value

  if ! value="$(terragrunt_output_raw "${output_name}" 2>/dev/null)" || [[ -z "${value}" || "${value}" == "null" ]]; then
    echo "Required Terragrunt output is missing or empty: ${output_name}" >&2
    exit 1
  fi

  printf '%s\n' "${value}"
}

require_command helm
require_command terragrunt

if [[ ! -d "${EKS_DIR}" ]]; then
  echo "EKS stack directory not found: ${EKS_DIR}" >&2
  exit 1
fi

if [[ ! -f "${VALUES_FILE}" ]]; then
  echo "Values file not found: ${VALUES_FILE}" >&2
  exit 1
fi

cluster_name="$(require_output cluster_name)"
region="$(require_output region)"
vpc_id="$(require_output vpc_id)"
alb_role_arn="$(require_output aws_load_balancer_controller_role_arn)"
alb_namespace="$(require_output aws_load_balancer_controller_namespace)"
alb_service_account_name="$(require_output aws_load_balancer_controller_service_account_name)"
cluster_autoscaler_role_arn="$(require_output cluster_autoscaler_role_arn)"
cluster_autoscaler_namespace="$(require_output cluster_autoscaler_namespace)"
cluster_autoscaler_service_account_name="$(require_output cluster_autoscaler_service_account_name)"

if [[ "${alb_namespace}" != "${cluster_autoscaler_namespace}" ]]; then
  echo "The cluster-addons chart expects both addons to share a namespace, but got ${alb_namespace} and ${cluster_autoscaler_namespace}." >&2
  exit 1
fi

tmp_chart_dir="$(mktemp -d)"
generated_values_file="$(mktemp)"
cleanup() {
  rm -rf "${tmp_chart_dir}" "${generated_values_file}"
}
trap cleanup EXIT

cp -R "${CHART_DIR}/." "${tmp_chart_dir}/"

cat >"${generated_values_file}" <<EOF
aws-load-balancer-controller:
  clusterName: ${cluster_name}
  region: ${region}
  vpcId: ${vpc_id}
  serviceAccount:
    name: ${alb_service_account_name}
    annotations:
      eks.amazonaws.com/role-arn: ${alb_role_arn}

cluster-autoscaler:
  awsRegion: ${region}
  autoDiscovery:
    clusterName: ${cluster_name}
  rbac:
    serviceAccount:
      name: ${cluster_autoscaler_service_account_name}
      annotations:
        eks.amazonaws.com/role-arn: ${cluster_autoscaler_role_arn}
EOF

echo "==> Building cluster-addons chart dependencies"
helm dependency build "${tmp_chart_dir}" >/dev/null

echo "==> Deploying ${RELEASE_NAME} to namespace ${alb_namespace}"
helm upgrade --install "${RELEASE_NAME}" "${tmp_chart_dir}" \
  --namespace "${alb_namespace}" \
  --wait \
  --atomic \
  --timeout 10m \
  -f "${VALUES_FILE}" \
  -f "${generated_values_file}"

echo "Cluster addons deployed for environment ${ENVIRONMENT}."
