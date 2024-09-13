#!/bin/bash

echo "Welcome to Endatix Platform! This script will help you quickly set up the platform using Docker. For additional details and troubleshooting, please check README.md."
echo ""

# Ensure the script is executable by running: chmod +x setup.sh

# Check if Docker is installed
if ! [ -x "$(command -v docker)" ]; then
  echo "Docker is not installed. Please install Docker before proceeding."
  exit 1
fi

# Check if docker-compose.dev.yml exists in the current directory
if [ ! -f "docker-compose.dev.yml" ]; then
  echo "docker-compose.dev.yml not found in the current directory. Please ensure it is present."
  exit 1
fi

echo "Docker and Docker Compose are installed, and docker-compose.dev.yml is found."

# Function to escape single quotes in variables
escape_single_quotes() {
  local input="$1"
  echo "$input" | sed "s/'/\\\'/g"
}

# Create or overwrite the .env file
echo "Creating .env file and setting the necessary environment variables..."
echo "# Environment variables for Endatix" > .env

# Prompt for environment variables with default values if nothing is entered
read -p "Enter ASPNETCORE_ENVIRONMENT (skip to use the default value 'Development'): " ASPNETCORE_ENVIRONMENT
ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Development}
echo "ASPNETCORE_ENVIRONMENT='$(escape_single_quotes "$ASPNETCORE_ENVIRONMENT")'" >> .env

read -p "Enter SECURITY_JWT_SIGNING_KEY (JWT signing key to be used in Endatix API, skip to use the default value): " SECURITY_JWT_SIGNING_KEY
SECURITY_JWT_SIGNING_KEY=${SECURITY_JWT_SIGNING_KEY:-'L2yGC_Vpd3k#L[<9Zb,h?.HT:n'\''T/5CTDmBpDskU?NAaT$sLfRU'}
echo "SECURITY_JWT_SIGNING_KEY='$(escape_single_quotes "$SECURITY_JWT_SIGNING_KEY")'" >> .env

read -p "Enter SECURITY_DEV_USERS_0_EMAIL (Test user email for API auth, skip to use the default value 'developer@endatix.com'): " SECURITY_DEV_USERS_0_EMAIL
SECURITY_DEV_USERS_0_EMAIL=${SECURITY_DEV_USERS_0_EMAIL:-developer@endatix.com}
echo "SECURITY_DEV_USERS_0_EMAIL='$(escape_single_quotes "$SECURITY_DEV_USERS_0_EMAIL")'" >> .env

read -p "Enter SECURITY_DEV_USERS_0_PASSWORD (Test user password for API auth, skip to use the default value 'password'): " SECURITY_DEV_USERS_0_PASSWORD
SECURITY_DEV_USERS_0_PASSWORD=${SECURITY_DEV_USERS_0_PASSWORD:-password}
echo "SECURITY_DEV_USERS_0_PASSWORD='$(escape_single_quotes "$SECURITY_DEV_USERS_0_PASSWORD")'" >> .env

read -p "Enter SENDGRID_API_KEY (SendGrid API key for sending emails, skip for not sending emails): " SENDGRID_API_KEY
echo "SENDGRID_API_KEY='$(escape_single_quotes "$SENDGRID_API_KEY")'" >> .env

read -p "Enter SQLSERVER_SA_PASSWORD (SQL Server container admin user password, skip to use the default value 'DbPa\$\$w0rD'): " SQLSERVER_SA_PASSWORD
SQLSERVER_SA_PASSWORD=${SQLSERVER_SA_PASSWORD:-DbPa\$\$w0rD}
echo "SQLSERVER_SA_PASSWORD='$(escape_single_quotes "$SQLSERVER_SA_PASSWORD")'" >> .env

echo ".env file created successfully with the provided values."

# Run docker compose with the .env file and docker-compose.dev.yml
echo "Starting Docker Compose..."
docker compose -f docker-compose.dev.yml up -d

if [ $? -ne 0 ]; then
  echo "Failed to start Docker Compose. Please check the logs for more details."
  exit 1
fi

echo "Docker Compose started successfully! 🚀"
echo "---------------------------------------------------"
echo "Access the Endatix containers at:"
echo "          🔗 http://localhost:5001/swagger for Endatix API"
echo "          🔗 http://localhost:3000 for Endatix Hub"
echo ""
echo "✅ The initial setup of Endatix Platform is completed."
echo "---------------------------------------------------"
echo "To stop the containers use \`docker compose -f docker-compose.dev.yml stop\`."
echo "To start again the containers use \`docker compose -f docker-compose.dev.yml start\`."
echo "To delete the containers use \`docker compose -f docker-compose.dev.yml down\`."
echo ""
