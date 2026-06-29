/* ********* Parameters ********** */
@description('Azure region for deploying resources')
param location string = resourceGroup().location

@description('Environment name for tagging and identification (e.g., "dev", "uat", "prod")')
param environment string = 'dev'

@description('Project token used in service names (defaults to "endatix")')
param project string = 'endatix'

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
param postgresAdminUsername string

@secure()
@description('PostgreSQL administrator password')
param postgresAdminPassword string

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

@description('Enable private network access (VNet integration) for PostgreSQL')
param enablePostgresqlPrivateNetwork bool = false

@description('Existing VNet resource ID. Leave empty with enablePostgresqlPrivateNetwork to create a managed VNet (snet-app, snet-db, GatewaySubnet).')
param vnetResourceId string = ''

@description('Managed VNet address space (app/db/gateway subnets are derived via cidrSubnet). Example prod: 10.71.0.0/16')
param vnetAddressPrefix string = '10.70.0.0/16'

@description('Point-to-site VPN client address pool (separate from VNet space). Example prod: 10.0.2.0/24')
param vpnAddressPoolPrefix string = '10.0.1.0/24'

@description('Company segment for CAF-style resource names (e.g. acme)')
param companyName string = project

@description('Workload segment for CAF-style resource names (e.g. endatix)')
param workloadName string = 'endatix'

@description('Azure region abbreviation for CAF-style resource names (e.g. weu, eus)')
param regionAbbreviation string = 'weu'

@description('When enablePostgresqlPrivateNetwork and vnetResourceId is set: PostgreSQL delegated subnet name inside that VNet')
param postgresSubnetName string = ''

@description('When enablePostgresqlPrivateNetwork and vnetResourceId is set: subnet name for API App Service VNet integration (ignored if apiVirtualNetworkSubnetId is set)')
param apiIntegrationSubnetName string = ''

@description('Optional full ARM subnet ID for API App Service VNet integration (overrides apiIntegrationSubnetName when using your own VNet)')
param apiVirtualNetworkSubnetId string = ''

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

// --- Resource name overrides (optional) ---
// Leave empty for CAF auto names: {abbr}-{companyName}-{workloadName}-{regionAbbreviation}-{environment}[-{role}]

@description('Override: API App Service name. Leave empty for CAF auto name (app-...)')
param apiAppNameOverride string = ''

@description('Override: Hub App name. Leave empty for CAF auto name (stapp- or app- by hubDeploymentMode)')
param hubAppNameOverride string = ''

@description('Override: App Service Plan name. Leave empty for CAF auto name (plan-...)')
param appServicePlanNameOverride string = ''

@description('Override: Application Insights name. Leave empty for CAF auto name (appi-...)')
param appInsightsNameOverride string = ''

@description('Override: Log Analytics Workspace name. Leave empty for CAF auto name (log-...)')
param logAnalyticsWorkspaceNameOverride string = ''

@description('Override: PostgreSQL Flexible Server name. Leave empty for CAF auto name (psql-...)')
param postgresqlServerNameOverride string = ''

@description('Override: Storage Account name (3-24 chars, lowercase alphanumeric only). Leave empty for CAF auto name (st+segments)')
param storageAccountNameOverride string = ''

@description('Override: VNet name (managed VNet only). Leave empty for CAF auto name (vnet-...)')
param vnetNameOverride string = ''

var cafNameSuffix = '${companyName}-${workloadName}-${regionAbbreviation}-${environment}'

func cafName(abbr string, suffix string, role string) string =>
  empty(role) ? '${abbr}-${suffix}' : '${abbr}-${suffix}-${role}'

func stripDashes(value string) string => replace(replace(value, '-', ''), '_', '')

func cafStorageAccountName(company string, workload string, region string, env string) string =>
  take('st${stripDashes(company)}${stripDashes(workload)}${region}${stripDashes(env)}', 24)

func resolveResourceName(override string, abbr string, suffix string, role string) string =>
  !empty(override) ? override : cafName(abbr, suffix, role)

var hubCafAbbr = hubDeploymentMode == 'static-site' ? 'stapp' : 'app'

var endatixAppInsightsName = resolveResourceName(appInsightsNameOverride, 'appi', cafNameSuffix, '')
var endatixLogAnalyticsWsName = resolveResourceName(logAnalyticsWorkspaceNameOverride, 'log', cafNameSuffix, '')
var endatixHubName = resolveResourceName(hubAppNameOverride, hubCafAbbr, cafNameSuffix, '')
var endatixApiName = resolveResourceName(apiAppNameOverride, 'app', cafNameSuffix, '')
var endatixServicePlanName = resolveResourceName(appServicePlanNameOverride, 'plan', cafNameSuffix, '')
var endatixVnetName = resolveResourceName(vnetNameOverride, 'vnet', cafNameSuffix, '')
var appSubnetPrefix = cidrSubnet(vnetAddressPrefix, 24, 0)
var dbSubnetPrefix = cidrSubnet(vnetAddressPrefix, 24, 1)
var gatewaySubnetPrefix = cidrSubnet(vnetAddressPrefix, 24, 3)
var appNsgName = cafName('nsg', cafNameSuffix, 'app')
var dbNsgName = cafName('nsg', cafNameSuffix, 'db')
var vgwName = cafName('vgw', cafNameSuffix, '01')
var vgwPipName = cafName('pip', cafNameSuffix, 'vgw-01')
var endatixStorageAccountName = !empty(storageAccountNameOverride)
  ? storageAccountNameOverride
  : cafStorageAccountName(companyName, workloadName, regionAbbreviation, environment)
