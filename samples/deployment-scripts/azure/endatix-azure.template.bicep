/* ********* Parameters ********** */
param resource_prefix string = 'temp-'
param location string = resourceGroup().location
param environment string = 'dev'
param branch string = 'main'
param tags object = {
  environment: environment
}
// Set a resource prefix if needed, e.g. 'ci-' or 'uat-'
var endatixAppInsightsName string = '${resource_prefix}endatix-appinsights'
var endatixHubStaticSiteName string = '${resource_prefix}endatix-hub'
var endatixApiName string = '${resource_prefix}endatix-api'
var endatixServicePlanName string = '${resource_prefix}endatix-serviceplan'
var endatixStorageAccountName string = '${replace(resource_prefix, '-', '')}endatixstorage' // storage account names use string that is storagename compliant [a-z0-9] e.g. only letters and numbers, no dashes

@secure()
param postgres_admin_username string

@secure()
param postgres_admin_password string

/* ********* End of Parameters ********** */

// App Insights
module appInsights './modules/app-insights.module.bicep' = {
  name: 'app_insights'
  params: {
    location: location
    tags: tags
    workspaceName: '${resource_prefix}endatix-appinsights-ws'
    appInsightsName: endatixAppInsightsName
  }
}

resource endatix_app_insights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: endatixAppInsightsName
}

// Endatix Hub
module endatixHub './modules/static-site.module.bicep' = {
  name: '${resource_prefix}endatix-hub'
  params: {
    location: location
    branch: branch
    staticSiteName: endatixHubStaticSiteName
    tags: tags
    hubCustomDomainName: ''
    appInsightsId: appInsights.outputs.appInsightsId
    appInsightsConnectionString: endatix_app_insights.properties.ConnectionString
    appInsightsInstrumentationKey: endatix_app_insights.properties.InstrumentationKey
  }
}

// Endatix API
module endatixApi './modules/endatix-api.module.bicep' = {
  name: 'endatixApiModule'
  params: {
    location: location
    tags: tags
    webAppName: endatixApiName
    webAppServicePlanName: endatixServicePlanName
    appInsightsId: appInsights.outputs.appInsightsId
    appInsightsConnectionString: endatix_app_insights.properties.ConnectionString
    appInsightsInstrumentationKey: endatix_app_insights.properties.InstrumentationKey
  }
}

// BLOB Storage
module endatixStorage './modules/storage.module.bicep' = {
  name: 'endatixStorageModule'
  params: {
    location: location
    storageAccountName: endatixStorageAccountName
    tags: tags
    allowedOrigins: [
      endatixHub.outputs.staticWebAppDefaultHostName
      endatixApi.outputs.endatixAppDefaultHostName
    ]
  }
}

// PostgreSQL Flexible Server
resource endatix_postgresql 'Microsoft.DBforPostgreSQL/flexibleServers@2026-01-01-preview' = {
  name: '${resource_prefix}endatix-postgresql'
  location: location
  tags: tags
  sku: {
    name: 'Standard_D2ds_v5'
    tier: 'GeneralPurpose'
  }
  properties: {
    dataEncryption: {
      type: 'SystemManaged'
    }
    replica: {
      role: 'Primary'
    }
    storage: {
      type: 'PremiumV2_LRS'
      iops: 3000
      throughput: 125
      storageSizeGB: 48
      autoGrow: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
      tenantId: tenant().tenantId
    }
    version: '16'
    administratorLogin: postgres_admin_username
    administratorLoginPassword: postgres_admin_password
    backup: {
      backupRetentionDays: 10
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    maintenanceWindow: {
      customWindow: 'Disabled'
      dayOfWeek: 0
      startHour: 0
      startMinute: 0
    }
    replicationRole: 'Primary'
  }
}

resource endatix_postgresql_Default 'Microsoft.DBforPostgreSQL/flexibleServers/advancedThreatProtectionSettings@2026-01-01-preview' = {
  parent: endatix_postgresql
  name: 'Default'
  properties: {
    state: 'Disabled'
  }
}

resource endatix_postgresql_require_secure_transport 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2026-01-01-preview' = {
  parent: endatix_postgresql
  name: 'require_secure_transport'
  properties: {
    value: 'ON'
    source: 'user-override'
  }
}

resource endatix_postgresql_endatix_db 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2026-01-01-preview' = {
  parent: endatix_postgresql
  name: 'endatix-db'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource endatix_postgresql_AllowAllAzureServicesAndResourcesWithinAzureIps_2025_1_10_22_12_6 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2026-01-01-preview' = {
  parent: endatix_postgresql
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps_2025-1-10_22-12-6'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}
