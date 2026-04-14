# Endatix Azure Deployment (Quickstart)

Use this guide to get Endatix running quickly on Azure with Bicep.

`parameters.bicepparam` is the primary source of truth for runtime configuration.  
Runtime URLs and hostnames are derived automatically from deployment resource names/default hostnames.

## Prerequisites

- Azure CLI (`az`)
- SWA CLI (`swa`) for Static Web App deployments
- .NET 10 SDK
- Node.js 22 + pnpm 10

### Directory Structure
To ensure the deployment scripts correctly resolve paths for the Hub and API source code, clone the repositories into a shared root folder:
```bash
mkdir endatix && cd endatix
git clone git@github.com:endatix/endatix.git endatix-api
git clone git@github.com:endatix/endatix-hub.git
```

## Quickstart (Interactive CLI)

We have provided an interactive script that streamlines the deployment process.

1. Navigate to the Azure deployment scripts folder:
```bash
cd endatix-api/oss/samples/deployment-scripts/azure
```

2. Run the interactive Quickstart Wizard:
```bash
node ./generate-quickstart-secrets.mjs
```

The script will guide you step-by-step:
1. It will assist you in selecting or creating an Azure Resource Group.
2. It natively generates secure secrets into a local override file (`parameters.production.bicepparam`).
3. It will provide the exact `az deployment group create` command to provision your Azure resources.
4. While the script runs, it waits for you to complete the deployment in another terminal window.
5. Once complete, it reads the Azure outputs to automatically map runtime URLs and automatically creates `.env.production` in your `endatix-hub` directory.
6. Finally, the script will output the exact commands you need to correctly build and deploy both the Hub and the API from your `endatix` root directory.

*Note: If you need to re-run the script after files have been generated, it will automatically prompt whether you want to overwrite or run in read-only mode, or you can add `--read-only` or `--force` flags directly.*

## Configuration Ownership (Important)


| Bucket                        | Source of truth                         | Where to set                                                                            |
| ----------------------------- | --------------------------------------- | --------------------------------------------------------------------------------------- |
| Runtime infrastructure values | Bicep                                   | `parameters.bicepparam` (`hubEnvironmentVariables`, `hubAppSettings`, `apiAppSettings`, and VNet keys below when using private PostgreSQL) |
| Runtime secrets               | Local override + Azure runtime settings | `parameters.production.bicepparam` (generated), then app settings                            |
| Build-time Hub values         | Local env file only                     | `endatix-hub/.env.production` before `pnpm build:standalone` (`NEXT_PUBLIC_*` and UI build flags only) |


### Auto-Mapped Runtime Values (No Guessing)

The template computes hostnames from resource names/default hostnames and injects these automatically:


| Target key                                         | Auto source                                 |
| -------------------------------------------------- | ------------------------------------------- |
| `AUTH_URL`                                         | Hub default hostname (`https://<hub-host>`) |
| `ENDATIX_BASE_URL`                                 | API default hostname (`https://<api-host>`) |
| `NEXT_PUBLIC_API_URL`                              | `https://<api-host>/api`                    |
| `ROBOTS_ALLOWED_DOMAINS`                           | Hub default hostname (host only)            |
| `AZURE_STORAGE_ACCOUNT_NAME`                       | Storage account name from template          |
| `Endatix__Hub__HubBaseUrl`                         | Hub default hostname URL                    |
| `Endatix__Storage__Providers__AzureBlob__HostName` | `<storageAccount>.blob.<suffix>`            |


### Generated Secrets and First Admin

The script generates and writes these to `parameters.production.bicepparam`:


| Key                                | Generated value                        |
| ---------------------------------- | -------------------------------------- |
| `hubSessionSecret`                 | signing secret                         |
| `hubAuthSecret`                    | signing secret                         |
| `nextServerActionsEncryptionKey`   | signing secret                         |
| `endatixJwtSigningKey`             | signing secret                         |
| `submissionsAccessTokenSigningKey` | signing secret                         |
| `initialUserEmail`                 | default `admin@endatix.com` (editable) |
| `initialUserPassword`              | generated random password              |


These map at runtime to:

- Hub: `SESSION_SECRET`, `AUTH_SECRET`, `NEXT_SERVER_ACTIONS_ENCRYPTION_KEY`, `HUB_ADMIN_USERNAME`
- API: `Endatix__Auth__Providers__EndatixJwt__SigningKey`, `Endatix__Submissions__AccessTokenSigningKey`, `Endatix__Data__InitialUser__Email`, `Endatix__Data__InitialUser__Password`

Notes:

- Connection strings are template-managed (`DefaultConnection`, `StorageConnection`).
- Keep `hubRepositoryUrl` and `apiRepositoryUrl` empty for manual/local deployment.
- `endatix-hub/.env.production` is intentionally minimal; runtime values like `AUTH_URL`, `ENDATIX_BASE_URL`, secrets, and admin identity come from Azure app settings provisioned by Bicep.

## Private PostgreSQL (VNet integration)

Defaults in `parameters.bicepparam` are correct for the public quickstart: `enablePostgresqlPrivateNetwork = false` and all VNet-related strings empty. Turn private networking on only when you want PostgreSQL off the public internet.

Set `enablePostgresqlPrivateNetwork` to `true` when you need private access.

- **Managed VNet:** leave `vnetResourceId` empty (and leave `postgresSubnetName`, `apiIntegrationSubnetName`, and `apiVirtualNetworkSubnetId` empty). The VNet name is `{resourcePrefix}endatix-vnet` (for example `test-endatix-vnet` with the sample prefix). Subnets: `snet-app` (API / `Microsoft.Web/serverFarms`), `snet-db` (PostgreSQL / `Microsoft.DBforPostgreSQL/flexibleServers`), `snet-pe` (reserved). The API Web App uses regional VNet integration and routed outbound traffic to reach the database.
- **Your own VNet:** set `vnetResourceId` to the VNet resource ID, `postgresSubnetName` to the PostgreSQL delegated subnet name, and either `apiVirtualNetworkSubnetId` (full ARM subnet ID; use when the subnet is in another resource group) or `apiIntegrationSubnetName` (subnet name in that VNet, delegated to `Microsoft.Web/serverFarms`).

To reach the VNet from your laptop (for example to connect to private PostgreSQL), provision a **point-to-site VPN** on an Azure **Virtual network gateway**

## Alternative: Hub on Web App

If you choose `hubDeploymentMode: "web-app"`:

```bash
pnpm build:standalone
( cd .next/standalone && zip -r ../hub-standalone.zip . )

az webapp config set \
  --resource-group RESOURCE_GROUP_NAME \
  --name HUB_APP_NAME \
  --startup-file "node server.js"

az webapp deploy \
  --resource-group RESOURCE_GROUP_NAME \
  --name HUB_APP_NAME \
  --src-path hub-standalone.zip \
  --type zip
```

## Developer Loop

1. Update `parameters.bicepparam`, regenerate `parameters.production.bicepparam`, and review `endatix-hub/.env.production`
2. Rebuild API and Hub
3. Redeploy API and Hub
4. Verify:
  - `az webapp log tail --resource-group RESOURCE_GROUP_NAME --name API_APP_NAME`
  - `az staticwebapp environment list --name HUB_APP_NAME --resource-group RESOURCE_GROUP_NAME -o table`

## Quick Troubleshooting

- SWA warmup timeout: deploy from `.next/standalone` (or add `--output-location .next/standalone`) and use `--api-language node --api-version 22`
- Hub can open but API fails: check `NEXT_PUBLIC_API_URL` and API CORS
- API starts but auth fails: verify generated secrets were applied and redeploy API

## Optional: Generate ARM JSON From Bicep

If you need the compiled ARM template for troubleshooting or other tooling:

```bash
cd oss/samples/deployment-scripts/azure
az bicep build --file endatix-azure.template.bicep
```

## Optional: OSS Root package.json Criteria

Do not add `oss/package.json` yet. Add it only when at least one of these becomes true:

- Multiple OSS-wide scripts need stable aliases (for example `pnpm quickstart:azure`).
- Shared script dependencies are required across two or more OSS areas.
- Script test/lint automation is needed at OSS root.