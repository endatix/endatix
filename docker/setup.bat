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

REM Check if docker-compose.dev.yml exists in the current directory
IF NOT EXIST "docker-compose.dev.yml" (
    echo docker-compose.dev.yml not found in the current directory. Please ensure it is present.
    pause
    exit /b 1
)

echo Docker and Docker Compose are installed, and docker-compose.dev.yml is found.

REM Create or overwrite the .env file
echo Creating .env file and setting the necessary environment variables...
echo # Environment variables for Endatix > .env

REM Enable delayed expansion for data collection
setlocal enabledelayedexpansion

REM Prompt for environment variables with default values if nothing is entered
set /p ASPNETCORE_ENVIRONMENT="Enter ASPNETCORE_ENVIRONMENT (skip to use the default value 'Development'): "
IF "%ASPNETCORE_ENVIRONMENT%"=="" set "ASPNETCORE_ENVIRONMENT=Development"

set /p SECURITY_JWT_SIGNING_KEY="Enter SECURITY_JWT_SIGNING_KEY (JWT signing key to be used in Endatix API, skip to use the default value): "
IF "%SECURITY_JWT_SIGNING_KEY%"=="" set "SECURITY_JWT_SIGNING_KEY=L2yGC_Vpd3k#L[<9Zb,h?.HT:n'T/5CTDmBpDskU?NAaT$sLfRU"

set /p SECURITY_DEV_USERS_0_EMAIL="Enter SECURITY_DEV_USERS_0_EMAIL (Test user email for API auth, skip to use the default value 'developer@endatix.com'): "
IF "%SECURITY_DEV_USERS_0_EMAIL%"=="" set "SECURITY_DEV_USERS_0_EMAIL=developer@endatix.com"

set /p SECURITY_DEV_USERS_0_PASSWORD="Enter SECURITY_DEV_USERS_0_PASSWORD (Test user password for API auth, skip to use the default value 'password'): "
IF "%SECURITY_DEV_USERS_0_PASSWORD%"=="" set "SECURITY_DEV_USERS_0_PASSWORD=password"

set /p SENDGRID_API_KEY="Enter SENDGRID_API_KEY (SendGrid API key for sending emails, skip for not sending emails): "

set /p SQLSERVER_SA_PASSWORD="Enter SQLSERVER_SA_PASSWORD (SQL Server container admin user password, skip to use the default value 'DbPa$$w0rD'): "
IF "%SQLSERVER_SA_PASSWORD%"=="" set "SQLSERVER_SA_PASSWORD=DbPa$$w0rD"

REM Write variables to the .env file using delayed expansion for special characters
(
    echo ASPNETCORE_ENVIRONMENT=!ASPNETCORE_ENVIRONMENT!
    echo SECURITY_JWT_SIGNING_KEY=!SECURITY_JWT_SIGNING_KEY!
    echo SECURITY_DEV_USERS_0_EMAIL=!SECURITY_DEV_USERS_0_EMAIL!
    echo SECURITY_DEV_USERS_0_PASSWORD=!SECURITY_DEV_USERS_0_PASSWORD!
    echo SENDGRID_API_KEY=!SENDGRID_API_KEY!
    echo SQLSERVER_SA_PASSWORD=!SQLSERVER_SA_PASSWORD!
) >> .env

REM End delayed expansion
endlocal

echo .env file created successfully with the provided values.

REM Run docker compose with the .env file and docker-compose.dev.yml
echo Starting Docker Compose...
docker compose -f docker-compose.dev.yml up -d

IF %ERRORLEVEL% NEQ 0 (
    echo Failed to start Docker Compose. Please check the logs for more details.
    pause
    exit /b 1
)

echo Docker Compose started successfully. Your containers should be up and running.
