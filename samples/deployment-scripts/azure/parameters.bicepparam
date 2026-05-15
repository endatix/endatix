using './endatix-azure.template.bicep'

// Quickstart note:
// 1) Run: node ./generate-quickstart-secrets.mjs  (creates parameters.production.bicepparam)
// 2) Deploy from this folder; keep deployment-outputs.json for the wizard's second phase:
//    az deployment group create --resource-group rg-endatix-sandbox-us --parameters parameters.production.bicepparam --mode Complete --query properties.outputs -o json > deployment-outputs.json
//
// Resource name overrides are not declared here. The wizard injects that block into parameters.production.bicepparam.
// Deploying without the wizard? Copy this file to parameters.production.bicepparam and add *Override params — see README.

// --- Naming convention (wizard) ---

// --- Naming: CAF segments (quickstart: company=project, workload=endatix, region=weu) ---
param companyName = 'endatix'
param workloadName = 'endatix'
param regionAbbreviation = 'weu'
param environment = 'sandbox'
param project = 'endatix'

// Managed VNet address space (subnets derived automatically). Prod example: 10.71.0.0/16
param vnetAddressPrefix = '10.70.0.0/16'
// Point-to-site VPN pool (separate from VNet). Prod example: 10.0.2.0/24
param vpnAddressPoolPrefix = '10.0.1.0/24'
param hubDeploymentMode = 'static-site'
param branch = 'main'
param hubRepositoryUrl = ''
param apiRepositoryUrl = ''
param apiDeploymentBranch = ''

param postgresAdminUsername = 'endatixadmin'
param postgresAdminPassword = 'CHANGE_ME_STRONG_PASSWORD'

param initialUserEmail = 'admin@endatix.com'
param initialUserPassword = 'CHANGE_ME_INITIAL_ADMIN_PASSWORD'
param endatixJwtSigningKey = 'CHANGE_ME_ENDATIX_JWT_SIGNING_KEY'
param submissionsAccessTokenSigningKey = 'CHANGE_ME_SUBMISSIONS_SIGNING_KEY'
param hubSessionSecret = 'CHANGE_ME_HUB_SESSION_SECRET'
param hubAuthSecret = 'CHANGE_ME_HUB_AUTH_SECRET'
param nextServerActionsEncryptionKey = 'CHANGE_ME_NEXT_SERVER_ACTIONS_ENCRYPTION_KEY'

param hubEnvironmentVariables = {
  NEXT_PUBLIC_ENVIRONMENT: 'production'
}

param apiAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
}

param apiConnectionStrings = {}
param enablePostgresqlHA = false

// --- PostgreSQL + VNet (see README "What gets provisioned" / "Key parameters") ---
// Quickstart default: private network off — leave all of the following empty / false.
// Managed VNet: set enablePostgresqlPrivateNetwork = true; keep vnetResourceId empty
//   (template creates CAF-named VNet with snet-app, snet-db, GatewaySubnet, NSGs, VPN gateway).
// BYO VNet: enablePostgresqlPrivateNetwork = true; set vnetResourceId + postgresSubnetName;
//   set apiVirtualNetworkSubnetId OR apiIntegrationSubnetName for the API Web App subnet.
param enablePostgresqlPrivateNetwork = false
param vnetResourceId = ''
param postgresSubnetName = ''
param apiIntegrationSubnetName = ''
param apiVirtualNetworkSubnetId = ''
param storageIsPrivate = false
param enableFailureAnomalyAlerts = false
