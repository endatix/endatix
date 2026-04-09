/* ********* Parameters ********** */

@minLength(1)
@maxLength(10)
@description('Prefix for all resource names (e.g., "prod-", "dev-", "uat-")')
param resource_prefix string = 'temp-'

@description('Azure region for deploying resources')
param location string = resourceGroup().location

@description('Environment name for tagging and identification (e.g., "dev", "uat", "prod")')
param environment string = 'dev'

@description('Git branch to deploy for the Hub static site')
param branch string = 'main'

@description('GitHub repository URL for the Hub frontend application (leave empty for local SWA CLI / zip deploy; set for GitHub CI/CD)')
param hubRepositoryUrl string = ''

@description('GitHub repository URL for the API backend application (leave empty for manual deployment)')
param apiRepositoryUrl string = ''

@description('Git branch to deploy for the API backend')
param apiDeploymentBranch string = 'main'

@description('Azure resource tags for organization and billing')
param tags object = {
  environment: environment
}

@secure()
@description('PostgreSQL administrator username')
param postgres_admin_username string

@secure()
@description('PostgreSQL administrator password')
param postgres_admin_password string

@description('Initial admin user email for first-time API setup')
param initialUserEmail string = 'admin@endatix.com'

@secure()
@description('Initial admin user password for first-time API setup')
param initialUserPassword string

@secure()
@description('JWT signing key for Endatix API auth provider')
param endatixJwtSigningKey string

@secure()
@description('Signing key for Endatix submission access tokens')
param submissionsAccessTokenSigningKey string

@secure()
@description('Hub session secret')
param hubSessionSecret string

@secure()
@description('Hub AUTH secret')
param hubAuthSecret string

@secure()
@description('Hub Next.js server actions encryption key')
param nextServerActionsEncryptionKey string

@description('Application settings for the Hub (Static Web App)')
param hubAppSettings object = {}

@description('Environment variables for the Hub (Static Web App)')
param hubEnvironmentVariables object = {}

@description('Application settings for the API (Web App)')
param apiAppSettings object = {}

@description('Connection strings for the API (Web App)')
param apiConnectionStrings object = {}

@description('PostgreSQL database name')
param postgresqlDatabaseName string = 'endatix-db'

@description('PostgreSQL server version')
param postgresqlVersion string = '16'

@description('Enable high availability for PostgreSQL')
param enablePostgresqlHA bool = false

@description('[OPTIONAL] Set to true if storage account should be private (no public blob access)')
param storageIsPrivate bool = false

@description('[OPTIONAL] Enable automatic failure anomaly alerts (requires subscription registration of Microsoft.AlertsManagement provider)')
param enableFailureAnomalyAlerts bool = false

@allowed([
  'static-site'
  'web-app'
])
@description('The deployment mode for the Endatix Hub frontend (recommended: static-site for fastest evaluation; use web-app if you explicitly want App Service hosting)')
param hubDeploymentMode string = 'static-site'

// Resource naming variables
var endatixAppInsightsName = '${resource_prefix}endatix-appinsights'
var endatixHubName = '${resource_prefix}endatix-hub'
var endatixApiName = '${resource_prefix}endatix-api'
var endatixServicePlanName = '${resource_prefix}endatix-serviceplan'
var endatixStorageAccountName = '${replace(resource_prefix, '-', '')}endatixstorage'
var hubDefaultHostName = hubDeploymentMode == 'static-site'
  ? '${endatixHubName}.azurestaticapps.net'
  : '${endatixHubName}.azurewebsites.net'
var hubBaseUrl = 'https://${hubDefaultHostName}'
var apiDefaultHostName = '${endatixApiName}.azurewebsites.net'
var apiBaseUrl = 'https://${apiDefaultHostName}'
var storageHostName = '${endatixStorageAccountName}.blob.${az.environment().suffixes.storage}'

// Calculate allowed origins for CORS based on hub deployment mode
var allowedOrigins = hubDeploymentMode == 'static-site' ? [
  '${endatixHubName}.azurestaticapps.net'
  '${endatixApiName}.azurewebsites.net'
] : [
  '${endatixHubName}.azurewebsites.net'
  '${endatixApiName}.azurewebsites.net'
]

// App Insights
module appInsights './modules/app-insights.module.bicep' = {
  name: 'app_insights'
  params: {
    location: location
    tags: tags
    workspaceName: '${resource_prefix}endatix-appinsights-ws'
    appInsightsName: endatixAppInsightsName
    enableFailureAnomalyAlerts: enableFailureAnomalyAlerts
  }
}

// BLOB Storage
module endatixStorage './modules/storage.module.bicep' = {
  name: 'endatixStorageModule'
  params: {
    location: location
    storageAccountName: endatixStorageAccountName
    tags: tags
    isPrivate: storageIsPrivate
    allowedOrigins: allowedOrigins
  }
}

// Endatix Hub - Static Web App (default)
module endatixHubSWA './modules/static-site.module.bicep' = if (hubDeploymentMode == 'static-site') {
  name: '${resource_prefix}endatix-hub'
  params: {
    location: location
    branch: branch
    repositoryUrl: hubRepositoryUrl
    staticSiteName: endatixHubName
    tags: tags
    hubCustomDomainName: ''
    appInsightsId: appInsights.outputs.appInsightsId
    appInsightsConnectionString: appInsights.outputs.appInsightsConnectionString
    appInsightsInstrumentationKey: appInsights.outputs.appInsightsInstrumentationKey
    appSettings: union({
      AUTH_URL: hubBaseUrl
      ENDATIX_BASE_URL: apiBaseUrl
      NEXT_PUBLIC_API_URL: '${apiBaseUrl}/api'
      ROBOTS_ALLOWED_DOMAINS: hubDefaultHostName
      AZURE_STORAGE_ACCOUNT_NAME: endatixStorage.outputs.storageAccountName
      AZURE_STORAGE_CUSTOM_DOMAIN: storageHostName
      SESSION_SECRET: hubSessionSecret
      AUTH_SECRET: hubAuthSecret
      NEXT_SERVER_ACTIONS_ENCRYPTION_KEY: nextServerActionsEncryptionKey
      HUB_ADMIN_USERNAME: initialUserEmail
    }, hubAppSettings)
    environmentVariables: union({
      NEXT_PUBLIC_API_URL: '${apiBaseUrl}/api'
    }, hubEnvironmentVariables)
    storageAccountName: endatixStorage.outputs.storageAccountName
    storageAccountKey: endatixStorage.outputs.storageAccountKey
    storageIsPrivate: storageIsPrivate
  }
}

