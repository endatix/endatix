#!/usr/bin/env bash
# Consumer contract for endatix/release-workflows (prepare half):
# build every NuGet package stamped with the given version.
# Called by the shared pipeline for PR validation (version 0.0.0-ci),
# canary releases, and stable rebuilds.
#
# Usage: scripts/release-prepare.sh <version>
set -euo pipefail

VERSION="${1:?usage: release-prepare.sh <version>}"

# Clean so the publish step only ever sees packages stamped with THIS version.
rm -rf build/packages/nuget

echo "──── Building at version ${VERSION} ────"
dotnet restore
dotnet build -c Release --no-restore -p:Version="${VERSION}"

echo "──── Packing NuGet packages at version ${VERSION} ────"
dotnet pack -c Release --no-build -p:Version="${VERSION}" -o build/packages/nuget
