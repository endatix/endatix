# Endatix Azure Deployment

This guide shows you how to deploy Endatix to Azure using Bicep templates. This is a simplified process designed for developers who want to evaluate Endatix on Azure and iterate quickly using app based Azure self-hosting. For Docker based self-hosting check the [https://docs.endatix.com/docs/guides/docker-setup/](https://docs.endatix.com/docs/guides/docker-setup/)

## Prerequisites

Before you begin, ensure you have these tools installed:

- **Azure Tools** - tools you will need to provision the Azure resources and deploy the Endatix Apps to them. For exact list of resources that will be provisioned check the https://docs.endatix.com/docs/guides/azure docs
   - **Azure CLI** - the cross-platform command-line tool for managing Azure resources ([install link](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest))
   - **Azure SWA** - All-in-One CLI for managing Azure Static Web Apps. This will be only needed if you choose to deploy the Endatix Hub to a static web site. If you choose to use Azure Web site, you can skil it ([install link](https://azure.github.io/static-web-apps-cli/))
- **Endatix System Requirements**
   - **.NET 10.0** - for building the Endatix API project ([install link](https://dotnet.microsoft.com/en-us/download/dotnet/10.0))
   - **Node.js 22 LTS** - for building the Endatix Hub project ([install link](https://nodejs.org/en/blog/release/v22.22.0)). Note: for using multinple version of node on the same machine, we recommend using NVMM (Node version manager) - https://github.com/nvm-sh/nvm
   - **pnpm** - The recommended package manager for node. We use version 10x - ([install link](https://pnpm.io/installation))

And last, but not least, you will need Azure subscription with permissions to create resources. Let's go!

## 1. Prepare Configuration

Create a `parameters.json` file in this directory. This is your single source of truth for all configuration - no passwords will appear in your CLI history.

**For evaluation/local development**, use this minimal template (no repository URLs needed):

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "resource_prefix": {
      "value": "eval-"
    },
    "environment": {
      "value": "development"
    },
    "hubDeploymentMode": {
      "value": "web-app"
    },
    "postgres_admin_username": {
      "value": "endatixadmin"
    },
    "postgres_admin_password": {
      "value": "YOUR_SECURE_PASSWORD_HERE"
    }
  }
}
```

**Important Notes:**

- `resource_prefix`: Use "eval-" for testing, "prod-" for production
- `hubDeploymentMode`: Use `**web-app`** for this repository. The Hub is built with Next.js `output: 'standalone'`, which produces a **Node `server.js`**. Azure **Static Web Apps** only serves static assets (and optional Functions); it does **not** run that server. Deploying `.next/standalone` with `swa deploy` typically ends in **“Web app warm up timed out”** because the platform never runs your Node process correctly for that layout.
- `postgres_admin_password`: Must be 8-128 characters, contain uppercase, lowercase, numbers, and special characters
- **Omit `hubRepositoryUrl` and `apiRepositoryUrl` (or set them to `""`)** for manual deploys. Non-empty values wire GitHub CI/CD on the Azure resources.

## 2. Deploy Infrastructure

Run these three commands to provision all Azure resources:

```bash
# 1. Create resource group
az group create --name rg-endatix-eval --location eastus

# 2. Deploy infrastructure
az deployment group create \
  --resource-group rg-endatix-eval \
  --template-file endatix-azure.template.bicep \
  --parameters parameters.json \
  --mode Complete

# 3. Confirm deployment
az deployment group show --resource-group rg-endatix-eval --name endatix-azure.template
```

This creates:

- PostgreSQL Flexible Server database
- Azure App Service for the API
- Azure Static Web App for the Hub (or App Service if you chose "web-app" mode)
- Azure Blob Storage for file uploads
- Application Insights for monitoring

## 3. Deploy Your Code

After infrastructure is ready, deploy your local code changes.

### Deploy the API

Build and deploy the .NET API:

```bash
# clone the https://github.com/endatix/endatix
cd endatix

# Build the API (from the endatix directory root)
cd ./src/Endatix.WebHost
dotnet publish -c Release -o ./publish

# Create deployment zip
cd publish
zip -r ../../api.zip .

# Deploy to Azure
az webapp deployment source config-zip \
  --resource-group rg-endatix-eval \
  --name eval-endatix-api \
  --src ../../../api.zip
```

### Deploy the Hub

Build the standalone bundle (same layout as `pnpm run:standalone`, without starting the server):

```bash
# from the Hub repo root (e.g. endatix-hub/ or hub/)
pnpm build:standalone
```

Create a zip whose **root** is the contents of `.next/standalone` (so `server.js` is at the root of the archive — not inside a `.next/standalone/` folder):

```bash
( cd .next/standalone && zip -r ../hub-standalone.zip . )
```

Configure the **Linux App Service** to start Node at the site root (set once per app; `eval-endatix-hub` must match your `resource_prefix` + `endatix-hub`):

```bash
az webapp config set \
  --resource-group rg-endatix-eval \
  --name eval-endatix-hub \
  --startup-file "node server.js"
```

Deploy the zip:

```bash
az webapp deploy \
  --resource-group rg-endatix-eval \
  --name eval-endatix-hub \
  --src-path hub-standalone.zip \
  --type zip
```

**Static Web Apps (same as `build-and-deploy.yml`)**  
If the Hub is hosted on a **Static Web App** (not App Service), deploy the standalone folder with the SWA CLI **and** set the API runtime version explicitly. The CLI defaults to **Node 16** (`FUNCTION_LANGUAGE_VERSION`), while this repo uses **Node 22** in `staticwebapp.config.json` — that mismatch often surfaces as **warm-up timeouts**.

```bash
pnpm build:standalone   # copies staticwebapp.config.json into .next/standalone/

swa deploy .next/standalone \
  --env production \
  --resource-group <your-rg> \
  --app-name <prefix>endatix-hub \
  --api-language node \
  --api-version 22
```

Or set once in your environment: `SWA_CLI_API_LANGUAGE=node` and `SWA_CLI_API_VERSION=22`. You can also deploy with the **deployment token** (same as GitHub Actions): `swa deploy .next/standalone --deployment-token "$HUB_DEPLOYMENT_TOKEN" --api-language node --api-version 22`.

**App Service** remains a valid option: use `hubDeploymentMode: "web-app"` and the zip + `node server.js` steps above if you prefer Linux Web App hosting.

## 4. Making Changes

To iterate on your code:

1. **Make your changes** in the local codebase
2. **Rebuild and redeploy** using the commands above
3. **Test your changes** at the Hub URL shown in deployment output

The infrastructure stays provisioned - you only redeploy code.

## 5. Production Path

For production deployments with CI/CD:

1. **Fork the repositories** to your GitHub organization
2. **Update parameters.json** with production values and repository URLs:
  ```json
   {
     "hubRepositoryUrl": {
       "value": "https://github.com/YOUR_ORG/endatix-hub"
     },
     "apiRepositoryUrl": {
       "value": "https://github.com/YOUR_ORG/endatix"
     }
   }
  ```
3. **Deploy infrastructure** (same commands as above)
4. **Push code** to your forked repositories - Azure will auto-deploy

## 6. Troubleshooting

### Common Issues

**"Resource group not found"**

- Ensure you created the resource group first
- Check the resource group name matches exactly

**"Invalid password"**

- PostgreSQL password must meet complexity requirements
- Use a strong password with mixed case, numbers, and symbols

**"Deployment failed"**

- Check Azure CLI authentication: `az account show`
- Verify parameters.json syntax with a JSON validator
- Review deployment logs: `az deployment group show --resource-group rg-endatix-eval --name endatix-azure.template`

**"Code deployment failed"**

- For API: Ensure you published from `src/Endatix.WebHost` (see Deploy the API above)
- For Hub: Run `pnpm build:standalone`, zip **contents** of `.next/standalone`, deploy to the **App Service** Hub (`hubDeploymentMode: "web-app"`), startup `node server.js`
- Check resource names match your `resource_prefix`

**"Web app warm up timed out" (`swa deploy`)**

1. Pass `**--api-language node --api-version 22`** (or env `SWA_CLI_API_VERSION=22`). The SWA CLI defaults to **Node 16**, which conflicts with `**platform.apiRuntime: "node:22"`** in `staticwebapp.config.json` and commonly breaks warm-up for this app.
2. Ensure `**staticwebapp.config.json**` is in the deployed folder (`pnpm build:standalone` copies it into `.next/standalone/`).
3. If it still fails, try **deployment-token** deploy (matches CI) or move the Hub to **App Service** (`hubDeploymentMode: "web-app"`) as in [Deploy the Hub](#deploy-the-hub).

**"The project is linked to GitHub" (Static Web Apps CLI)**

Only applies if you still use a **Static Web App** resource with GitHub connected. Unlink, or use App Service for the Hub instead:

```bash
az staticwebapp disconnect --name <prefix>endatix-hub --resource-group <your-rg>
```

Then deploy the Hub to **App Service** (zip + `node server.js`), not `swa deploy`.

### Getting Help

- Check deployment status: `az deployment operation group list --resource-group rg-endatix-eval --name endatix-azure.template`
- View application logs: `az webapp log tail --resource-group rg-endatix-eval --name eval-endatix-api`


