# Endatix Azure Deployment

This directory contains Bicep templates for deploying Endatix to Azure.

## Overview

The `endatix-azure.template.bicep` template deploys a complete Endatix environment with the following Azure resources:

- **Application Insights** - Monitoring and diagnostics
- **Static Web App** - Endatix Hub frontend (Next.js)
- **App Service** - Endatix API backend
- **Blob Storage** - File storage for forms and submissions
- **PostgreSQL Flexible Server** - Primary database

## Prerequisites

Before deploying, ensure you have:

1. **Azure CLI** installed and authenticated

   ```bash
   az login
   ```

2. **PostgreSQL Admin Credentials** ready (username and password to set in parameters.json)

3. **Deployment Script** (automatic validation)
   - The deployment script (`deploy.sh` or `deploy.ps1`) validates all prerequisites
   - It will create the resource group if it doesn't exist
   - No manual CLI commands needed - just run the script!

## Template Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `resource_prefix` | string | `temp-` | Prefix for all resource names (e.g., 'prod-', 'uat-', 'ci-') |
| `location` | string | Resource Group location | Azure region for deploying resources |
| `environment` | string | `dev` | Environment name (dev, uat, prod) - used for tagging |
| `branch` | string | `main` | Git branch to deploy for the Hub static site |
| `hubRepositoryUrl` | string | `https://github.com/endatix/endatix-hub` | Hub (Frontend) GitHub repository URL |
| `apiRepositoryUrl` | string | `https://github.com/endatix/endatix-api` | API (Backend) GitHub repository URL |
| `apiDeploymentBranch` | string | `main` | Git branch to deploy for the API backend |
| `tags` | object | `{environment: environment}` | Azure resource tags for organization and billing |
| `postgres_admin_username` | string (secure) | *Required* | PostgreSQL administrator username |
| `postgres_admin_password` | string (secure) | *Required* | PostgreSQL administrator password |
| `hubAppSettings` | object | `{}` | App settings for the Hub (Static Web App) |
| `hubEnvironmentVariables` | object | `{}` | Environment variables for the Hub |
| `apiAppSettings` | object | `{ASPNETCORE_ENVIRONMENT: Production}` | App settings for the API (Web App) |
| `apiConnectionStrings` | object | `{}` | Connection strings for the API (auto-configured) |
| `postgresqlDatabaseName` | string | `endatix-db` | PostgreSQL database name |
| `postgresqlVersion` | string | `16` | PostgreSQL server version |
| `enablePostgresqlHA` | bool | `false` | Enable high availability for PostgreSQL |
| `storageIsPrivate` | bool | `false` | [OPTIONAL] Set to true if storage account should be private (no public blob access) |

## Deployment Instructions

### Quick Start (macOS/Linux/WSL)

1. **Configure deployment parameters:**
   ```bash
   # Edit parameters.json with your settings
   nano parameters.json
   ```

2. **Deploy using the deployment script:**
   ```bash
   chmod +x deploy.sh
   ./deploy.sh \
     --resource-group {YOUR_RESOURCE_GROUP} \
     --location eastus
   ```

   The script will:
   - ✅ Validate prerequisites (Azure CLI, authentication, files)
   - ✅ Verify resource group exists (create if needed)
   - ✅ Show deployment preview with key parameters
   - ✅ Ask for confirmation before deploying
   - ✅ Deploy Bicep template using `az deployment group create`
   - ✅ Display resource URLs and deployment outputs

### Windows PowerShell

```powershell
# Make script executable (PowerShell 7+)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Deploy
.\deploy.ps1 -ResourceGroup {YOUR_RESOURCE_GROUP} -Location eastus
```

### Using Parameters File

All deployment configuration should go in `parameters.json`. This ensures consistency and easy redeployment.

**Example `parameters.json`** (see `parameters.json` in this directory for complete reference):

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "resource_prefix": {
      "value": "prod-"
    },
    "environment": {
      "value": "production"
    },
    "hubRepositoryUrl": {
      "value": "https://github.com/YOUR_ORG/endatix-hub"
    },
    "apiRepositoryUrl": {
      "value": "https://github.com/YOUR_ORG/endatix"
    },
    "postgres_admin_username": {
      "value": "adminuser"
    },
    "postgres_admin_password": {
      "value": "YOUR_SECURE_PASSWORD"
    }
  }
}
```

**Why use a parameters file?**
- Single source of truth for all deployment values
- Easy to version control (excluding secrets)
- Effortless reproduction of environments
- No manual CLI arguments prone to error
- Deployment script automates the entire process

### Using Azure Key Vault for Secure Secrets

For production, reference secrets from Azure Key Vault instead of hardcoding them:

```json
{
  "postgres_admin_password": {
    "reference": {
      "keyVault": {
        "id": "/subscriptions/{SUB}/resourceGroups/{RG}/providers/Microsoft.KeyVault/vaults/{VAULT}"
      },
      "secretName": "postgres-admin-password"
    }
  }
}
```
```

