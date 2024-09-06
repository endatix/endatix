#!/bin/bash

# Ensure the script is executable by running: chmod +x setup.sh

# Check if Docker is installed
if ! [ -x "$(command -v docker)" ]; then
  echo "Docker is not installed. Please install Docker before proceeding."
  exit 1
fi

# Check if Docker Compose is installed
if ! [ -x "$(command -v docker-compose)" ]; then
  echo "Docker Compose is not installed. Please install Docker Compose before proceeding."
  exit 1
fi

# Check if docker-compose.prod.yml exists in the current directory
if [ ! -f "docker-compose.prod.yml" ]; then
  echo "docker-compose.prod.yml not found in the current directory. Please ensure it is present."
  exit 1
fi

echo "Docker and Docker Compose are installed, and docker-compose.prod.yml is found."

# Create or overwrite the .env file
echo "Creating .env file..."
echo "# Environment variables for Endatix" > .env

# Collect environment variables from the user
read -p "Enter ASPNETCORE_ENVIRONMENT (e.g., Production): " ASPNETCORE_ENVIRONMENT
echo "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT" >> .env

read -p "Enter SECURITY_JWT_SIGNING_KEY (JWT Signing Key): " SECURITY_JWT_SIGNING_KEY
echo "SECURITY_JWT_SIGNING_KEY=$SECURITY_JWT_SIGNING_KEY" >> .env

read -p "Enter SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL (Dev User Email): " SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL
echo "SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL=$SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL" >> .env

read -p "Enter SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD (Dev User Password): " SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD
echo "SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD=$SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD" >> .env

read -p "Enter SENDGRID_API_KEY (SendGrid API Key): " SENDGRID_API_KEY
echo "SENDGRID_API_KEY=$SENDGRID_API_KEY" >> .env

read -p "Enter SQLSERVER_SA_PASSWORD (SQL Server SA Password): " SQLSERVER_SA_PASSWORD
echo "SQLSERVER_SA_PASSWORD=$SQLSERVER_SA_PASSWORD" >> .env

echo ".env file created successfully with the provided values."

# Run docker-compose with the .env file and docker-compose.prod.yml
echo "Starting Docker Compose..."
docker-compose -f docker-compose.prod.yml up -d

if [ $? -ne 0 ]; then
  echo "Failed to start Docker Compose. Please check the logs for more details."
  exit 1
fi

echo "Docker Compose started successfully. Your containers should be up and running."