var endatixPostgresqlServerName = resolveResourceName(postgresqlServerNameOverride, 'psql', cafNameSuffix, '')
var deployManagedVnet = enablePostgresqlPrivateNetwork && vnetResourceId == ''
var storageHostName = '${endatixStorageAccountName}.blob.${az.environment().suffixes.storage}'
var resolvedHubDefaultHostName = hubDeploymentMode == 'static-site'
  ? endatixHubSWA!.outputs.staticWebAppDefaultHostName
  : endatixHubWebApp!.outputs.appDefaultHostName
var resolvedHubBaseUrl = 'https://${resolvedHubDefaultHostName}'
var apiBaseUrl = 'https://${endatixApi.outputs.appDefaultHostName}'
var apiPublicUrl = '${apiBaseUrl}/api'

// Shared Hub settings used by both deployment modes during initial provisioning.
var hubBaseSettings = {
  ENDATIX_BASE_URL: apiBaseUrl
  NEXT_PUBLIC_API_URL: apiPublicUrl
  AZURE_STORAGE_ACCOUNT_NAME: endatixStorage.outputs.storageAccountName
  AZURE_STORAGE_ACCOUNT_KEY: endatixStorage.outputs.storageAccountKey
  AZURE_STORAGE_IS_PRIVATE: storageIsPrivate? 'true' : ''
  AZURE_STORAGE_CUSTOM_DOMAIN: storageHostName
  SESSION_SECRET: hubSessionSecret
  AUTH_SECRET: hubAuthSecret
  NEXT_SERVER_ACTIONS_ENCRYPTION_KEY: nextServerActionsEncryptionKey
  HUB_ADMIN_USERNAME: initialUserEmail
}
var hubInitialAppSettings = union(hubBaseSettings, hubEnvironmentVariables)
var hubFinalizedAppSettings = union(hubInitialAppSettings, {
  AUTH_URL: resolvedHubBaseUrl
  ROBOTS_ALLOWED_DOMAINS: resolvedHubDefaultHostName
})

// Blob CORS uses real hostnames; applied in leaf module after Hub + API exist.
var resolvedStorageCorsOrigins = [
  resolvedHubBaseUrl
  'https://${endatixApi.outputs.appDefaultHostName}'
]

// Serilog → Application Insights via app settings (Azure __ nesting)
var apiSerilogApplicationInsightsSettings = {
  Serilog__Using__0: 'Serilog.Sinks.ApplicationInsights'
  Serilog__WriteTo__0__Name: 'ApplicationInsights'
  Serilog__WriteTo__0__Args__telemetryConverter: 'Serilog.Sinks.ApplicationInsights.TelemetryConverters.TraceTelemetryConverter, Serilog.Sinks.ApplicationInsights'
}

/* ********* Finalize Existing Resources ********** */

// Endatix API - Finalize app settings with resolved Hub hostname
// NOTE: This finalizer pattern is required because Azure App Service and Static Web Apps use undeterministic 
// auto-generated hostnames. We must deploy the resources first to generate their URLs, then patch the settings.
resource endatixApiFinalize 'Microsoft.Web/sites/config@2025-03-01' = {
  name: '${endatixApiName}/appsettings'
  dependsOn: [
    endatixApi
  ]
  properties: union(
    apiAppSettings,
    apiSerilogApplicationInsightsSettings,
    {
      APPINSIGHTS_INSTRUMENTATIONKEY: appInsights.outputs.appInsightsInstrumentationKey
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.outputs.appInsightsConnectionString
      ASPNETCORE_ENVIRONMENT: 'Production'
      Endatix__Storage__Providers__AzureBlob__HostName: storageHostName
      Endatix__Auth__Providers__EndatixJwt__SigningKey: endatixJwtSigningKey
      Endatix__Submissions__AccessTokenSigningKey: submissionsAccessTokenSigningKey
      Endatix__Data__SeedSampleData: true
      Endatix__Data__SeedSampleForms: true
      Endatix__Data__InitialUser__Email: initialUserEmail
      Endatix__Data__InitialUser__Password: initialUserPassword
    }
  )
}


resource endatixHubFinalize 'Microsoft.Web/sites/config@2025-03-01' = if (hubDeploymentMode == 'web-app') {
  name: '${endatixHubName}/appsettings'
  properties: union(
    hubFinalizedAppSettings,
    hubEnvironmentVariables
  )
}

