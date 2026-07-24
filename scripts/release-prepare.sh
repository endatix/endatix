#!/usr/bin/env bash
# Consumer contract for endatix/release-workflows (prepare half):
# build every NuGet package, the Endatix.WebHost container image AND the Helm
# chart, all stamped with the given version. Called by the shared pipeline for
# PR validation (version 0.0.0-ci), canary releases, and stable rebuilds.
# Builds only — nothing is pushed here (PR validation has no registry login).
#
# Usage: scripts/release-prepare.sh <version>
# Expects env vars (exported by the shared workflows):
#   DOCKER_IMAGE  container image name, from the caller's docker-image input
set -euo pipefail

VERSION="${1:?usage: release-prepare.sh <version>}"
: "${DOCKER_IMAGE:?DOCKER_IMAGE env var is required (is docker-image set on the caller workflow?)}"

# Clean so the publish step only ever sees artifacts stamped with THIS version.
rm -rf build/packages/nuget build/packages/helm

echo "──── Building at version ${VERSION} ────"
dotnet restore
dotnet build -c Release --no-restore -p:Version="${VERSION}"

echo "──── Packing NuGet packages at version ${VERSION} ────"
dotnet pack -c Release --no-build -p:Version="${VERSION}" -o build/packages/nuget

# Container image via the .NET SDK (/t:PublishContainer) — no Dockerfile.
# One single-arch image per architecture, each loaded into the local Docker
# daemon: a daemon cannot hold a manifest list, so release-publish.sh stitches
# the per-arch tags into the multi-arch manifest once they reach a registry.
# Each --arch is its own RID-specific compile, so --no-build does not apply.
for arch in "x64:amd64" "arm64:arm64"; do
  echo "──── Building container image for linux-${arch%%:*} at version ${VERSION} ────"
  dotnet publish src/Endatix.WebHost/Endatix.WebHost.csproj \
    -c Release --os linux --arch "${arch%%:*}" \
    -p:Version="${VERSION}" \
    /t:PublishContainer \
    -p:ContainerRepository="${DOCKER_IMAGE}" \
    -p:ContainerImageTag="${VERSION}-${arch##*:}"
done

# Helm chart, released in lockstep with the image. Stamping BOTH version and
# appVersion with the release version is what makes the chart self-consistent:
# the deployment template falls back to .Chart.Version for the image tag, so a
# chart at X.Y.Z always deploys the image built at X.Y.Z above. The 0.1.0 in
# Chart.yaml is only a placeholder for local `helm template` runs.
# lint first — on PRs this is the only thing that type-checks the templates.
echo "──── Packaging Helm chart at version ${VERSION} ────"
helm lint helm
helm package helm \
  --version "${VERSION}" \
  --app-version "${VERSION}" \
  --destination build/packages/helm
