/*
Endatix App Insights Bicep module
*/

param location string
param tags object
param workspaceName string = 'endatix-appinsights-ws'
param appInsightsName string = 'endatix-appinsights'

@description('Enable automatic failure anomaly alerts (requires Microsoft.AlertsManagement provider registration)')
param enableFailureAnomalyAlerts bool = false

resource app_insights_workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      legacy: 0
      searchVersion: 1
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: json('-1')
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource app_insights 'microsoft.insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Redfield'
    Request_Source: 'IbizaWebAppExtensionCreate'
    RetentionInDays: 90
    IngestionMode: 'LogAnalytics'
    WorkspaceResourceId: app_insights_workspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    DisableIpMasking: false
  }
}

// Optional: Failure Anomaly Alert Rule (requires Microsoft.AlertsManagement provider)
resource app_insights_smart_detection 'Microsoft.Insights/actionGroups@2023-01-01' = if (enableFailureAnomalyAlerts) {
  name: '${appInsightsName}-smart-detection'
  location: 'Global'
  dependsOn: [app_insights]
  tags: tags
  properties: {
    groupShortName: 'SmartDetect'
    enabled: true
  }
}

resource app_insights_smart_alerts_rule 'microsoft.alertsManagement/smartDetectorAlertRules@2021-04-01' = if (enableFailureAnomalyAlerts) {
  name: '${appInsightsName}smart-alerts'
  location: 'global'
  tags: tags
  properties: {
    description: 'Failure Anomalies notifies you of an unusual rise in the rate of failed HTTP requests or dependency calls.'
    state: 'Enabled'
    severity: 'Sev3'
    frequency: 'PT1M'
    detector: {
      id: 'FailureAnomaliesDetector'
    }
    scope: [app_insights.id]
    actionGroups: {
      groupIds: [app_insights.id]
    }
  }
}

output appInsightsId string = app_insights.id
@secure()
output appInsightsConnectionString string = app_insights.properties.ConnectionString
@secure()
output appInsightsInstrumentationKey string = app_insights.properties.InstrumentationKey
