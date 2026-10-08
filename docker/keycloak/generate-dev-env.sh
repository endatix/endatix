#!/bin/bash
# Creates docker/keycloak/.env with random credentials for the local Keycloak dev realm.

set -euo pipefail
# The file holds credentials: readable by the current user only.
umask 077

ENV_FILE="$(dirname "$0")/.env"

# The realm keeps the values from its first import, so replacing them would leave
# Keycloak and this file out of sync. Running the script again is a no-op.
if [[ -f "$ENV_FILE" ]]; then
  echo "$ENV_FILE already exists, keeping it. To start over, delete it, run this script, then 'docker compose -f docker/docker-compose.keycloak.yml down -v'."
  exit 0
fi

if ! command -v openssl > /dev/null; then
  echo "openssl is not installed. Copy .env.example to .env and set the values by hand."
  exit 1
fi

# Generated before writing because set -e ignores a failed command substitution
# inside a heredoc, which would leave an empty value in the file.
ADMIN_PASSWORD=$(openssl rand -hex 16)
USER_PASSWORD=$(openssl rand -hex 12)
CLIENT_SECRET=$(openssl rand -hex 32)

cat > "$ENV_FILE" << EOF
KC_BOOTSTRAP_ADMIN_PASSWORD=$ADMIN_PASSWORD
KC_DEV_USER_PASSWORD=$USER_PASSWORD
KC_HUB_CLIENT_SECRET=$CLIENT_SECRET
EOF

echo "Created $ENV_FILE:"
cat "$ENV_FILE"
echo ""
echo "Use KC_HUB_CLIENT_SECRET as the API Keycloak ClientSecret and the Hub AUTH_KEYCLOAK_CLIENT_SECRET."
