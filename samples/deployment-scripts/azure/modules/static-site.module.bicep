/*
Endatix Hub Bicep module
*/

param location string
param branch string = 'main'
param staticSiteName string = 'endatix-hub'
param repositoryUrl string = 'https://github.com/endatix/endatix-hub'
param tags object
param hubCustomDomainName string
param appInsightsId string
@secure()
param appInsightsInstrumentationKey string
@secure()
param appInsightsConnectionString string

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
    repositoryUrl: repositoryUrl
    allowConfigFileUpdates: true
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
  properties: {
    APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
  }
}

resource static_site_customdomain 'Microsoft.Web/staticSites/customDomains@2025-03-01' = if (hubCustomDomainName != ''){
  parent: static_site
  name: hubCustomDomainName
}

output staticWebAppDefaultHostName string = static_site.properties.defaultHostname
output staticWebAppId string = static_site.id
output staticWebAppName string = static_site.name