## Database Setup

After deployment, you need to set up the PostgreSQL database:

1. Connect to the PostgreSQL server (retrieve connection details from Azure portal)
2. Run migration scripts to initialize the database schema
3. Seed initial data if required

The template creates:

- PostgreSQL Flexible Server (version 16)
- Database: `endatix-db` (UTF-8, en_US.utf8 collation)
- Firewall rule allowing Azure services
- SSL requirement enabled (`require_secure_transport` = ON)

## Managing App Settings & Secrets

### Update App Settings After Deployment

To update app settings without redeploying the entire template:

```bash
# Update Hub app settings
az staticwebapp appsettings set \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name {HUB_APP_NAME} \
  --setting-names \
    NEXT_PUBLIC_API_URL="https://new-api-url.com" \
    LOG_LEVEL="Debug"

# Update API app settings
az webapp config appsettings set \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name {API_APP_NAME} \
  --settings \
    CUSTOM_SETTING="new_value" \
    LOG_LEVEL="Information"
```

### Using Azure Key Vault for Secrets

For production environments, use Azure Key Vault to store and reference secrets:

1. Create a Key Vault:
```bash
az keyvault create \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name {KEYVAULT_NAME} \
  --location eastus
```

2. Store secrets:
```bash
az keyvault secret set \
  --vault-name {KEYVAULT_NAME} \
  --name postgres-admin-password \
  --value '{YOUR_SECURE_PASSWORD}'
```

3. Reference in parameters file (see Deployment Instructions section above)

## Application Code Deployment

After running `./deploy.sh`, infrastructure is ready and applications auto-deploy from GitHub.

### Automatic Deployment (Default)

Both Hub and API are configured for automatic GitHub integration:

- **Hub (Frontend)** - Static Web App
  - Every push to the specified branch (default: `main`) triggers automatic rebuild
  - GitHub Actions workflow generated automatically by Static Web App
  - Deployment logs: Azure Portal → Resource Group → Hub Static Web App → Deployment Center

- **API (Backend)** - App Service  
  - Every push to the specified branch (default: `main`) triggers automatic rebuild
  - Build logs: `https://{api-app-name}.scm.azurewebsites.net`
  - Deployment history: Azure Portal → App Service → Deployment Center

### On-Demand Code Deployment

To deploy code immediately without waiting for a git push:

#### Option 1: Git Push (Fastest)

Push to your repository's main branch:

```bash
git add .
git commit -m "Deploy updates"
git push origin main
```

Both Hub and API automatically redeploy within seconds.

#### Option 2: Manual Sync - API Only

Manually trigger deployment for the API (App Service):

```bash
az webapp deployment source sync \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name {API_APP_NAME}
```

**Note:** Static Web App (Hub) does not support manual sync; use git push instead.

#### Option 3: GitHub Actions Manual Dispatch

If your GitHub repository has a workflow configured with `workflow_dispatch`, trigger it manually:

**From Azure CLI:**
```bash
az workflow run list \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --repository endatix \
  --query "[0].id" -o tsv | xargs -I {} \
az workflow run create --resource-group {YOUR_RESOURCE_GROUP} --workflow-id {}
```

**From GitHub Web:**
1. Go to your repository → Actions
2. Select the deployment workflow
3. Click "Run workflow" → select branch → Run

#### Option 4: Deploy Locally Built Code - API Only

For testing locally built .NET code before committing:

```bash
# Build API
dotnet publish src/Endatix.WebHost/Endatix.WebHost.csproj \
  -c Release -o ./publish

# Create deployment package
cd publish && zip -r ../api.zip . && cd ..

# Deploy to Azure
az webapp deployment source config-zip \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name {API_APP_NAME} \
  --src api.zip

# Cleanup
rm -rf publish api.zip
```

### Recommended Deployment Workflow

