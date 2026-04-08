
/*
Endatix Blob Storage Bicep module

This module deploys Azure Blob Storage for storing forms and submissions.
CORS configuration is handled via app settings rather than at deployment time
to avoid circular dependencies between resources.
*/

@description('Azure region for deployment')
param location string

@description('Storage account name')
param storageAccountName string = 'endatixstorage'

@description('Azure resource tags')
param tags object

@description('Whether the storage account is private (no public blob access)')
param isPrivate bool = false

@description('Allowed origins for CORS (e.g., Hub and API hostnames)')
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
    allowBlobPublicAccess: !isPrivate
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
    cors: allowedOrigins != [] ? {
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
    } : null
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
var storageAccountKey = endatix_storage.listKeys().keys[0].value

// Outputs
output storageAccountId string = endatix_storage.id
output storageAccountName string = endatix_storage.name
output storageAccountKey string = storageAccountKey
output storageAccountBlobEndpoint string = endatix_storage.properties.primaryEndpoints.blob
output blobStorageConnectionString string = blobStorageConnectionString

