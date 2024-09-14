# Setup Endatix Platform Using Docker Containers

Welcome to the Endatix Platform! This guide will help you quickly set up the platform using Docker, with a simple setup script that handles everything from environment configuration to container management.

## Prerequisites

Before proceeding, please ensure the following are installed on your machine:

- **Docker**
- **Docker Compose**

The easiest and recommended way to get both of them is to install [Docker Desktop](https://docs.docker.com/get-docker/).

The scripts run successfully with `Docker version 26.1.1` and `Docker Compose version v2.27.0`. In case of compatibility issues, try these or later versions.

---

## Step 1: Download the Necessary Files

To get the Endatix Platform running you need just two files:
- a setup script
- a docker-compose file

**Note:** If you have already cloned [Endatix repository](https://github.com/endatix/endatix), the necessary files are  present in the folder `/docker`.

You have to choose one of the two provided setup scripts, depending on your operating system:

- **For Windows**: Use the `.bat` file
- **For Linux/macOS**: Use the `.sh` file

### Download Links

- **Windows**: [Download setup.bat](https://raw.githubusercontent.com/endatix/endatix/main/docker/setup.bat)
- **Linux/macOS**: [Download setup.sh](https://raw.githubusercontent.com/endatix/endatix/main/docker/setup.sh)
- **Docker Compose File**: [Download docker-compose.dev.yml](https://raw.githubusercontent.com/endatix/endatix/main/docker/docker-compose.dev.yml)

### Command-line Download Options (Using `curl`)

You can also download the required files via the terminal:

#### For Windows Users:
```bash
curl -o setup.bat https://raw.githubusercontent.com/endatix/endatix/main/docker/setup.bat
curl -o docker-compose.dev.yml https://raw.githubusercontent.com/endatix/endatix/main/docker/docker-compose.dev.yml
```

#### For Linux/macOS Users:
```bash
curl -o setup.sh https://raw.githubusercontent.com/endatix/endatix/main/docker/setup.sh
curl -o docker-compose.dev.yml https://raw.githubusercontent.com/endatix/endatix/main/docker/docker-compose.dev.yml
```

---

## Step 2: Running the Setup Script

**Note:** Ensure that the following local ports are not occupied:
- 1443 - needed for SQL Server, ensure there are no instances running locally on this port
- 5001 - to run Endatix API
- 3000 - to run Endatix Hub

Once you have the setup script and `docker-compose.dev.yml` file, follow these instructions based on your operating system:

#### For Windows Users:
1. **Navigate** to the folder where you downloaded the files.
2. **Run the Setup Script**: 
   - Double-click `setup.bat` or run the following command in **Command Prompt** or **PowerShell**:
   ```bash
   setup.bat
   ```
3. **Follow the prompts** to enter the required environment variables when prompted (see details below).

#### For Linux/macOS Users:
1. **Make the script executable** (if not already):
   ```bash
   chmod +x setup.sh
   ```
2. **Run the Setup Script**:
   ```bash
   ./setup.sh
   ```
3. **Follow the prompts** to enter the required environment variables when prompted (see details below).

### Environment Variables Configuration Details

During the setup, the script will ask for the following environment variables. Most of them have default values that can be used if no values is entered. Here’s a quick overview of what each one means:

- **ASPNETCORE_ENVIRONMENT**: Do not enter anything to use the default value `Development`. The containers are not ready for production environment, because they lack https connection, prod level security for the configuration and others.
- **SECURITY_JWT_SIGNING_KEY**: The secret key to be used in Endatix API to sign JWT tokens for authentication. The default value is `L2yGC_Vpd3k#L[<9Zb,h?.HT:n'T/5CTDmBpDskU?NAaT$sLfRU`.
- **SECURITY_DEV_USERS_0_EMAIL**: Email of a user to be set in Endatix API for testing purposes. A new user will be created with this email. The default value is `developer@endatix.com`.
- **SECURITY_DEV_USERS_0_PASSWORD**: Password of a user to be set in Endatix API for testing purposes. It must be minimum 8 characters. A new user will be created with this password. The default value is `password`.
- **SENDGRID_API_KEY**: The API key to be used for sending emails through SendGrid. Skip for not sending emails.
- **SQLSERVER_SA_PASSWORD**: The SA (System Administrator) password to be set for the SQL Server running in a container. A new user will be created with this password. The default value is `DbPa649w0rD`. Check [SQL Server Password Policy](https://learn.microsoft.com/en-us/sql/relational-databases/security/password-policy) to ensure the password you enter is according to the policy.

You will be prompted one by one to provide these values. The script will automatically save them in a `.env` file, ensuring your environment is configured correctly.

### Getting the Images and Creating the Containers

Once you’ve entered the necessary environment variables:

1. The setup script will **automatically pull the necessary Docker images** from Docker Hub.
2. It will **create and configure the containers** for the Endatix Platform, using the settings you've provided.
3. The containers will then **start automatically**. You should see confirmation messages indicating that the platform is up and running.

---

## Step 3: Verify the Setup

To verify that everything is working correctly:

- For **Endatix Hub** (admin console), open your browser and navigate to http://localhost:3000
- For **Endatix API**, navigate to http://localhost:5001/swagger, use the authentication endpoint with the user credentials entered during the setup and explore the other API endpoints, e.g. `GET /api/forms`.

---

## Troubleshooting

If you encounter any issues during the setup:

- Make sure Docker and Docker Compose are properly installed and running.
- Ensure that you've correctly entered all environment variables.
- Check the output of the setup script for any error messages.

To stop the containers, run the following command:

```bash
docker compose -f docker-compose.dev.yml down
```

---

## Further Usage

After the initial setup is completed, the commands for some useful actions are:
- `docker compose -f docker-compose.dev.yml stop` to stop the containers
- `docker compose -f docker-compose.dev.yml start` to start again the containers
- `docker compose -f docker-compose.dev.yml down` to delete the containers

---

## Conclusion

The Endatix Platform setup should now be complete! If you followed the steps above, the platform should be running and ready for use. If you encounter any issues or have questions, please contact us at [endatix.com](https://endatix.com/contact) or on [GitHub](https://github.com/endatix/endatix/discussions).

Happy deploying and exploring!
