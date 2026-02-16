@echo off
setlocal

echo Welcome to Endatix Platform! This script will help you quickly set up the platform using Docker. For additional details and troubleshooting, please check README.md.
echo.

REM Check if Pester is loaded in PowerShell and remove it if present
powershell -Command "if (Get-Module -Name Pester -ErrorAction SilentlyContinue) { Remove-Module -Name Pester -Force; Write-Host 'Pester has been disabled because this script cannot run with it enabled.' }"

REM Check if Docker is installed
docker --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo Docker is not installed. Please install Docker before proceeding.
    pause
    exit /b 1
)

REM Check if docker-compose.yaml exists in the current directory
IF NOT EXIST "docker-compose.yaml" (
    echo docker-compose.yaml not found in the current directory. Please ensure it is present.
    pause
    exit /b 1
)

echo Docker and Docker Compose are installed, and docker-compose.yaml is found.

REM Create or overwrite the .env file
echo Creating .env file and setting the necessary environment variables...

REM Set default values for all variables
set "ENDATIX_API_PORT=8080"
set "ENDATIX_DB_PASSWORD=DbPa$$w0rD"
set "ENDATIX_API_IMAGE=endatix/endatix-api:latest"
set "ASPNETCORE_ENVIRONMENT=Development"
set "ENDATIX_HUB_IMAGE=endatix/endatix-hub:latest"

REM Enable delayed expansion for data collection
setlocal enabledelayedexpansion

REM Prompt for environment variables with default values if nothing is entered
set /p USER_EMAIL="Enter USER_EMAIL - Initial admin user email (skip to use the default value 'admin@endatix.com'): "
IF "%USER_EMAIL%"=="" set "USER_EMAIL=admin@endatix.com"

set /p USER_PASSWORD="Enter USER_PASSWORD - Initial admin user password, minimum 8 characters, must include lower and upper case letters, digit and special character (skip to use the default value 'P@ssw0rd'): "
IF "%USER_PASSWORD%"=="" set "USER_PASSWORD=P@ssw0rd"

set /p SEED_SAMPLE_FORMS_INPUT="Seed sample forms and submissions into the database? Enter 'y' or 'n' (skip to use the default value 'y'): "
IF "!SEED_SAMPLE_FORMS_INPUT!"=="" set "SEED_SAMPLE_FORMS_INPUT=y"
IF /I "!SEED_SAMPLE_FORMS_INPUT!"=="y" (set "SEED_SAMPLE_FORMS=true") ELSE (set "SEED_SAMPLE_FORMS=false")

REM Create the .env file with the exact structure and values
(
    echo # Default container port for endatix-api
    echo ENDATIX_API_PORT=%ENDATIX_API_PORT%
    echo.
    echo # Parameter endatix-db-password
    echo ENDATIX_DB_PASSWORD=%ENDATIX_DB_PASSWORD%
    echo.
    echo # Container image name for endatix-api
    echo ENDATIX_API_IMAGE=%ENDATIX_API_IMAGE%
    echo.
    echo # Parameter aspnetcore-environment
    echo ASPNETCORE_ENVIRONMENT=%ASPNETCORE_ENVIRONMENT%
    echo.
    echo # Parameter user-email
    echo USER_EMAIL=!USER_EMAIL!
    echo.
    echo # Parameter user-password
    echo USER_PASSWORD=!USER_PASSWORD!
    echo.
    echo # Seed sample forms and submissions into the database
    echo SEED_SAMPLE_FORMS=!SEED_SAMPLE_FORMS!
    echo.
    echo # Container image name for endatix-hub
    echo ENDATIX_HUB_IMAGE=%ENDATIX_HUB_IMAGE%
    echo.
) > .env

echo .env file created successfully with the provided values.

REM End delayed expansion
endlocal

REM Run docker compose with the .env file and docker-compose.yaml
echo Starting Docker Compose...
docker compose -f docker-compose.yaml up -d

IF %ERRORLEVEL% NEQ 0 (
    echo Failed to start Docker Compose. Please check the logs for more details.
    pause
    exit /b 1
)

echo Docker Compose started successfully!
echo ---------------------------------------------------
echo Access the Endatix containers at:
echo             http://localhost:%ENDATIX_API_PORT% for Endatix API
echo             http://localhost:3000 for Endatix Hub
echo.
echo The initial setup of Endatix Platform is completed.
echo ---------------------------------------------------
echo To stop the containers use `docker compose -f docker-compose.yaml stop`.
echo To start again the containers use `docker compose -f docker-compose.yaml start`.
echo To delete the containers use `docker compose -f docker-compose.yaml down`.
echo.