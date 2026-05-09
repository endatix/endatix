# Endatix Azure Deployment (Quickstart)

Use this guide to get Endatix running quickly on Azure so you can see it in action. This script provisions the required Azure resources and configures Endatix environment variables and secrets for the Azure App Services hosting model (non-containerized). If you want the containerized deployment model instead, use the [Install Endatix via Docker guide](https://docs.endatix.com/docs/getting-started/quick-start/#install-via-docker-container).

> [!Note]
> `parameters.bicepparam` is a bicep parameters template used to generate your bespoke Azure runtime configuration. You don't need to modify this file unless you want to change the base biceparam structure. As a typical flow, the CLI tool will generate your customized `parameters.production.bicepparam`, which includes an injected **Resource name overrides** block (with `// auto:` hints derived from your `resourcePrefix` and `project`). Edit that block before deploying if you want custom Azure resource names. Use the generated file to provision, deploy and configure your ready-to-test Endatix solution.

## Prerequisites

To follow this guide, you will need the ensure the following are intalled

- **Azure CLI (`az`)** - the Azure Command-Line Interface to get you connected to Azure and provision the needed resources [[documentation link](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)]
- **SWA CLI (`swa`)** - Static Web Apps (SWA) CLI to execute your deployment for Azure Static Sites. [[documentation link](https://azure.github.io/static-web-apps-cli/docs/use/install)]

...and from the Endatix System requirements:

- **.NET 10 SDK** to build and publish the Endatix API
- **Node.js 22 + pnpm 10** - to build your Endatix Hub application

### Directory Structure

Before you start, ensure the following directory structure, so that the scripts are able to correctly resolve paths for the Endatix Hub and Endatix API source codes. 

```bash
endatix/
├── endatix-api/      # cloned from github.com:endatix/endatix.git
├── endatix-hub/      # cloned from github.com:endatix/endatix-hub.git
```

To do so, execute the commands below

```bash
mkdir endatix && cd endatix
git clone git@github.com:endatix/endatix.git endatix-api
git clone git@github.com:endatix/endatix-hub.git
```

## Using the Quickstart Interactice CLI

We have provided an interactive script that streamlines the deployment process. It will guide you. To use it:

1. Open your terminal and cd into the Azure deployment scripts folder (same folder as this README):

```bash
cd endatix-api/samples/deployment-scripts/azure
```

1. Run the interactive Quickstart Wizard and follow the instructions:

```bash
node ./generate-quickstart-secrets.mjs
```

The CLI won't provision any Azure resource, but instead will guide you step-by-step:

1. It will assist you in selecting or creating an Azure Resource Group.
2. It natively generates secure configs, passwords and secrets into a local file (`parameters.production.bicepparam`).
3. **(Optional) Open `parameters.production.bicepparam` and adjust the "Resource name overrides" block at the top** if you want custom resource names (for example, to match the [Azure CAF resource abbreviations](https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations)). See [Resource name overrides](#resource-name-overrides) below. Leave any value empty to keep the auto-generated default.
4. It will provide the exact `az deployment group create` command to provision your Azure resources.
5. While the script runs, it waits for you to complete the deployment in another terminal window.
6. Once complete, it reads the Azure outputs to automatically map runtime URLs and automatically creates `.env.production` in your `endatix-hub` directory.
7. Finally, the script will output the exact commands you need to correctly build and deploy both the Hub and the API from your `endatix` root directory.
8. The entire process should take 5-10 minutes

## What gets provisioned (high level)

The Bicep template deploys into **your** resource group. It's meant to be simple, affordable, evaluation-ready setup, but it can be easily tunned to be production ready. By default you will get:

- **Application Insights** (and linked Log Analytics workspace where applicable)
- **Storage account** plus **containers** and **blob** configuration. BLOBs can be public or private depending on `storageIsPrivate`
- **PostgreSQL Flexible Server** (database for the API)
- **Endatix API** — **App Service (Linux Web App)** on a shared **App Service plan**
- **Endatix Hub** — either **Azure Static Web Apps** or a second **App Service Web App**, depending on `hubDeploymentMode`
- **VNet** [Optional] when `enablePostgresqlPrivateNetwork` is set to true, the template will secure the PostgreSQL with **managed VNet** (or integration with your VNet) for closer to production-ready recommended network level security.

For exact resource types, naming, and conditional branches, see `[endatix-azure.template.bicep](./endatix-azure.template.bicep)` and the modules it references under `[modules/](./modules/)`.

## Key parameters (quick reference)

Edit values in `[parameters.bicepparam](./parameters.bicepparam)` (or the generated `parameters.production.bicepparam` from the wizard). Parameter names and defaults are defined in the template; see `[endatix-azure.template.bicep](./endatix-azure.template.bicep)` for full descriptions and constraints.


| What you want                               | Parameter(s) to change                                                                                                                                                                                                                                                                                                  |
| ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Hub on Static Web Apps vs App Service**   | `hubDeploymentMode`: `'static-site'` (default, fastest for evaluation) or `'web-app'`                                                                                                                                                                                                                                   |
| **Private PostgreSQL + networking**         | `enablePostgresqlPrivateNetwork` — set `true` for private access. **Managed VNet:** leave `vnetResourceId` empty; template creates `{resourcePrefix}endatix-vnet`. **Your VNet:** set `vnetResourceId`, `postgresSubnetName`, and either `apiVirtualNetworkSubnetId` (full subnet ARM ID) or `apiIntegrationSubnetName` |
| **Private storage (no public blob access)** | `storageIsPrivate` — set `true`                                                                                                                                                                                                                                                                                         |
| **PostgreSQL high availability**            | `enablePostgresqlHA` — set `true`                                                                                                                                                                                                                                                                                       |
| **Failure anomaly alerts**                  | `enableFailureAnomalyAlerts` — set `true` (requires provider registration; see template description)                                                                                                                                                                                                                    |
| **Naming / tags**                           | `resourcePrefix`, `project`, `environment`                                                                                                                                                                                                                                                                              |
| **Per-resource name overrides**             | `apiAppNameOverride`, `hubAppNameOverride`, `appServicePlanNameOverride`, `appInsightsNameOverride`, `logAnalyticsWorkspaceNameOverride`, `postgresqlServerNameOverride`, `storageAccountNameOverride`, `vnetNameOverride` — see [Resource name overrides](#resource-name-overrides)                                    |
| **Git deploy from GitHub (optional)**       | `hubRepositoryUrl`, `apiRepositoryUrl`, `branch`, `apiDeploymentBranch`                                                                                                                                                                                                                                                 |


## Resource name overrides

By default, every resource is named using the `{resourcePrefix}{project}-{type}` convention (for example `wetest-endatix-api`). If you need different names — for example to follow the [Azure CAF resource abbreviations](https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations) using a `{type}-{company}-{workload}-{region}-{env}` format — edit the **Resource name overrides** block.

For the quickstart wizard, that block is **injected into `parameters.production.bicepparam`** when secrets are generated (empty values + inline `// auto:` hints match your chosen prefix and project). If you deploy **without** the wizard, copy `[parameters.bicepparam](./parameters.bicepparam)` and add the optional `*Override` parameters yourself (same names as below); omitted overrides default to empty in the template.

Set any of the following:

| Override param                        | Resource type                | Default name                          | CAF-style example                              |
| ------------------------------------- | ---------------------------- | ------------------------------------- | ---------------------------------------------- |
| `apiAppNameOverride`                  | App Service (API)            | `{prefix}{project}-api`               | `app-fairtrade-surveytools-weu-test`           |
| `hubAppNameOverride`                  | Static Web App / Web App     | `{prefix}{project}-hub`               | `stapp-fairtrade-surveytools-weu-test`         |
| `appServicePlanNameOverride`          | App Service Plan             | `{prefix}{project}-serviceplan`       | `plan-fairtrade-surveytools-weu-test`          |
| `appInsightsNameOverride`             | Application Insights         | `{prefix}{project}-appinsights`       | `appi-fairtrade-surveytools-weu-test`          |
| `logAnalyticsWorkspaceNameOverride`   | Log Analytics Workspace      | `{prefix}{project}-appinsights-ws`    | `log-fairtrade-surveytools-weu-test`           |
| `postgresqlServerNameOverride`        | PostgreSQL Flexible Server   | `{prefix}{project}-postgresql`        | `psql-fairtrade-surveytools-weu-test`          |
| `storageAccountNameOverride`          | Storage Account              | `{prefix}{project}{hash}` (truncated) | `stfairtrsurveyweutest`                        |
| `vnetNameOverride`                    | VNet (managed only)          | `{prefix}{project}-vnet`              | `vnet-fairtrade-surveytools-weu-test`          |

Notes:

- Leave any value empty (`''`) to keep the auto-generated default.
- **Storage account names must be 3-24 characters, lowercase alphanumeric only — no dashes.** Plan abbreviations accordingly (the example above shortens `fairtrade` and `surveytools` to fit).
- These overrides take effect on the next `az deployment group create`. If you have already deployed with auto-generated names, changing them will provision new resources alongside the old ones — delete the old resources or your old resource group first.


## Optional: Generate ARM JSON From Bicep

If you need the compiled ARM template for troubleshooting or other tooling:

```bash
cd endatix-api/samples/deployment-scripts/azure
az bicep build --file endatix-azure.template.bicep
```

