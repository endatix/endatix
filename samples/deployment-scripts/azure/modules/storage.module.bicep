
/*
Endatix Hub Bicep module
*/

param location string
param storageAccountName string = 'endatixstorage'
param tags object
param allowedOrigins array = []

resource endatix_storage 'Microsoft.Storage/storageAccounts@2025-06-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    dnsEndpointType: 'Standard'
    defaultToOAuthAuthentication: false
    publicNetworkAccess: 'Enabled'
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: true
    allowSharedKeyAccess: true
    networkAcls: {
      ipv6Rules: []
      bypass: 'AzureServices'
      virtualNetworkRules: []
      ipRules: []
      defaultAction: 'Allow'
    }
    supportsHttpsTrafficOnly: true
    encryption: {
      requireInfrastructureEncryption: false
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

resource endatix_storage_default 'Microsoft.Storage/storageAccounts/blobServices@2025-06-01' = {
  parent: endatix_storage
  name: 'default'
  properties: {
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    cors: {
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
          exposedHeaders: [
            '*'
          ]
          allowedHeaders: [
            '*'
          ]
        }
      ]
    }
    deleteRetentionPolicy: {
      allowPermanentDelete: false
      enabled: true
      days: 7
    }
  }
}

resource endatix_storage_default_content 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' = {
  parent: endatix_storage_default
  name: 'content'
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'Blob'
  }
}

resource endatix_storage_default_user_files 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' = {
  parent: endatix_storage_default
  name: 'user-files'
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'Blob'
  }
}

// Determine our connection string
var blobStorageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${endatix_storage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${endatix_storage.listKeys().keys[0].value}'

// Output our variable
output storageAccountId string = endatix_storage.id
output storageAccountName string = endatix_storage.name
output storageAccountBlobEndpoint string = endatix_storage.properties.primaryEndpoints.blob
output blobStorageConnectionString string = blobStorageConnectionString

