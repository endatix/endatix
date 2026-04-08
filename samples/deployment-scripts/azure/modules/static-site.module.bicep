/*
Endatix Hub Static Web App Bicep module

This module deploys a Static Web App for the Endatix Hub frontend (Next.js) with git-based deployment.
*/

@description('Azure region for deployment')
param location string

@description('Git branch to deploy')
param branch string = 'main'

@description('Static Web App resource name')
param staticSiteName string = 'endatix-hub'

@description('GitHub repository URL for the Hub application (default: official Endatix Hub repo)')
param repositoryUrl string = 'https://github.com/endatix/endatix-hub'

@description('Azure resource tags')
param tags object

@description('Custom domain name (optional)')
param hubCustomDomainName string = ''

@description('App Insights resource ID')
param appInsightsId string

@secure()
@description('App Insights instrumentation key')
param appInsightsInstrumentationKey string

@secure()
@description('App Insights connection string')
param appInsightsConnectionString string

@description('App settings for the Static Web App environment')
param appSettings object = {}

@description('Environment variables for the Static Web App')
param environmentVariables object = {}

@description('Azure Storage account name for client-side storage access')
param storageAccountName string = ''

@secure()
@description('Azure Storage account key for client-side storage access')
param storageAccountKey string = ''

@description('Whether storage is private (no public blob access)')
param storageIsPrivate bool = false

var staticSiteTags = union(
  {
    'hidden-link: /app-insights-resource-id': appInsightsId
    'hidden-link: /app-insights-instrumentation-key': appInsightsInstrumentationKey
    'hidden-link: /app-insights-conn-string': appInsightsConnectionString
  },
  tags
)
resource static_site 'Microsoft.Web/staticSites@2025-03-01' = {
  location: location
  name: staticSiteName
  properties: {
    allowConfigFileUpdates: true
    repositoryUrl: repositoryUrl
    branch: branch
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
    enterpriseGradeCdnStatus: 'Disabled'
    stagingEnvironmentPolicy: 'Enabled'
  }
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  tags: staticSiteTags
}

resource static_site_appsettings 'Microsoft.Web/staticSites/config@2025-03-01' = {
  name: 'appsettings'
  kind: 'string'
  parent: static_site
  properties: union(
    {
      APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    },
    (storageAccountName != '') ? {
      AZURE_STORAGE_ACCOUNT_NAME: storageAccountName
      AZURE_STORAGE_ACCOUNT_KEY: storageAccountKey
      AZURE_STORAGE_IS_PRIVATE: string(storageIsPrivate)
    } : {},
    appSettings,
    environmentVariables
  )
}

resource static_site_customdomain 'Microsoft.Web/staticSites/customDomains@2025-03-01' = if (hubCustomDomainName != '') {
  parent: static_site
  name: hubCustomDomainName
}

output staticWebAppDefaultHostName string = static_site.properties.defaultHostname
output staticWebAppId string = static_site.id
output staticWebAppName string = static_site.name
