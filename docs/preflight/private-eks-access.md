# Private EKS Access Validation

Execution date: 2026-07-03T17:04:28Z

## Scope

This phase inspected and tested private operational access paths for the
private-only EKS cluster API endpoint.

No Terraform/Terragrunt apply, destroy, security group change, IAM policy change,
EKS endpoint change, Helm install, Kubernetes apply/delete, workload deploy, or
Git push was executed.

## Execution Context

- Branch: `chore/private-eks-access-validation`
- AWS account: `1450********8802`
- AWS identity: `arn:aws:iam::145023118802:user/terraform-lab`
- Region: `us-east-1`
- Cluster: `logistics-platform-dev`

## EKS Status

AWS CLI validation:

- Cluster status: `ACTIVE`
- Kubernetes version: `1.35`
- VPC: `vpc-0fe33938202034387`
- Endpoint private access: `true`
- Endpoint public access: `false`
- OIDC issuer:
  `https://oidc.eks.us-east-1.amazonaws.com/id/C45613EAD1A4FB94AC9D9AA7D391C4FC`
- Node groups:
  - `api-node-group`
  - `worker-node-group`
- Addons:
  - `aws-ebs-csi-driver`
  - `coredns`
  - `kube-proxy`
  - `vpc-cni`

Local `kubectl` cannot connect from the current workstation because the EKS API
endpoint is private-only and this machine has no network route into the VPC.

## NAT Instances Inspected

The existing NAT EC2 instances are in the EKS VPC and have SSM instance profiles:

| Instance ID | AZ | Subnet | Private IP | Public IP | State | SSM |
| --- | --- | --- | --- | --- | --- | --- |
| `i-02ee098265fcb9b88` | `us-east-1a` | `subnet-0c53db864ff972e83` | `10.0.3.90` | `34.227.237.179` | `running` | `Online` |
| `i-0995259bf6eee77c8` | `us-east-1b` | `subnet-0ec9f42d9e1c9d10f` | `10.0.17.7` | `54.204.16.56` | `running` | `Online` |
| `i-033abe7faebb2185d` | `us-east-1c` | `subnet-083d616c63f1dda98` | `10.0.37.227` | `34.236.66.48` | `running` | `Online` |

All three instances are Amazon Linux instances with SSM agent `3.3.4624.0`.

## SSM Test Instance

Selected instance:

```text
i-02ee098265fcb9b88
```

Reason:

- It is `running`.
- It is in the EKS VPC.
- It is registered and `Online` in SSM.
- It has the SSM instance profile
  `cloud-native-platform-dev-us-east-1a-nat-ssm-profile`.

## Tooling Validation Through SSM

SSM Run Command succeeded for basic tooling inspection.

Available:

- `aws`
- `curl`

Missing:

- `kubectl`

Observed output:

```text
aws-cli/2.33.15 Python/3.9.25 Linux/6.1.175-219.359.amzn2023.x86_64
/usr/bin/aws
kubectl: command not found
```

No tools were installed because this phase is read-only and the NAT instances
should not become permanent bastions.

## AWS CLI Permission Test From NAT

The NAT role can assume its instance profile and call STS, but cannot describe
the EKS cluster:

```text
arn:aws:sts::145023118802:assumed-role/cloud-native-platform-dev-us-east-1a-nat-ssm-role/i-02ee098265fcb9b88
AccessDeniedException: not authorized to perform: eks:DescribeCluster
```

Attached managed policies on the NAT role:

```text
AmazonSSMManagedInstanceCore
```

Inline policies:

```text
none
```

No IAM policies were changed.

## Private Endpoint Network Test From NAT

DNS resolution from the NAT instance works for the private EKS API endpoint:

```text
10.0.148.69 C45613EAD1A4FB94AC9D9AA7D391C4FC.gr7.us-east-1.eks.amazonaws.com
10.0.135.91 C45613EAD1A4FB94AC9D9AA7D391C4FC.gr7.us-east-1.eks.amazonaws.com
```

TCP/HTTPS access to the API endpoint timed out:

```text
curl: (28) Connection timed out after 5002 milliseconds
```

Read-only security group inspection showed the EKS cluster security group only
allows inbound traffic from itself:

```text
sg-0216c41ff3acc9eb2
inbound: self-reference only
```

The selected NAT instance uses security group:

```text
sg-0b7207ce5dcbaf71a
```

No security group rules were changed.

## Kubectl Read-Only Result

`kubectl get nodes` and `kubectl get pods -A` were not executed successfully
from inside the VPC because the tested SSM path is missing required prerequisites:

- `kubectl` is not installed on the NAT instance.
- The NAT instance role lacks `eks:DescribeCluster`.
- The EKS cluster security group does not allow API server access from the NAT
  instance security group.

This is an access-path blocker, not an EKS health failure. AWS CLI validation
from the operator machine already confirmed the EKS cluster, node groups, addons,
OIDC provider, and IRSA prerequisites are healthy.

## Recommendation

Do not use NAT instances as a permanent bastion or mutate them ad hoc.

Recommended next phase:

1. Create a small private management EC2 instance with SSM in the EKS VPC.
2. Attach a narrowly scoped IAM role for read-only EKS access and SSM.
3. Install or bake required tools through a controlled bootstrap:
   - AWS CLI
   - `kubectl`
   - optionally Helm for a later phase
4. Allow API server access from the management instance security group to the EKS
   private endpoint security group.
5. Validate only read-only commands first:
   - `aws eks update-kubeconfig`
   - `kubectl get nodes -o wide`
   - `kubectl get pods -A`

Alternative options:

- Use VPN/peering/direct private network access if already available.
- Temporarily enable public endpoint access with strict CIDR only as an explicit
  later decision and last resort.

## Explicitly Not Executed

- `terraform apply`
- `terragrunt apply`
- `terragrunt run-all apply`
- `kubectl apply`
- `kubectl delete`
- `helm upgrade --install`
- workload deploys
- destroy
- EKS endpoint changes
- security group changes
- IAM policy/role changes
- package/software installation on NAT instances
- Git push
