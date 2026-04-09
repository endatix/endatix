using './endatix-azure.template.bicep'

// Quickstart note:
// Keep deployment outputs persisted locally - do not delete deployment-outputs.json.
// Example infra deployment command:
// az deployment group create --resource-group rg-endatix-sandbox-us --parameters parameters.deploy.bicepparam --mode Complete --query properties.outputs -o json > deployment-outputs.json
// The build-env step reads this file:
// node ./generate-quickstart-secrets.mjs build-env --outputs-file ./deployment-outputs.json

param resource_prefix = 'test-'
param environment = 'sandbox'
param hubDeploymentMode = 'static-site'
param branch = 'main'
param hubRepositoryUrl = ''
param apiRepositoryUrl = ''
param apiDeploymentBranch = ''

param postgres_admin_username = 'endatixadmin'
param postgres_admin_password = 'CHANGE_ME_STRONG_PASSWORD'

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

param hubAppSettings = {}

param apiAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
}

param apiConnectionStrings = {}
param enablePostgresqlHA = false
param storageIsPrivate = false
param enableFailureAnomalyAlerts = false