// Endatix Hub - Web App (Node.js)
module endatixHubWebApp './modules/web-app.module.bicep' = if (hubDeploymentMode == 'web-app') {
  name: '${resource_prefix}endatix-hub'
  params: {
    location: location
    tags: tags
    webAppName: endatixHubName
    webAppServicePlanName: endatixServicePlanName
    appInsightsId: appInsights.outputs.appInsightsId
    appInsightsConnectionString: appInsights.outputs.appInsightsConnectionString
    appInsightsInstrumentationKey: appInsights.outputs.appInsightsInstrumentationKey
    repositoryUrl: hubRepositoryUrl
    deploymentBranch: branch
    linuxFxVersion: 'NODE|22-lts'
    appSettings: union({
      AUTH_URL: hubBaseUrl
      ENDATIX_BASE_URL: apiBaseUrl
      NEXT_PUBLIC_API_URL: '${apiBaseUrl}/api'
      ROBOTS_ALLOWED_DOMAINS: hubDefaultHostName
      AZURE_STORAGE_ACCOUNT_NAME: endatixStorage.outputs.storageAccountName
      AZURE_STORAGE_CUSTOM_DOMAIN: storageHostName
      SESSION_SECRET: hubSessionSecret
      AUTH_SECRET: hubAuthSecret
      NEXT_SERVER_ACTIONS_ENCRYPTION_KEY: nextServerActionsEncryptionKey
      HUB_ADMIN_USERNAME: initialUserEmail
    }, hubAppSettings)
    connectionStrings: {}
  }
}

// Endatix API
module endatixApi './modules/web-app.module.bicep' = {
  name: 'endatixApiModule'
  params: {
    location: location
    tags: tags
    webAppName: endatixApiName
    webAppServicePlanName: endatixServicePlanName
    appInsightsId: appInsights.outputs.appInsightsId
    appInsightsConnectionString: appInsights.outputs.appInsightsConnectionString
    appInsightsInstrumentationKey: appInsights.outputs.appInsightsInstrumentationKey
    repositoryUrl: apiRepositoryUrl
    deploymentBranch: apiDeploymentBranch
    linuxFxVersion: 'DOTNETCORE|10.0'
    appSettings: union(apiAppSettings, {
      ASPNETCORE_ENVIRONMENT: 'Production'
      Endatix__Hub__HubBaseUrl: hubDeploymentMode == 'static-site'
        ? 'https://${endatixHubSWA!.outputs.staticWebAppDefaultHostName}'
        : 'https://${endatixHubWebApp!.outputs.appDefaultHostName}'
      Endatix__Storage__Providers__AzureBlob__HostName: storageHostName
      Endatix__Auth__Providers__EndatixJwt__SigningKey: endatixJwtSigningKey
      Endatix__Submissions__AccessTokenSigningKey: submissionsAccessTokenSigningKey
      Endatix__Data__SeedSampleData: true
      Endatix__Data__SeedSampleForms: true
      Endatix__Data__InitialUser__Email: initialUserEmail
      Endatix__Data__InitialUser__Password: initialUserPassword
    })
    connectionStrings: union(apiConnectionStrings, {
      DefaultConnection: {
        value: postgresqlModule.outputs.postgresqlConnectionString
        type: 'custom'
      }
      DefaultConnection_DbProvider: {
        value: 'postgresql'
        type: 'custom'
      }
      StorageConnection: {
        value: endatixStorage.outputs.blobStorageConnectionString
        type: 'Custom'
      }
    })
  }
}

// PostgreSQL Flexible Server
module postgresqlModule './modules/postgres.module.bicep' = {
  name: 'postgresqlModule'
  params: {
    location: location
    resource_prefix: resource_prefix
    tags: tags
    postgres_admin_username: postgres_admin_username
    postgres_admin_password: postgres_admin_password
    postgresVersion: postgresqlVersion
    databaseName: postgresqlDatabaseName
    enableHighAvailability: enablePostgresqlHA
  }
}

/* ********* Outputs ********** */
output hubBaseUrl string = hubDeploymentMode == 'static-site'
  ? 'https://${endatixHubSWA!.outputs.staticWebAppDefaultHostName}'
  : 'https://${endatixHubWebApp!.outputs.appDefaultHostName}'
output apiBaseUrl string = 'https://${endatixApi.outputs.appDefaultHostName}'
output nextPublicApiUrl string = 'https://${endatixApi.outputs.appDefaultHostName}/api'
output hubDefaultHostName string = hubDeploymentMode == 'static-site'
  ? endatixHubSWA!.outputs.staticWebAppDefaultHostName
  : endatixHubWebApp!.outputs.appDefaultHostName
output apiDefaultHostName string = endatixApi.outputs.appDefaultHostName
output resourceGroupName string = resourceGroup().name
output apiAppName string = endatixApiName
output hubAppName string = endatixHubName
