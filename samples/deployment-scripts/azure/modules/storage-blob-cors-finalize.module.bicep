@description('Existing storage account name')
param storageAccountName string

@description('Allowed CORS origins (include scheme, e.g. https://myapp.azurestaticapps.net)')
param allowedOrigins array

resource storage 'Microsoft.Storage/storageAccounts@2025-06-01' existing = {
  name: storageAccountName
}

// Re-apply default blob service with same retention as initial storage module so CORS can use resolved Hub/API URLs.
resource blobDefault 'Microsoft.Storage/storageAccounts/blobServices@2025-06-01' = {
  parent: storage
  name: 'default'
  properties: {
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    cors: length(allowedOrigins) > 0
      ? {
          corsRules: [
            {
              allowedOrigins: allowedOrigins
              allowedMethods: [
                'DELETE'
                'GET'
                'HEAD'
                'MERGE'
                'POST'
                'OPTIONS'
                'PUT'
              ]
              maxAgeInSeconds: 86400
              exposedHeaders: ['*']
              allowedHeaders: ['*']
            }
          ]
        }
      : null
    deleteRetentionPolicy: {
      allowPermanentDelete: false
      enabled: true
      days: 7
    }
  }
}
