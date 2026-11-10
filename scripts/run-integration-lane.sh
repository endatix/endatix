#!/usr/bin/env bash
# CI step (ci.yml, build-and-test): runs one integration lane from its own copy of the built test
# app, made by the "Copy the integration test app" step. coverlet.MTP instruments the DLLs next to
# the app in place, so lanes running at the same time can't share one bin folder.
#
# Usage: scripts/run-integration-lane.sh <lane> <provider> <filter> <minimum expected tests>
#        <lane> names the app copy ($RUNNER_TEMP/lanes/<lane>) and the results folder (.coverage/<lane>).
set -euo pipefail

lane="$1" provider="$2" filter="$3" min_tests="$4"
start=$(date +%s)
status=0
ENDATIX_TEST_DB_PROVIDER="$provider" dotnet exec "$RUNNER_TEMP/lanes/$lane/Endatix.IntegrationTests.dll" \
  --filter "$filter" \
  --minimum-expected-tests "$min_tests" \
  --report-xunit-ctrf \
  --timeout 30m \
  --coverlet \
  --results-directory "$GITHUB_WORKSPACE/.coverage/$lane" || status=$?
echo "::notice title=$lane::$(( $(date +%s) - start )) s, exit $status (started $start)"
exit "$status"
