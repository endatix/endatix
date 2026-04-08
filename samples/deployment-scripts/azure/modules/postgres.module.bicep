/*
Endatix PostgreSQL Flexible Server Bicep module

This module deploys a PostgreSQL Flexible Server with database and security configurations.
*/

param location string
@description('Resource prefix for naming resources')
param resource_prefix string = 'temp-'
param tags object

@secure()
@description('PostgreSQL administrator username')
param postgres_admin_username string

@secure()
@description('PostgreSQL administrator password')
param postgres_admin_password string

@description('PostgreSQL version')
param postgresVersion string = '16'

@description('Database size in GB')
param storageSizeGB int = 48

@description('IOPS for storage')
param storageIops int = 3000

@description('Database name')
param databaseName string = 'endatix-db'

@description('Enable high availability')
param enableHighAvailability bool = false

@description('Backup retention days')
param backupRetentionDays int = 10

// PostgreSQL Flexible Server
resource postgresql 'Microsoft.DBforPostgreSQL/flexibleServers@2026-01-01-preview' = {
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
      iops: storageIops
      throughput: 125
      storageSizeGB: storageSizeGB
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
    version: postgresVersion
    administratorLogin: postgres_admin_username
    administratorLoginPassword: postgres_admin_password
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: enableHighAvailability ? 'ZoneRedundant' : 'Disabled'
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

// Advanced threat protection
resource advancedThreatProtection 'Microsoft.DBforPostgreSQL/flexibleServers/advancedThreatProtectionSettings@2026-01-01-preview' = {
  parent: postgresql
  name: 'Default'
  properties: {
    state: 'Disabled'
  }
}

// Require SSL
resource requireSecureTransport 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2026-01-01-preview' = {
  parent: postgresql
  name: 'require_secure_transport'
  properties: {
    value: 'ON'
    source: 'user-override'
  }
}

// Create database
resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2026-01-01-preview' = {
  parent: postgresql
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Firewall rule to allow Azure services
resource firewallRule 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2026-01-01-preview' = {
  parent: postgresql
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Outputs
output postgresqlId string = postgresql.id
output postgresqlFqdn string = postgresql.properties.fullyQualifiedDomainName
output databaseName string = databaseName
output postgresqlPort int = 5432

@secure()
output postgresqlConnectionString string = 'Server=${postgresql.properties.fullyQualifiedDomainName};Port=5432;Database=${databaseName};User Id=${postgres_admin_username};Password=${postgres_admin_password};SSL Mode=Require;'
