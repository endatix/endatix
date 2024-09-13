#!/bin/bash

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

# Create or overwrite the .env file
echo "Creating .env file..."
echo "# Environment variables for Endatix" > .env

# Collect environment variables from the user
read -p "Enter ASPNETCORE_ENVIRONMENT (e.g., Development): " ASPNETCORE_ENVIRONMENT
echo "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT" >> .env

read -p "Enter SECURITY_JWT_SIGNING_KEY (JWT signing key to be used in Endatix API): " SECURITY_JWT_SIGNING_KEY
echo "SECURITY_JWT_SIGNING_KEY=$SECURITY_JWT_SIGNING_KEY" >> .env

read -p "Enter SECURITY_DEV_USERS_0_EMAIL (Test user email for API auth): " SECURITY_DEV_USERS_0_EMAIL
echo "SECURITY_DEV_USERS_0_EMAIL=$SECURITY_DEV_USERS_0_EMAIL" >> .env

read -p "Enter SECURITY_DEV_USERS_0_PASSWORD (Test user password for API auth, min 8 chars): " SECURITY_DEV_USERS_0_PASSWORD
echo "SECURITY_DEV_USERS_0_PASSWORD=$SECURITY_DEV_USERS_0_PASSWORD" >> .env

read -p "Enter SENDGRID_API_KEY (SendGrid API key for sending emails, skip if not present): " SENDGRID_API_KEY
echo "SENDGRID_API_KEY=$SENDGRID_API_KEY" >> .env

read -p "Enter SQLSERVER_SA_PASSWORD (SQL Server container admin user password, min 8 chars, include uppercase, lowercase, digit and special char): " SQLSERVER_SA_PASSWORD
echo "SQLSERVER_SA_PASSWORD=$SQLSERVER_SA_PASSWORD" >> .env

echo ".env file created successfully with the provided values."

# Run docker compose with the .env file and docker-compose.dev.yml
echo "Starting Docker Compose..."
docker compose -f docker-compose.dev.yml up -d

if [ $? -ne 0 ]; then
  echo "Failed to start Docker Compose. Please check the logs for more details."
  exit 1
fi

echo "Docker Compose started successfully. Your containers should be up and running."
