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

2. **An Azure Resource Group** created

   ```bash
   az group create --name rg-endatix-prod --location eastus
   ```

3. **PostgreSQL Admin Credentials** ready (username and password)

## Template Parameters

| Parameter                 | Type            | Default                      | Description                                                  |
| ------------------------- | --------------- | ---------------------------- | ------------------------------------------------------------ |
| `resource_prefix`         | string          | `temp-`                      | Prefix for all resource names (e.g., 'prod-', 'uat-', 'ci-') |
| `location`                | string          | Resource Group location      | Azure region for deploying resources                         |
| `environment`             | string          | `dev`                        | Environment name (dev, uat, prod) - used for tagging         |
| `branch`                  | string          | `main`                       | Git branch to deploy for the Hub static site                 |
| `tags`                    | object          | `{environment: environment}` | Azure resource tags for organization and billing             |
| `postgres_admin_username` | string (secure) | _Required_                   | PostgreSQL administrator username                            |
| `postgres_admin_password` | string (secure) | _Required_                   | PostgreSQL administrator password                            |

## Deployment Instructions

### Basic Deployment

```bash
az deployment group create \
  --resource-group rg-endatix-prod \
  --template-file endatix-azure.template.bicep \
  --mode Complete \
  --parameters \
    postgres_admin_username=adminuser \
    postgres_admin_password='YourSecurePassword123!'
```

### Deployment with Custom Parameters

```bash
az deployment group create \
  --resource-group rg-endatix-prod \
  --template-file endatix-azure.template.bicep \
  --mode Complete \
  --parameters \
    resource_prefix=prod- \
    environment=production \
    branch=main \
    location=westus2 \
    postgres_admin_username=adminuser \
    postgres_admin_password='YourSecurePassword123!'
```

### Using a Parameters File

Create `parameters.json`:

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
    "branch": {
      "value": "main"
    },
    "location": {
      "value": "eastus"
    },
    "postgres_admin_username": {
      "value": "adminuser"
    },
    "postgres_admin_password": {
      "value": "YourSecurePassword123!"
    }
  }
}
```

Then deploy:

```bash
az deployment group create \
  --resource-group rg-endatix-prod \
  --template-file endatix-azure.template.bicep \
  --parameters parameters.json \
  --mode Complete
```

## Examples

### Development Environment

```bash
az deployment group create \
  --resource-group rg-endatix-dev \
  --template-file endatix-azure.template.bicep \
  --mode Complete \
  --parameters \
    resource_prefix=dev- \
    environment=development \
    branch=develop \
    postgres_admin_username=adminuser \
    postgres_admin_password='DevPassword123!'
```

### Production Environment

```bash
az deployment group create \
  --resource-group rg-endatix-prod-us \
  --template-file endatix-azure.template.bicep \
  --mode Complete \
  --parameters \
    resource_prefix=prod- \
    environment=production \
    branch=main \
    location=eastus \
    postgres_admin_username=prodadmin \
    postgres_admin_password='ProdSecurePassword123!' \
    tags='{"environment":"production","team":"platform","cost-center":"engineering"}'
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

## Outputs

After successful deployment, retrieve outputs with:

```bash
az deployment group show \
  --resource-group rg-endatix-prod \
  --name endatix-azure.template
```

Key resources created:

- Static Web App hosting Endatix Hub
- App Service hosting Endatix API
- Blob Storage for file uploads
- PostgreSQL database connection

## Module Dependencies

This template uses the following modules (ensure they exist in the `modules/` directory):

- `modules/app-insights.module.bicep`
- `modules/static-site.module.bicep`
- `modules/endatix-api.module.bicep`
- `modules/storage.module.bicep`

## Security Considerations

- PostgreSQL admin credentials are marked as `@secure()` and should never be committed to version control
- Use Azure Key Vault to store secrets and reference them in parameters
- Firewall rules allow public access - consider restricting based on your security requirements
- Enable advanced threat protection for production environments
- Regularly update and patch PostgreSQL versions

## Troubleshooting

### Deployment Fails with "Resource Already Exists"

The template uses `Complete` mode, which removes resources not defined in the template. Ensure you're using the correct resource group and consider using `Incremental` mode for updates.

### PostgreSQL Connection Issues

- Verify firewall rules allow your IP address
- Check that `require_secure_transport` is configured for your client
- Ensure SSL certificate is properly downloaded for local connections

### Static Site Not Accessible

- Verify the GitHub/Git repository is accessible
- Check branch name exists in the repository
- Review App Insights logs for deployment errors

```

```
