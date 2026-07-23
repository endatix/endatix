#!/usr/bin/env bash
# Consumer contract for endatix/release-workflows (publish half):
# push the artifacts produced by release-prepare.sh.
#
# Channels (RELEASE_CHANNEL, set by the shared workflows):
#   canary       push to main             prerelease build
#   hotfix       push to hotfix/X.Y.x     prerelease build on a maintenance line
#   stable       promoted release, and the newest version overall
#   maintenance  promoted release on an older line (a newer stable exists)
#
# Prereleases stay on the internal feeds — nuget.org and Docker Hub are public
# and immutable, so per-push builds must never land there. BOTH promoted
# channels publish to the public registries; only `stable` may move floating
# pointers (docker `latest` — nuget.org sorts by semver on its own), so a hotfix
# for an older line ships without dragging `latest` backwards.
#
#   channel              GitHub Packages + GHCR   nuget.org + Docker Hub   latest
#   canary / hotfix               yes                      no               no
#   maintenance                   yes                      yes              no
#   stable                        yes                      yes              yes
#
# Usage: scripts/release-publish.sh <version>
# Expects env vars (exported by the shared workflows):
#   NUGET_SOURCE       GitHub NuGet feed (nuget.pkg.github.com/endatix)
#   GH_PACKAGES_TOKEN  job token with packages:write
#   DOCKER_IMAGE       GHCR image name, from the caller's docker-image input
#                      (the shared workflows do the ghcr.io login)
#   NUGET_API_KEY      short-lived nuget.org key minted per run via OIDC
#                      trusted publishing (NuGet/login) — promoted releases only
#   ENDATIX_DOCKERHUB_USERNAME / ENDATIX_DOCKERHUB_TOKEN
#                      Docker Hub credentials via Infisical (fetch-secrets:
#                      true on the caller) — promoted releases only
set -euo pipefail

VERSION="${1:?usage: release-publish.sh <version>}"
CHANNEL="${RELEASE_CHANNEL:-stable}"
: "${NUGET_SOURCE:?NUGET_SOURCE env var is required}"
: "${GH_PACKAGES_TOKEN:?GH_PACKAGES_TOKEN env var is required}"
: "${DOCKER_IMAGE:?DOCKER_IMAGE env var is required (is docker-image set on the caller workflow?)}"

# Public mirror for promoted releases. Not an input on the shared workflows
# (they model a single image) — mirroring is this repo's own publish concern.
DOCKERHUB_IMAGE="docker.io/endatix/endatix-api"

# Fail loud on an unrecognised channel: silently publishing nothing is a far
# worse outcome for a release than a red run.
case "$CHANNEL" in
  stable) PUBLISH_PUBLIC=true; MOVE_LATEST=true ;;
  maintenance) PUBLISH_PUBLIC=true; MOVE_LATEST=false ;;
  canary | hotfix) PUBLISH_PUBLIC=false; MOVE_LATEST=false ;;
  *) echo "::error::Unknown RELEASE_CHANNEL '${CHANNEL}' — release-workflows contract changed?" >&2; exit 1 ;;
esac

echo "──── Publishing ${VERSION} (${CHANNEL} channel) ────"

# Globs are pinned to the release version — never trust a wildcard not to
# pick up a stale artifact.
echo "──── Pushing NuGet packages to ${NUGET_SOURCE} ────"
dotnet nuget push "build/packages/nuget/*.${VERSION}.nupkg" \
  -k "$GH_PACKAGES_TOKEN" \
  -s "$NUGET_SOURCE" \
  --skip-duplicate

if [[ "$PUBLISH_PUBLIC" = true ]]; then
  : "${NUGET_API_KEY:?NUGET_API_KEY env var is required for promoted releases (minted by NuGet/login — is nuget-login: true set?)}"
  echo "──── Pushing NuGet packages to nuget.org ────"
  dotnet nuget push "build/packages/nuget/*.${VERSION}.nupkg" \
    -k "$NUGET_API_KEY" \
    -s https://api.nuget.org/v3/index.json \
    --skip-duplicate
fi

# The per-arch tags are what release-prepare.sh built and loaded into the local
# daemon; the plain version tag is a manifest list stitched from them once they
# are in the registry, so `docker pull` resolves the right platform.
echo "──── Pushing container image to ${DOCKER_IMAGE} ────"
docker push "${DOCKER_IMAGE}:${VERSION}-amd64"
docker push "${DOCKER_IMAGE}:${VERSION}-arm64"
docker buildx imagetools create \
  -t "${DOCKER_IMAGE}:${VERSION}" \
  "${DOCKER_IMAGE}:${VERSION}-amd64" \
  "${DOCKER_IMAGE}:${VERSION}-arm64"

if [[ "$MOVE_LATEST" = true ]]; then
  echo "──── Moving ${DOCKER_IMAGE}:latest to ${VERSION} ────"
  docker buildx imagetools create -t "${DOCKER_IMAGE}:latest" "${DOCKER_IMAGE}:${VERSION}"
fi

if [[ "$PUBLISH_PUBLIC" = true ]]; then
  : "${ENDATIX_DOCKERHUB_USERNAME:?ENDATIX_DOCKERHUB_USERNAME env var is required for promoted releases (Infisical prod — is fetch-secrets: true set?)}"
  : "${ENDATIX_DOCKERHUB_TOKEN:?ENDATIX_DOCKERHUB_TOKEN env var is required for promoted releases (Infisical prod — is fetch-secrets: true set?)}"

  # Mirror by copying the manifest — same digests as GHCR, no rebuild.
  DOCKERHUB_TAGS=(-t "${DOCKERHUB_IMAGE}:${VERSION}")
  if [[ "$MOVE_LATEST" = true ]]; then
    DOCKERHUB_TAGS+=(-t "${DOCKERHUB_IMAGE}:latest")
  fi

  echo "──── Mirroring container image to ${DOCKERHUB_IMAGE} ────"
  echo "$ENDATIX_DOCKERHUB_TOKEN" | docker login docker.io -u "$ENDATIX_DOCKERHUB_USERNAME" --password-stdin
  docker buildx imagetools create "${DOCKERHUB_TAGS[@]}" "${DOCKER_IMAGE}:${VERSION}"
fi