resource endatixHubSwaFinalize 'Microsoft.Web/staticSites/config@2025-03-01' = if (hubDeploymentMode == 'static-site') {
  name: '${endatixHubName}/appsettings'
  properties: union(
    hubFinalizedAppSettings,
    hubEnvironmentVariables
  )
}

/* ********* Deployment Modules ********** */

// App Insights
module appInsights './modules/app-insights.module.bicep' = {
  name: 'app_insights'
  params: {
    location: location
    tags: tags
    workspaceName: endatixLogAnalyticsWsName
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
  }
}

// Managed VNet (private PostgreSQL + API integration) — only when no BYO VNet
module vnetModule './modules/vnet.module.bicep' = if (deployManagedVnet) {
  name: 'vnetModule'
  params: {
    location: location
    tags: tags
    vnetName: endatixVnetName
    vnetAddressPrefix: vnetAddressPrefix
    appSubnetPrefix: appSubnetPrefix
    dbSubnetPrefix: dbSubnetPrefix
    gatewaySubnetPrefix: gatewaySubnetPrefix
    vpnAddressPoolPrefix: vpnAddressPoolPrefix
    appNsgName: appNsgName
    dbNsgName: dbNsgName
    vgwName: vgwName
    vgwPublicIpName: vgwPipName
  }
}

// Endatix Hub - Static Web App (default)
module endatixHubSWA './modules/static-site.module.bicep' = if (hubDeploymentMode == 'static-site') {
  name: endatixHubName
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
    appSettings: hubInitialAppSettings
  }
}

// Endatix Hub - Web App (Node.js)
module endatixHubWebApp './modules/web-app.module.bicep' = if (hubDeploymentMode == 'web-app') {
  name: endatixHubName
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
    appSettings: hubInitialAppSettings
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
    appSettings: union(
      apiAppSettings,
      apiSerilogApplicationInsightsSettings,
      {
        ASPNETCORE_ENVIRONMENT: 'Production'
        Endatix__Storage__Providers__AzureBlob__HostName: storageHostName
        Endatix__Auth__Providers__EndatixJwt__SigningKey: endatixJwtSigningKey
        Endatix__Submissions__AccessTokenSigningKey: submissionsAccessTokenSigningKey
        Endatix__Data__SeedSampleData: true
        Endatix__Data__SeedSampleForms: true
        Endatix__Data__InitialUser__Email: initialUserEmail
        Endatix__Data__InitialUser__Password: initialUserPassword
      }
    )
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
    virtualNetworkSubnetId: enablePostgresqlPrivateNetwork ? (deployManagedVnet ? vnetModule!.outputs.appSubnetId : (apiVirtualNetworkSubnetId != '' ? apiVirtualNetworkSubnetId : '${vnetResourceId}/subnets/${apiIntegrationSubnetName}')) : ''
  }
}

// Storage blob containers and CORS with resolved Hub + API origins
module endatixStorageServices './modules/storage-blob-services.module.bicep' = {
  name: 'endatixStorageServices'
  params: {
    storageAccountName: endatixStorageAccountName
    allowedOrigins: resolvedStorageCorsOrigins
    isPrivate: storageIsPrivate
  }
}

// PostgreSQL Flexible Server
module postgresqlModule './modules/postgres.module.bicep' = {
  name: 'postgresqlModule'
  params: {
    location: location
    serverName: endatixPostgresqlServerName
    tags: tags
    postgresAdminUsername: postgresAdminUsername
    postgresAdminPassword: postgresAdminPassword
    postgresVersion: postgresqlVersion
    databaseName: postgresqlDatabaseName
    enableHighAvailability: enablePostgresqlHA
    enablePrivateNetwork: enablePostgresqlPrivateNetwork
    vnetResourceId: deployManagedVnet ? vnetModule!.outputs.vnetId : vnetResourceId
    postgresDelegatedSubnetResourceId: enablePostgresqlPrivateNetwork ? (deployManagedVnet ? vnetModule!.outputs.postgresSubnetId : '${vnetResourceId}/subnets/${postgresSubnetName}') : ''
  }
}

/* ********* Outputs ********** */
output hubBaseUrl string = hubDeploymentMode == 'static-site'
  ? 'https://${endatixHubSWA!.outputs.staticWebAppDefaultHostName}'
  : 'https://${endatixHubWebApp!.outputs.appDefaultHostName}'
output apiBaseUrl string = apiBaseUrl
output nextPublicApiUrl string = apiPublicUrl
output hubDefaultHostName string = hubDeploymentMode == 'static-site'
  ? endatixHubSWA!.outputs.staticWebAppDefaultHostName
  : endatixHubWebApp!.outputs.appDefaultHostName
output apiDefaultHostName string = endatixApi.outputs.appDefaultHostName
output resourceGroupName string = resourceGroup().name
output apiAppName string = endatixApiName
output hubAppName string = endatixHubName