**For Development/Testing:**
1. Make code changes locally
2. Commit and push to main (or feature branch)
3. Applications automatically deploy
4. Monitor deployment in Azure Portal

**For Production:**
1. Commit changes to repository
2. (Optional) Create GitHub Actions workflow for testing/approval
3. Merge to main branch
4. Automatic deployment triggers
5. Monitor in Azure Portal

### Checking Deployment Status

**Hub (Static Web App) Status:**
```bash
az staticwebapp deployment list \
  --name {HUB_APP_NAME} \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --query "[0:5].[id, status, buildId]" -o table
```

**API (App Service) Status:**
```bash
az webapp deployment list \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name {API_APP_NAME} \
  --query "[0:5].[id, status, author, message]" -o table
```

**View Deployment Logs:**
```bash
# API logs (via Kudu)
az webapp deployment source show \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name {API_APP_NAME} \
  --query "properties" -o json

# Or access Kudu directly
# https://{api-app-name}.scm.azurewebsites.net/api/logs/latest
```

---

## Deployment Script Usage

### Basic Usage

The deployment scripts (`deploy.sh` and `deploy.ps1`) handle all deployment details:

**macOS/Linux:**
```bash
./deploy.sh --resource-group rg-endatix-prod --location eastus
```

**Windows (PowerShell):**
```powershell
.\deploy.ps1 -ResourceGroup rg-endatix-prod -Location eastus
```

### Script Options

Both scripts support the following options:

- `--resource-group` (required): Azure resource group name
- `--location`: Azure region (auto-detected if RG exists)
- `--parameters-file`: Path to parameters.json (default: ./parameters.json)
- `--mode`: Deployment mode - `Complete` (default) or `Incremental`

### Script Features

- ✅ Validates all prerequisites (Azure CLI, authentication, files)
- ✅ Creates resource group if it doesn't exist
- ✅ Shows deployment preview with key parameters
- ✅ Asks for confirmation before deploying
- ✅ Runs `az deployment group create` with optimized settings
- ✅ Displays deployment duration and outputs
- ✅ Works consistently on Windows, macOS, and Linux

### Manual Deployment (Alternative)

If you prefer not to use the scripts, deploy directly with Azure CLI:

```bash
az deployment group create \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --template-file endatix-azure.template.bicep \
  --parameters parameters.json \
  --mode Complete
```

---

## Customizing Repository URLs

By default, the template deploys from the official public repositories:
- **Hub**: `https://github.com/endatix/endatix-hub`
- **API**: `https://github.com/endatix/endatix`

To deploy from your own forks instead, update the `parameters.json`:

```json
{
  "parameters": {
    "hubRepositoryUrl": {
      "value": "https://github.com/YOUR_ORG/endatix-hub"
    },
    "apiRepositoryUrl": {
      "value": "https://github.com/YOUR_ORG/endatix"
    }
  }
}
```

Every push to the configured branch (`main` by default) automatically triggers rebuilds and deployments.

---

## Outputs

After successful deployment, retrieve outputs with:

```bash
az deployment group show \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name endatix-azure.template
```

Key resources created:

- Static Web App hosting Endatix Hub
- App Service hosting Endatix API
- Blob Storage for file uploads
- PostgreSQL Flexible Server database

## Module Dependencies

This template uses the following modules (ensure they exist in the `modules/` directory):

- `modules/app-insights.module.bicep` - Application Insights and Log Analytics workspace
- `modules/static-site.module.bicep` - Static Web App for Hub frontend with git-based deployment
- `modules/endatix-api.module.bicep` - App Service for API backend with git-based deployment
- `modules/storage.module.bicep` - Blob Storage for file uploads
- `modules/postgres.module.bicep` - PostgreSQL Flexible Server with database and security configs

## Architecture & Deployment Strategy

### Resolving Circular Dependencies

This template avoids circular resource dependencies through careful module ordering and configuration:

1. **Storage Module** - Deployed first with CORS rules configured for calculated Hub and API hostnames
2. **Hub Module** - Receives storage credentials via environment variables
3. **API Module** - Doesn't need storage configuration; storage connection string provided via connection strings

This approach provides:
- **Clean dependency graph** - No circular references
- **CORS Pre-Configuration** - Storage CORS rules configured at deployment time using predictable hostname patterns:
  - Hub: `{staticSiteName}.azurestaticapps.net`
  - API: `{appName}.azurewebsites.net`
- **Environment-based configuration** - Hub app gets `AZURE_STORAGE_*` environment variables
- **Private vs. Public storage** - Easily switch between public and private storage without redeployment

