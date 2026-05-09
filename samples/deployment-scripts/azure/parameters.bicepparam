using './endatix-azure.template.bicep'

// Quickstart note:
// Keep deployment outputs persisted locally - do not delete deployment-outputs.json.
// Example infra deployment command:
// az deployment group create --resource-group rg-endatix-sandbox-us --parameters parameters.deploy.bicepparam --mode Complete --query properties.outputs -o json > deployment-outputs.json
// The build-env step reads this file:
// node ./generate-quickstart-secrets.mjs build-env --outputs-file ./deployment-outputs.json
//
// Resource name overrides: not declared here. When you run generate-quickstart-secrets.mjs, it injects
// an optional "Resource name overrides" block (with accurate // auto: hints) into parameters.production.bicepparam.
// Deploying without the wizard? Copy this file and add the optional *Override params — see README "Resource name overrides".

param resourcePrefix = 'test-'
param project = 'endatix'
param environment = 'sandbox'
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
//   (template creates {resourcePrefix}endatix-vnet with snet-app, snet-db, snet-pe).
// BYO VNet: enablePostgresqlPrivateNetwork = true; set vnetResourceId + postgresSubnetName;
//   set apiVirtualNetworkSubnetId OR apiIntegrationSubnetName for the API Web App subnet.
param enablePostgresqlPrivateNetwork = false
param vnetResourceId = ''
param postgresSubnetName = ''
param apiIntegrationSubnetName = ''
param apiVirtualNetworkSubnetId = ''
param storageIsPrivate = false
param enableFailureAnomalyAlerts = false
