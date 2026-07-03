# EKS kubectl Access

These instructions configure `kubectl` for the private EKS cluster `logistics-platform-dev`.

## Prerequisites

- AWS CLI v2.12.3 or later
- `kubectl` installed locally
- Network connectivity to the private cluster endpoint from inside the VPC or a connected network such as VPN, Direct Connect, Transit Gateway, or an SSM/bastion host
- An IAM principal with `eks:DescribeCluster`
- Kubernetes access for that IAM principal through the cluster creator permissions or an EKS access entry

This repository configures new EKS clusters with `API_AND_CONFIG_MAP` authentication mode so EKS access entries work by default.

## Quick Start

Check the IAM identity you are using:

```bash
aws sts get-caller-identity
```

Create or update your kubeconfig:

```bash
aws eks update-kubeconfig \
  --region us-east-1 \
  --name logistics-platform-dev
```

If you need to authenticate to the cluster with a specific IAM role, use:

```bash
aws eks update-kubeconfig \
  --region us-east-1 \
  --name logistics-platform-dev \
  --role-arn arn:aws:iam::<account-id>:role/<role-name>
```

Verify token generation and cluster connectivity:

```bash
aws eks get-token \
  --region us-east-1 \
  --cluster-name logistics-platform-dev >/dev/null

kubectl config use-context logistics-platform-dev
kubectl get namespaces
```

## Helper Scripts

Update kubeconfig and verify IAM token generation:

```bash
./infra/scripts/eks/update-kubeconfig.sh
```

Verify end-to-end access:

```bash
./infra/scripts/eks/verify-access.sh
```

Grant an IAM role or user EKS access with an access entry:

```bash
PRINCIPAL_ARN=arn:aws:iam::<account-id>:role/<role-name> \
./infra/scripts/eks/grant-access-entry.sh
```

## IAM Authentication Notes

This cluster is private-only, so `kubectl` must run from within the VPC or a connected network.

The `aws eks update-kubeconfig` command configures `kubectl` to use AWS IAM authentication through the AWS CLI exec plugin. Authentication uses the current AWS CLI identity by default. If you need a different IAM role, pass `--role-arn`.

If your IAM principal is not already authorized for Kubernetes access, create an EKS access entry and associate an access policy:

```bash
aws eks create-access-entry \
  --region us-east-1 \
  --cluster-name logistics-platform-dev \
  --principal-arn arn:aws:iam::<account-id>:role/<role-name> \
  --type STANDARD

aws eks associate-access-policy \
  --region us-east-1 \
  --cluster-name logistics-platform-dev \
  --principal-arn arn:aws:iam::<account-id>:role/<role-name> \
  --policy-arn arn:aws:eks::aws:cluster-access-policy/AmazonEKSClusterAdminPolicy \
  --access-scope type=cluster
```

`kubectl auth can-i` is not a reliable validation step for EKS access policies. Use `aws eks get-token`, `kubectl cluster-info`, or a real resource read such as `kubectl get namespaces` instead.

## References

- AWS CLI `update-kubeconfig`: https://docs.aws.amazon.com/cli/latest/reference/eks/update-kubeconfig.html
- Amazon EKS kubeconfig setup: https://docs.aws.amazon.com/eks/latest/userguide/create-kubeconfig.html
- Amazon EKS private endpoint access: https://docs.aws.amazon.com/eks/latest/userguide/cluster-endpoint.html
- Amazon EKS access entries: https://docs.aws.amazon.com/eks/latest/userguide/create-standard-access-entry-policy.html
- Amazon EKS access policies: https://docs.aws.amazon.com/eks/latest/userguide/access-policies.html