## Bicep Best Practices Applied

This template follows Bicep best practices:

1. **Modular Design** - Infrastructure divided into reusable modules for maintainability
2. **Parameter Metadata** - All parameters include descriptions and validation constraints
3. **Secure Parameters** - Sensitive values marked with `@secure()` decorator
4. **Symbolic References** - Uses symbolic references (e.g., module outputs) instead of hardcoded IDs
5. **Dependency Management** - Explicitly avoids circular dependencies through careful architectural patterns
6. **Consistent Naming** - Uses variable expressions for consistent resource naming across environments
7. **Flexible Configuration** - Supports environment-specific settings via parameters without template modifications
8. **Outputs** - Modules export relevant identifiers and connection details for post-deployment use
9. **Runtime Configuration** - Storage and application settings passed as environment variables for runtime flexibility

## Security Considerations

- PostgreSQL admin credentials are marked as `@secure()` and should never be committed to version control
- Use Azure Key Vault to store secrets and reference them in parameters
- Firewall rules allow public access - consider restricting based on your security requirements
- Enable advanced threat protection for production environments
- Regularly update and patch PostgreSQL versions
- For production deployments, use managed identities instead of connection string credentials
- Enable private endpoints for Static Web App and App Service to restrict public access

## Troubleshooting

### Deployment Script Issues

#### "Command not found: az"
- **Cause**: Azure CLI is not installed or not in PATH
- **Solution**: Install Azure CLI from https://docs.microsoft.com/cli/azure/install-azure-cli

#### "Not authenticated to Azure"
- **Cause**: Not logged into Azure
- **Solution**: Run `az login` and follow the authentication flow

#### "Resource group does not exist"
- **Cause**: Specified resource group hasn't been created
- **Solution**: Let the script create it by specifying `--location` (macOS/Linux) or `-Location` (PowerShell)

#### Script permission denied (macOS/Linux)
- **Cause**: Deploy script is not executable
- **Solution**: Run `chmod +x deploy.sh` before executing

### Deployment Failures

#### "Deployment Fails with 'Resource Already Exists'"
- **Cause**: Template uses Complete mode, which removes resources not defined
- **Solution**: Either delete conflicting resources or use `--mode Incremental` for updates

#### "MissingSubscriptionRegistration for Microsoft.AlertsManagement"
- **Cause**: Subscription hasn't registered the alerts provider
- **Solution**: Set `enableFailureAnomalyAlerts` to `false` in parameters.json (default)

#### "PostgreSQL Connection Issues"
- **Cause**: Firewall, SSL, or connection string issues
- **Solution**:
  - Verify firewall rules allow your IP
  - Check `require_secure_transport` configuration
  - Ensure connection string format matches your driver

#### "Static Site Not Accessible"
- **Cause**: GitHub repository not accessible or branch doesn't exist
- **Solution**:
  - Verify GitHub repository is public
  - Confirm branch name exists (default: `main`)
  - Check deployment logs in Azure Portal

#### "App Settings Not Applied"
- **Cause**: JSON syntax error or settings need restart to take effect
- **Solution**:
  - Validate JSON syntax in parameters.json
  - Restart web app: `az webapp restart --resource-group {RG} --name {AppName}`
  - Verify settings: `az webapp config appsettings list --resource-group {RG} --name {AppName}`

### Redeploying After Configuration Changes

To deploy again after updating `parameters.json`:

```bash
# macOS/Linux
./deploy.sh --resource-group rg-endatix-prod --mode Incremental

# Windows PowerShell
.\deploy.ps1 -ResourceGroup rg-endatix-prod -Mode Incremental
```

Use `Incremental` mode to update existing deployments without recreating all resources.

### Checking Deployment Status

Monitor deployment progress in Azure Portal:

```bash
# List recent deployments
az deployment group list --resource-group {YOUR_RESOURCE_GROUP} --query '[].name' -o table

# Get detailed deployment info
az deployment group show \
  --resource-group {YOUR_RESOURCE_GROUP} \
  --name endatix-yyyymmdd-hhmmss \
  --query "properties.{provisioningState:provisioningState, timestamp:timestamp, outputs:outputs}" -o json
```

### Getting Help

For deployment script help:

```bash
# macOS/Linux
./deploy.sh --help

# Windows PowerShell
Get-Help .\deploy.ps1 -Detailed
```

---

## Legacy Deployment Notes