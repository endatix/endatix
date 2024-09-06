# Setup Endatix Platform Using Docker Containers

Welcome to the Endatix Platform! This guide will help you quickly set up the platform using Docker, with a simple setup script that handles everything from environment configuration to container management.

## Prerequisites

Before proceeding, please ensure the following are installed on your machine:

- **Docker**
- **Docker Compose**

The easiest and recommended way to get both of them is to install [Docker Desktop](https://docs.docker.com/get-docker/).

---

## Step 1: Download the Necessary Files

To get the Endatix Platform running you need just two files:
- a setup script
- a docker-compose file

You have to choose one of the two provided setup scripts, depending on your operating system:

- **For Windows**: Use the `.bat` file
- **For Linux/macOS**: Use the `.sh` file

### Download Links

- **Windows**: [Download setup.bat](https://raw.githubusercontent.com/endatix/endatix/feature/issue-84-set-up-docker/docker/setup.bat)
- **Linux/macOS**: [Download setup.sh](https://raw.githubusercontent.com/endatix/endatix/feature/issue-84-set-up-docker/docker/setup.sh)
- **Docker Compose File**: [Download docker-compose.prod.yml](https://raw.githubusercontent.com/endatix/endatix/feature/issue-84-set-up-docker/docker/docker-compose.prod.yml)

### Command-line Download Options (Using `curl`)

You can also download the required files via the terminal:

#### For Windows Users:
```bash
curl -o setup.bat https://raw.githubusercontent.com/endatix/endatix/feature/issue-84-set-up-docker/docker/setup.bat
curl -o docker-compose.prod.yml https://raw.githubusercontent.com/endatix/endatix/feature/issue-84-set-up-docker/docker/docker-compose.prod.yml
```

#### For Linux/macOS Users:
```bash
curl -o setup.sh https://raw.githubusercontent.com/endatix/endatix/feature/issue-84-set-up-docker/docker/setup.sh
curl -o docker-compose.prod.yml https://raw.githubusercontent.com/endatix/endatix/feature/issue-84-set-up-docker/docker/docker-compose.prod.yml
```

---

## Step 2: Running the Setup Script

Once you've downloaded the setup script and `docker-compose.prod.yml` file, follow these instructions based on your operating system:

### **For Windows Users**:
1. **Navigate** to the folder where you downloaded the files.
2. **Run the Setup Script**: 
   - Double-click `setup.bat` or run the following command in **Command Prompt** or **PowerShell**:
   ```bash
   setup.bat
   ```
3. **Follow the prompts** to enter the required environment variables when prompted (see Step 3 below).

### **For Linux/macOS Users**:
1. **Make the script executable** (if not already):
   ```bash
   chmod +x setup.sh
   ```
2. **Run the Setup Script**:
   ```bash
   ./setup.sh
   ```
3. **Follow the prompts** to enter the required environment variables when prompted (see Step 3 below).

---

## Step 3: Environment Variable Configuration

During the setup, the script will ask for the following environment variables. Here’s a quick overview of what each one means:

- **ASPNETCORE_ENVIRONMENT**: Set this to `Production` for the production environment.
- **SECURITY_JWT_SIGNING_KEY**: The secret key used to sign JWT tokens for authentication.
- **SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_EMAIL**: Email of a dev user for testing purposes.
- **SECURITY_JWT_SIGNING_KEY_DEV_USERS_0_PASSWORD**: Password for the dev user.
- **SENDGRID_API_KEY**: Your API key for sending emails through SendGrid.
- **SQLSERVER_SA_PASSWORD**: The SA (System Administrator) password for SQL Server.

You will be prompted one by one to provide these values. The script will automatically save them in a `.env` file, ensuring your environment is configured correctly.

---

## Step 4: Automatic Setup and Startup

Once you’ve entered the necessary environment variables:

1. The setup script will **automatically pull the necessary Docker images** from Docker Hub.
2. It will **create and configure the containers** for the Endatix Platform, using the settings you've provided.
3. The containers will then **start automatically**. You should see confirmation messages indicating that the platform is up and running.

---

## Step 5: Verify the Setup

To verify that everything is working correctly:

- For **Endatix Hub** (admin console), open your browser and navigate to http://localhost:3000
- For **Endatix API**, navigate to http://localhost:5001/swagger, use the authentication endpoint with the dev user credentials entered during the setup and explore the other API endpoints.

---

## Troubleshooting

If you encounter any issues during the setup:

- Make sure Docker and Docker Compose are properly installed and running.
- Ensure that you've correctly entered all environment variables.
- Check the output of the setup script for any error messages.

To stop the containers, run the following command:

```bash
docker-compose -f docker-compose.prod.yml down
```

---

## Conclusion

The Endatix Platform setup should now be complete! If you followed the steps above, the platform should be running and ready for use. If you encounter any issues or have questions, please contact us at [endatix.com](https://endatix.com/contact) or on [GitHub](https://github.com/endatix/endatix/discussions).

Happy deploying and exploring!
