# Endatix Azure Deployment (Quickstart)

Use this guide to get Endatix running quickly on Azure so you can see it in action. This folder contains a Bicep template and an interactive wizard that generate secrets, provision infrastructure, and print the commands to deploy the **Endatix API** (App Service) and **Endatix Hub** (Static Web Apps by default).

For a containerized deployment instead, see the [Install Endatix via Docker guide](https://docs.endatix.com/docs/getting-started/quick-start/#install-via-docker-container).

> [!Note]
> **`parameters.bicepparam`** is the base template. You normally do not edit it for a one-off deploy. Run **`generate-quickstart-secrets.mjs`** to create **`parameters.production.bicepparam`** (secure secrets + an injected **Resource name overrides** block with `// auto:` hints). Deploy with that generated file.

## Prerequisites

Install and sign in before you start:

- **Azure CLI (`az`)** — install and run `az login` ([install docs](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest))
- **SWA CLI (`swa`)** — required when `hubDeploymentMode` is `'static-site'` (default) ([install docs](https://azure.github.io/static-web-apps-cli/docs/use/install))
- **.NET 10 SDK** — build and publish the Endatix API
- **Node.js 22 + pnpm 10** — build the Endatix Hub

### Directory layout

The wizard resolves Hub paths automatically when repos sit next to each other:

**Separate clones (documented quickstart layout):**

```text
endatix/
├── endatix-api/    # github.com/endatix/endatix
│   └── samples/deployment-scripts/azure/   ← run the wizard here
└── endatix-hub/
```

```bash
mkdir endatix && cd endatix
git clone git@github.com:endatix/endatix.git endatix-api
git clone git@github.com:endatix/endatix-hub.git
```

**Monorepo (this repository):** API sources live under `oss/`, Hub under `hub/`. Run the wizard from `oss/samples/deployment-scripts/azure/`; it will look for `hub/.env.production` four levels up.

## Quickstart wizard

From the Azure scripts folder:

```bash
cd endatix-api/samples/deployment-scripts/azure   # or oss/samples/deployment-scripts/azure in the monorepo
node ./generate-quickstart-secrets.mjs
```

**CLI flags** (optional):

| Flag | Effect |
|------|--------|
| *(none)* | If `parameters.production.bicepparam` exists, prompt to Overwrite / Reuse (read-only) / Cancel |
| `--force` | Regenerate secrets and overrides without prompting |
| `--read-only` | Skip secret generation; reuse existing `parameters.production.bicepparam` |

The wizard does **not** call Azure itself. It:

1. Helps you choose or name a resource group (`rg-{project}-{environment}-us` when creating a new one).
2. Writes **`parameters.production.bicepparam`** (secrets + **Resource name overrides** block at the top).
3. Prints **`az group create`** (if needed) and **`az deployment group create`** — run those in **another terminal**, still from this `azure/` folder (paths are relative to here).
4. Waits until you press Enter after infrastructure finishes.
5. Reads **`deployment-outputs.json`** (created by the deploy command’s output redirect) and writes **`.env.production`** into your Hub repo root.
6. Prints Hub and API build/deploy commands (API zip + `az webapp deploy`; Hub **`swa deploy`** when using Static Web Apps).

**Before you deploy infrastructure:** open `parameters.production.bicepparam` and adjust the **Resource name overrides** block or other parameters if needed ([Resource name overrides](#resource-name-overrides), [Key parameters](#key-parameters-quick-reference)).

**Typical timing:** about **5–10 minutes** for the default stack (no managed VNet). If you enable **managed private networking**, the VPN gateway alone can take **30–45 minutes** and adds significant cost.

### Files produced locally

| File | Purpose |
|------|---------|
| `parameters.production.bicepparam` | Parameters for `az deployment group create` |
| `deployment-outputs.json` | Template outputs (`hubBaseUrl`, `apiAppName`, etc.) — keep this file; the wizard reads it after deploy |
| `{hub}/.env.production` | Hub build-time env (API URL, session secret, public flags) |

Example infrastructure command (also printed by the wizard):

```bash
az deployment group create \
  --resource-group rg-endatix-sandbox-us \
  --parameters parameters.production.bicepparam \
  --mode Complete \
  --query properties.outputs -o json > deployment-outputs.json
```

### Build and deploy applications

Run these after infrastructure succeeds. Paths assume the **separate-clone** layout; adjust `cd` targets for the monorepo (`oss/`, `hub/`).

**Hub (default `hubDeploymentMode = 'static-site'`):**

1. `cd` into **endatix-hub** (ensure `.env.production` is in the repo root).
2. `pnpm build:standalone`
3. Deploy with the `swa deploy ...` command from the wizard (run from a directory where `.next/standalone` is available — typically the Hub root after build).

**API:**

1. `cd` into **endatix-api** (repository root containing `src/Endatix.WebHost`).
2. `dotnet publish src/Endatix.WebHost -c Release -o ./publish`
3. Zip `publish/` → `endatix-api.zip` (wizard prints Windows or Unix command).
4. `az webapp deploy ...` (wizard prints resource group and app name from outputs).

> [!Important]
> The wizard’s Hub deploy step uses **`swa deploy`** only. If you set **`hubDeploymentMode = 'web-app'`**, provision with Bicep but deploy the Hub with **`az webapp deploy`** (zip of the standalone build) instead — see [deploy-endatix-to-azure.yml](./deploy-endatix-to-azure.yml) for a CI example.

## What gets provisioned (high level)

The Bicep template deploys into **your** resource group. Default settings target a simple, evaluation-ready stack:

- **Application Insights** (and linked Log Analytics workspace where applicable)
- **Storage account** plus containers and blob configuration (`storageIsPrivate` controls public vs private blob access)
- **PostgreSQL Flexible Server** (API database)
- **Endatix API** — Linux **App Service** on a shared **App Service plan**
- **Endatix Hub** — **Azure Static Web Apps** (`static-site`) or a second **App Service** (`web-app`), per `hubDeploymentMode`
- **Managed VNet (optional)** — when `enablePostgresqlPrivateNetwork = true` and `vnetResourceId` is empty: VNet with `snet-app`, `snet-db`, `GatewaySubnet`, NSGs, **VPN gateway** (`VpnGw2AZ`), and gateway public IP

For resource types, naming, and branches, see [endatix-azure.template.bicep](./endatix-azure.template.bicep) and [modules/](./modules/).

## Key parameters (quick reference)

Edit [parameters.bicepparam](./parameters.bicepparam) before running the wizard, or edit the generated **`parameters.production.bicepparam`** afterward. Full descriptions are in the Bicep template.

| What you want | Parameter(s) |
|---------------|----------------|
| **Hub on Static Web Apps vs App Service** | `hubDeploymentMode`: `'static-site'` (default) or `'web-app'` |
| **Private PostgreSQL + networking** | `enablePostgresqlPrivateNetwork = true`. **Managed VNet:** leave `vnetResourceId` empty. **Your VNet:** set `vnetResourceId`, `postgresSubnetName`, and `apiVirtualNetworkSubnetId` or `apiIntegrationSubnetName` |
| **Managed VNet IP addressing** | `vnetAddressPrefix` (e.g. `10.70.0.0/16` test, `10.71.0.0/16` prod); `vpnAddressPoolPrefix` for point-to-site clients (e.g. `10.0.1.0/24` test) |
| **Private storage** | `storageIsPrivate = true` |
| **PostgreSQL high availability** | `enablePostgresqlHA = true` |
| **Failure anomaly alerts** | `enableFailureAnomalyAlerts = true` (see template notes on provider registration) |
| **Naming / tags** | `resourcePrefix`, `project`, `environment`, `companyName`, `workloadName`, `regionAbbreviation` |
| **Per-resource name overrides** | `*Override` params — [Resource name overrides](#resource-name-overrides) |
| **Git deploy from GitHub (optional)** | `hubRepositoryUrl`, `apiRepositoryUrl`, `branch`, `apiDeploymentBranch` |

## Resource name overrides

**Compute / data plane (default):** `{resourcePrefix}{project}-{suffix}` (e.g. `test-endatix-api` with prefix `test-`).

**Managed VNet resources:** `{abbr}-{companyName}-{workloadName}-{regionAbbreviation}-{environment}` from `parameters.bicepparam` (defaults: `endatix` / `endatix` / `weu` / your `environment` value).

The wizard injects an overrides block into **`parameters.production.bicepparam`** (empty values + `// auto:` hints). Deploying without the wizard: copy `parameters.bicepparam`, add the optional `*Override` parameters, or run the wizard once and reuse the block.

**CAF-style examples** below use fictitious segments `acme` / `datanium` / `eus` / `test` (same as `// override e.g.` hints in `generate-quickstart-secrets.mjs`):

| Override param | Resource type | Default name | CAF-style example |
|----------------|---------------|--------------|-------------------|
| `apiAppNameOverride` | App Service (API) | `{prefix}{project}-api` | `app-acme-datanium-eus-test` |
| `hubAppNameOverride` | Static Web App / Web App | `{prefix}{project}-hub` | `stapp-acme-datanium-eus-test` |
| `appServicePlanNameOverride` | App Service Plan | `{prefix}{project}-serviceplan` | `plan-acme-datanium-eus-test` |
| `appInsightsNameOverride` | Application Insights | `{prefix}{project}-appinsights` | `appi-acme-datanium-eus-test` |
| `logAnalyticsWorkspaceNameOverride` | Log Analytics Workspace | `{prefix}{project}-appinsights-ws` | `log-acme-datanium-eus-test` |
| `postgresqlServerNameOverride` | PostgreSQL Flexible Server | `{prefix}{project}-postgresql` | `psql-acme-datanium-eus-test` |
| `storageAccountNameOverride` | Storage Account | `{prefix}{project}{hash}` (truncated) | `stacmedataniumeustest` |
| `vnetNameOverride` | VNet (managed only) | `vnet-{company}-{workload}-{region}-{env}` | `vnet-acme-datanium-eus-test` |

When managed VNet is enabled, these are also created (no separate override params in v1):

| Resource | Default name pattern | Example |
|----------|----------------------|---------|
| App NSG | `nsg-{company}-{workload}-{region}-{env}-app` | `nsg-acme-datanium-eus-test-app` |
| DB NSG | `nsg-{company}-{workload}-{region}-{env}-db` | `nsg-acme-datanium-eus-test-db` |
| VPN Gateway | `vgw-{company}-{workload}-{region}-{env}-01` | `vgw-acme-datanium-eus-test-01` |
| Gateway Public IP | `pip-{company}-{workload}-{region}-{env}-vgw-01` | `pip-acme-datanium-eus-test-vgw-01` |

**Notes:**

- Leave any override empty (`''`) to keep the auto-generated default.
- **Storage account names:** 3–24 characters, lowercase alphanumeric only, **no dashes**. The example is `st` + `acme` + `datanium` + `eus` + `test`.
- Changing overrides after deploy creates **new** resources; remove the old ones or use a new resource group.

## Optional: GitHub Actions sample

[deploy-endatix-to-azure.yml](./deploy-endatix-to-azure.yml) shows OIDC-based API and Hub deployment for CI. It complements this interactive quickstart; configure repository variables and secrets as documented in that file.

## Optional: compile Bicep to ARM JSON

```bash
cd endatix-api/samples/deployment-scripts/azure
az bicep build --file endatix-azure.template.bicep
```
