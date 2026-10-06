#!/bin/bash
# Creates docker/keycloak/.env with random credentials for the local Keycloak dev realm.

set -euo pipefail

ENV_FILE="$(dirname "$0")/.env"

# The realm keeps the values from its first import, so replacing them would leave
# Keycloak and this file out of sync.
if [ -f "$ENV_FILE" ]; then
  echo "$ENV_FILE already exists. Delete it and run 'docker compose -f docker/docker-compose.keycloak.yml down -v' to start over."
  exit 1
fi

if ! [ -x "$(command -v openssl)" ]; then
  echo "openssl is not installed. Copy .env.example to .env and set the values by hand."
  exit 1
fi

cat > "$ENV_FILE" << EOF
KC_BOOTSTRAP_ADMIN_PASSWORD=$(openssl rand -hex 16)
KC_DEV_USER_PASSWORD=$(openssl rand -hex 12)
KC_HUB_CLIENT_SECRET=$(openssl rand -hex 32)
EOF

echo "Created $ENV_FILE:"
cat "$ENV_FILE"
echo ""
echo "Use KC_HUB_CLIENT_SECRET as the API Keycloak ClientSecret and the Hub AUTH_KEYCLOAK_CLIENT_SECRET."
