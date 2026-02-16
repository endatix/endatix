#!/bin/bash

echo "Welcome to Endatix Platform! This script will help you quickly set up the platform using Docker. For additional details and troubleshooting, please check README.md."
echo ""

# Ensure the script is executable by running: chmod +x setup.sh

# Check if Docker is installed
if ! [ -x "$(command -v docker)" ]; then
  echo "Docker is not installed. Please install Docker before proceeding."
  exit 1
fi

# Check if docker-compose.yaml exists in the current directory
if [ ! -f "docker-compose.yaml" ]; then
  echo "docker-compose.yaml not found in the current directory. Please ensure it is present."
  exit 1
fi

echo "Docker and Docker Compose are installed, and docker-compose.yaml is found."

# Create or overwrite the .env file
echo "Creating .env file and setting the necessary environment variables..."

# Set default values for all variables
ENDATIX_API_PORT="8080"
# ENDATIX_DB_PASSWORD="DbPa\$\$w0rD"
ENDATIX_DB_PASSWORD="MyDbPass1234"
ENDATIX_API_IMAGE="endatix/endatix-api:latest"
ASPNETCORE_ENVIRONMENT="Development"
ENDATIX_HUB_IMAGE="endatix/endatix-hub:latest"

# Prompt for environment variables with default values if nothing is entered
read -p "Enter USER_EMAIL - Initial admin user email (skip to use the default value 'admin@endatix.com'): " USER_EMAIL
USER_EMAIL=${USER_EMAIL:-admin@endatix.com}

read -p "Enter USER_PASSWORD - Initial admin user password, minimum 8 characters, must include lower and upper case letters, digit and special character (skip to use the default value 'P@ssw0rd'): " USER_PASSWORD
USER_PASSWORD=${USER_PASSWORD:-P@ssw0rd}

read -p "Seed sample forms and submissions into the database? Enter 'y' or 'n' (skip to use the default value 'y'): " SEED_SAMPLE_FORMS_INPUT
SEED_SAMPLE_FORMS_INPUT=${SEED_SAMPLE_FORMS_INPUT:-y}
if [[ "${SEED_SAMPLE_FORMS_INPUT,,}" == "y" ]]; then
  SEED_SAMPLE_FORMS="true"
else
  SEED_SAMPLE_FORMS="false"
fi

# Create the .env file with the exact structure and values
cat > .env << EOF
# Default container port for endatix-api
ENDATIX_API_PORT=$ENDATIX_API_PORT

# Parameter endatix-db-password
ENDATIX_DB_PASSWORD=$ENDATIX_DB_PASSWORD

# Container image name for endatix-api
ENDATIX_API_IMAGE=$ENDATIX_API_IMAGE

# Parameter aspnetcore-environment
ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT

# Parameter user-email
USER_EMAIL=$USER_EMAIL

# Parameter user-password
USER_PASSWORD=$USER_PASSWORD

# Seed sample forms and submissions into the database
SEED_SAMPLE_FORMS=$SEED_SAMPLE_FORMS

# Container image name for endatix-hub
ENDATIX_HUB_IMAGE=$ENDATIX_HUB_IMAGE

EOF

echo ".env file created successfully with the provided values."

# Run docker compose with the .env file and docker-compose.yaml
echo "Starting Docker Compose..."
docker compose -f docker-compose.yaml up -d

if [ $? -ne 0 ]; then
  echo "Failed to start Docker Compose. Please check the logs for more details."
  exit 1
fi

echo "Docker Compose started successfully! 🚀"
echo "---------------------------------------------------"
echo "Access the Endatix containers at:"
echo "          🔗 http://localhost:$ENDATIX_API_PORT for Endatix API"
echo "          🔗 http://localhost:3000 for Endatix Hub"
echo ""
echo "✅ The initial setup of Endatix Platform is completed."
echo "---------------------------------------------------"
echo "To stop the containers use \`docker compose -f docker-compose.yaml stop\`."
echo "To start again the containers use \`docker compose -f docker-compose.yaml start\`."
echo "To delete the containers use \`docker compose -f docker-compose.yaml down\`."
echo ""