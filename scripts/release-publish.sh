#!/usr/bin/env bash
# Consumer contract for endatix/release-workflows (publish half):
# push the packages produced by release-prepare.sh.
#
# Channel split (RELEASE_CHANNEL, set by the shared workflows):
#   canary → GitHub Packages ONLY. nuget.org is public and immutable —
#            per-push canaries must never land there.
#   stable → GitHub Packages + nuget.org.
#
# Usage: scripts/release-publish.sh <version>
# Expects env vars (exported by the shared workflows):
#   NUGET_SOURCE       GitHub NuGet feed (nuget.pkg.github.com/endatix)
#   GH_PACKAGES_TOKEN  job token with packages:write
#   NUGET_API_KEY      short-lived nuget.org key minted per run via OIDC
#                      trusted publishing (NuGet/login) — stable only
set -euo pipefail

VERSION="${1:?usage: release-publish.sh <version>}"
CHANNEL="${RELEASE_CHANNEL:-stable}"
: "${NUGET_SOURCE:?NUGET_SOURCE env var is required}"
: "${GH_PACKAGES_TOKEN:?GH_PACKAGES_TOKEN env var is required}"

echo "──── Publishing ${VERSION} (${CHANNEL} channel) ────"

# Globs are pinned to the release version — never trust a wildcard not to
# pick up a stale artifact.
echo "──── Pushing NuGet packages to ${NUGET_SOURCE} ────"
dotnet nuget push "build/packages/nuget/*.${VERSION}.nupkg" \
  -k "$GH_PACKAGES_TOKEN" \
  -s "$NUGET_SOURCE" \
  --skip-duplicate

if [[ "$CHANNEL" = "stable" ]]; then
  : "${NUGET_API_KEY:?NUGET_API_KEY env var is required for stable releases (minted by NuGet/login — is nuget-login: true set?)}"
  echo "──── Pushing NuGet packages to nuget.org ────"
  dotnet nuget push "build/packages/nuget/*.${VERSION}.nupkg" \
    -k "$NUGET_API_KEY" \
    -s https://api.nuget.org/v3/index.json \
    --skip-duplicate
fi
