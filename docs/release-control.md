# Release Control Specification

This repository uses a two-branch release control model:

- `dev`: active integration branch
- `main`: stable production branch

The design is intentionally branch-extensible. Adding a future `stage` branch should only require:

- a new protected branch named `stage`
- a new environment overlay at `k8s/environments/stage/platform-services.values.yaml`
- a new GitHub Environment named `stage`

## Source Control Policy

- Feature, fix, infra, and docs work lands in `dev` through pull requests.
- Promotion to production happens through a pull request from `dev` to `main`.
- Pull requests into `main` must come from `dev`.
- Direct pushes to `dev` and `main` should be blocked by branch protection.
- Change approval is satisfied through pull request review, not through a separate release-approval system.

Recommended repository settings:

- protect `dev` and `main`
- require at least one approving review
- dismiss stale approvals on new commits
- require status checks from `.github/workflows/validate.yml`
- restrict direct pushes to administrators only if your team needs break-glass access

Expected GitHub variables and secrets:

- repository variable `AWS_REGION`
- repository secret `AWS_GITHUB_ACTIONS_ROLE_ARN` for image build and release-tag operations
- environment variable `EKS_CLUSTER_NAME` on `dev` and `prod`
- optional environment secret override `AWS_GITHUB_ACTIONS_ROLE_ARN` if deploys use environment-specific roles

## Commit And Versioning Rules

- Conventional Commits are required.
- A scope is required on every commit and PR title.
- Semantic versioning is manual.
- Official release identifiers are Git tags in the format `vMAJOR.MINOR.PATCH`.
- Pre-release tags are intentionally out of scope for now.

Recommended commit types for this repository:

- `feat(scope): ...`
- `fix(scope): ...`
- `refactor(scope): ...`
- `build(scope): ...`
- `ci(scope): ...`
- `docs(scope): ...`
- `test(scope): ...`
- `chore(scope): ...`
- `release(scope): ...`

Recommended scopes:

- `auth`
- `shipment`
- `tracking`
- `infra`
- `eks`
- `k8s`
- `release`
- `ci`
- `docs`
- `repo`

The scope should describe the primary affected area. It does not need to come from a fixed whitelist.

## Artifact Model

The repository follows a strict build-once model for service images:

1. Pushes to `dev` or `main` build images once.
2. Those images receive an immutable commit tag: `sha-<full commit SHA>`.
3. A manual release operation on `main` adds the release tag `vX.Y.Z` to the exact same image digest.
4. Deployments use the image digest, not the tag.

This gives each release four linked identifiers:

| Concept | Example | Purpose |
| --- | --- | --- |
| Git tag | `v1.4.2` | official release identifier |
| Commit tag | `sha-5f0f0d4f1a...` | immutable build trace to source |
| Image digest | `sha256:abcd...` | exact deployable artifact |
| Values metadata | `release.version`, `release.gitTag`, `release.commitSha` | deployment traceability in repo |

Tags remain useful for humans, but cluster deployments must use `repository@sha256:...`.

## Repository Structure

Release control data lives in four places:

```text
.github/
  PULL_REQUEST_TEMPLATE.md
  release.yml
  workflows/
    validate.yml
    build-sha-images.yml
    create-release.yml
    deploy-platform.yml
docs/
  release-control.md
release/
  services.json
k8s/
  charts/platform-services/
  environments/
    dev/platform-services.values.yaml
    prod/platform-services.values.yaml
scripts/
  deploy-platform-services.sh
```

Purpose of each area:

- `release/services.json`: single source of truth for releasable services, Docker build inputs, and Helm service keys
- `k8s/environments/<env>/platform-services.values.yaml`: exact deployable runtime references for each environment
- `.github/workflows/build-sha-images.yml`: produces immutable commit-tagged images
- `.github/workflows/create-release.yml`: adds `vX.Y.Z` tags to existing digests and creates a draft GitHub Release
- `.github/workflows/deploy-platform.yml`: performs manual deployments and records deployment metadata in GitHub Actions history

## Environment Values Contract

Each environment values file is expected to store:

- `release.version`
- `release.gitTag`
- `release.commitSha`
- `services.<service>.image.repository`
- `services.<service>.image.tag`
- `services.<service>.image.digest`

Rules:

- `digest` is the deployable source of truth
- `tag` is retained for operator readability and traceability
- `release.gitTag` is blank only for unreleased environment deployments
- production releases should always populate all three release fields

## Manual Release Flow

1. Merge approved work into `dev`.
2. Validate and test on `dev`.
3. Open a promotion PR from `dev` to `main`.
4. Review and merge the promotion PR.
5. Run `.github/workflows/create-release.yml` from `main` with the chosen `vX.Y.Z`.
6. The workflow:
   - validates semver
   - finds the already-built `sha-<commit>` images
   - applies the `vX.Y.Z` tag to the same digests
   - creates a draft GitHub Release with autogenerated notes
   - attaches a `release-manifest.json` asset for traceability
7. Manually edit the draft release notes before publishing the GitHub Release.
8. Update the target environment values file with the release metadata and digests.
9. Run `.github/workflows/deploy-platform.yml` for the target environment.

Why this model:

- it preserves the build-once rule
- it keeps release numbering manual
- it makes the Git tag the official release identifier
- it keeps deployable digests visible in normal Git history

## Deployment And Promotion Flow

Environment promotion is a values-file change, not a rebuild.

Example:

- `dev` may deploy unreleased commit builds tagged `sha-<commit>`
- `prod` deploys the same digests after the release workflow adds `vX.Y.Z`

Promotion between environments is therefore:

1. copy the exact digests into the next environment values file
2. update `release.version`, `release.gitTag`, and `release.commitSha`
3. deploy with the manual workflow

## Rollback Flow

Rollback stays manual on purpose, but should be fast:

1. Identify the previous successful deployment from GitHub Actions history.
2. Re-run `.github/workflows/deploy-platform.yml` with:
   - the same environment
   - `git_ref` pointing to the older commit, tag, or values revision that still contains the previous digests
3. After the rollback succeeds, open a PR that restores the environment values file on the owning branch so Git stays aligned with the actual cluster state.

This works because the values files store exact digests, not floating tags.

## Deployment History Requirements

Each manual deployment run should capture at least:

- environment
- version
- git tag
- image digest
- commit SHA
- actor
- timestamp

In this repository, that data is recorded in:

- the GitHub Actions run metadata
- the workflow summary
- the uploaded `deployment-record.json` artifact

## Release Notes

Release notes are generated automatically first and then edited manually before publishing.

Repository support for that consists of:

- `.github/release.yml` for note categories
- `.github/workflows/create-release.yml` for draft release creation

## Future `stage` Branch

To add `stage` later, keep the same model:

- create `stage`
- protect it like `dev` and `main`
- add `k8s/environments/stage/platform-services.values.yaml`
- add a `stage` GitHub Environment
- extend `.github/workflows/deploy-platform.yml` environment choices and branch mapping

No redesign of tags, digests, or release notes should be required.
