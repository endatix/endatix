@echo off
setlocal

REM Check if Docker is installed
docker --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo Docker is not installed. Please install Docker before proceeding.
    pause
    exit /b 1
)

REM Check if Docker Compose is installed
docker-compose --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo Docker Compose is not installed. Please install Docker Compose before proceeding.
    pause
    exit /b 1
)

REM Check if docker-compose.prod.yml exists in the current directory
IF NOT EXIST "docker-compose.prod.yml" (
    echo docker-compose.prod.yml not found in the current directory. Please ensure it is present.
    pause
    exit /b 1
)

echo Docker and Docker Compose are installed, and docker-compose.prod.yml is found.

REM Create or overwrite the .env file
echo Creating .env file...
echo # Environment variables for Endatix > .env

REM Enable delayed expansion for data collection
setlocal enabledelayedexpansion

REM Prompt for environment variables and store them in variables
set /p ASPNETCORE_ENVIRONMENT="Enter ASPNETCORE_ENVIRONMENT (e.g., Production): "
set /p SECURITY_JWT_SIGNING_KEY="Enter SECURITY_JWT_SIGNING_KEY (JWT Signing Key): "
set /p SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL="Enter SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL (Dev User Email): "
set /p SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD="Enter SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD (Dev User Password): "
set /p SENDGRID_API_KEY="Enter SENDGRID_API_KEY (SendGrid API Key): "
set /p SQLSERVER_SA_PASSWORD="Enter SQLSERVER_SA_PASSWORD (SQL Server SA Password): "

REM Write variables to the .env file using delayed expansion for special characters
(
    echo ASPNETCORE_ENVIRONMENT=!ASPNETCORE_ENVIRONMENT!
    echo SECURITY_JWT_SIGNING_KEY=!SECURITY_JWT_SIGNING_KEY!
    echo SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL=!SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL!
    echo SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD=!SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD!
    echo SENDGRID_API_KEY=!SENDGRID_API_KEY!
    echo SQLSERVER_SA_PASSWORD=!SQLSERVER_SA_PASSWORD!
) >> .env

REM End delayed expansion
endlocal

echo .env file created successfully with the provided values.

REM Run docker-compose with the .env file and docker-compose.prod.yml
echo Starting Docker Compose...
docker-compose -f docker-compose.prod.yml up -d

IF %ERRORLEVEL% NEQ 0 (
    echo Failed to start Docker Compose. Please check the logs for more details.
    pause
    exit /b 1
)

echo Docker Compose started successfully. Your containers should be up and running.
pause
