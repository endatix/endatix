@description('Web App resource name')
param webAppName string

@description('Complete app settings object to apply')
param appSettings object

resource webApp 'Microsoft.Web/sites@2025-03-01' existing = {
  name: webAppName
}

resource webAppAppSettings 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: webApp
  name: 'appsettings'
  properties: appSettings
}
