# Private Management EC2 With SSM

Date: 2026-07-03T17:27:40Z

Branch: `feat/private-management-ec2`

AWS account: `145023118802`

Region: `us-east-1`

## Goal

Create a clean private operational path for read-only Kubernetes validation against the private EKS cluster `logistics-platform-dev`.

This phase does not deploy workloads, install Helm releases, enable the public EKS endpoint, or mutate the existing NAT instances into bastion hosts.

## Why NAT Was Not Used

The existing NAT EC2 instances are registered in SSM, but they are intentionally not used as Kubernetes management hosts:

- They do not have `kubectl`.
- Their IAM role is limited to SSM.
- They do not have `eks:DescribeCluster`.
- Their security group is not allowed to reach the private EKS API server.
- They should remain NAT infrastructure, not permanent bastion or ops hosts.

## Design Implemented

A dedicated management stack was added at:

- `infra/live/dev/management`

A reusable module was added at:

- `infra/modules/management-host`

Resources created:

- Private EC2 management host.
- Dedicated management security group.
- IAM role and instance profile.
- SSM managed instance permissions.
- Minimal EKS IAM permission for `eks:DescribeCluster`.
- EKS access entry for the management role.
- EKS access policy association with `AmazonEKSAdminViewPolicy`.
- EKS cluster security group ingress rule allowing TCP 443 from the management security group.

The access policy is read-oriented and is used only for validation commands such as `kubectl get nodes` and `kubectl get pods -A`.

## EC2

- Instance ID: `i-01b3101eaca85d043`
- Instance type: `t3.nano`
- Private IP: `10.0.129.245`
- Public IP: none
- Subnet: `subnet-03aa254292f6017e8`
- Security group: `sg-0980b6aa338f1ef83`
- IAM instance profile: `cloud-native-platform-dev-management-profile`
- Root volume: 8 GiB gp3, encrypted
- Access method: SSM Session Manager / SSM Run Command

Tags:

- `Project=cloud-native-platform`
- `Environment=dev`
- `Role=management`
- `Name=cloud-native-platform-dev-management`

## Security Group Summary

Management SG:

- No inbound rules.
- Egress TCP 443 to `0.0.0.0/0` for SSM, AWS APIs, package downloads, and the private EKS API endpoint.
- Egress TCP/UDP 53 to the VPC CIDR for DNS.

EKS cluster SG:

- Allows TCP 443 from the management SG.

The EKS public endpoint remains disabled.

## IAM Summary

Management EC2 role:

- `AmazonSSMManagedInstanceCore`
- Inline `eks:DescribeCluster` for `logistics-platform-dev`

EKS access:

- EKS access entry for `cloud-native-platform-dev-management-role`
- EKS access policy association: `AmazonEKSAdminViewPolicy`
- Cluster scope

The initial `AmazonEKSViewPolicy` association allowed authentication but did not permit listing cluster-scoped `nodes`. It was replaced with `AmazonEKSAdminViewPolicy` to support read-only cluster-wide validation.

This is a deliberate lab-management decision, not a workload permission model. `AmazonEKSAdminViewPolicy` is privileged read access: it can list and view cluster resources, including Kubernetes Secrets metadata and values exposed through read APIs. Keep it limited to the private management role and do not reuse it for application workloads.

## Tooling Validation

SSM status:

- Instance `i-01b3101eaca85d043`: `Online`

Validated by SSM Run Command:

- AWS CLI: `aws-cli/2.33.15`
- kubectl client: `v1.35.0`
- EKS describe cluster: `ACTIVE`

The first user data attempt failed because `dnf install -y awscli curl` hit an Amazon Linux 2023 `curl`/`curl-minimal` package conflict. The user data was corrected to reuse existing AWS CLI/curl when present and only install missing packages.

## kubectl Read-Only Validation

Command path:

```bash
aws eks update-kubeconfig --region us-east-1 --name logistics-platform-dev --kubeconfig /root/.kube/config
KUBECONFIG=/root/.kube/config kubectl get nodes -o wide
KUBECONFIG=/root/.kube/config kubectl get pods -A
```

Result:

- `kubectl get nodes -o wide`: success
- `kubectl get pods -A`: success

Nodes observed:

- `ip-10-0-142-73.ec2.internal`: `Ready`
- `ip-10-0-145-254.ec2.internal`: `Ready`
- `ip-10-0-174-228.ec2.internal`: `Ready`

System pods observed:

- `aws-node`: running on all nodes
- `coredns`: running
- `ebs-csi-controller`: running
- `ebs-csi-node`: running on all nodes
- `kube-proxy`: running on all nodes

Only read-only `kubectl get` commands were executed.

## Non-Applied Scope Confirmed

Still not applied or deployed:

- API Gateway
- Lambda
- `apigateway-integration`
- Helm releases
- Application workloads

No `kubectl apply`, `kubectl delete`, Helm install, endpoint public access change, manual security group change, manual IAM change, or manual package installation was performed.

## Logs

Local logs and evidence files:

- `/tmp/cloud-native-platform-management-access/management-init.log`
- `/tmp/cloud-native-platform-management-access/management-plan.log`
- `/tmp/cloud-native-platform-management-access/management-apply.log`
- `/tmp/cloud-native-platform-management-access/management-plan-after-userdata-fix.log`
- `/tmp/cloud-native-platform-management-access/management-apply-after-userdata-fix.log`
- `/tmp/cloud-native-platform-management-access/management-plan-after-access-policy-fix.log`
- `/tmp/cloud-native-platform-management-access/management-apply-after-access-policy-fix.log`
- `/tmp/cloud-native-platform-management-access/tooling-command-result.json`
- `/tmp/cloud-native-platform-management-access/kubectl-final-command-result.json`

## Cost And Risk

Additional active cost:

- One private `t3.nano` EC2 instance.
- One small encrypted gp3 root volume.
- Existing NAT/public IPv4, EKS, RDS, EBS, S3/ECR/SQS/EventBridge costs remain active.

Risks:

- This host is an operational convenience and should stay minimal.
- Do not broaden IAM or security group access without an explicit phase.
- Consider stopping or destroying the management host when not needed if cost minimization becomes the priority.

## Next Step

Recommended next phase:

`Fase 1.15 - PR integration for private management EC2 evidence`

After integration, the platform can move toward cluster add-ons and Helm preflight from a validated private access path.
