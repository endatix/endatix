/*
Endatix managed Virtual Network for private PostgreSQL + App Service integration.

Deploys snet-app (Microsoft.Web/serverFarms), snet-db (PostgreSQL flexible server),
and snet-pe (reserved for private endpoints).
*/

@description('Azure region for deployment')
param location string

@description('Virtual network name')
param vnetName string

@description('Azure resource tags')
param tags object

resource vnet 'Microsoft.Network/virtualNetworks@2025-05-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: ['10.70.0.0/16']
    }
  }
}

resource subnetApp 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' = {
  parent: vnet
  name: 'snet-app'
  properties: {
    addressPrefix: '10.70.0.0/24'
    delegations: [
      {
        name: 'app-service-delegation'
        properties: {
          serviceName: 'Microsoft.Web/serverFarms'
        }
      }
    ]
  }
}

resource subnetDb 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' = {
  parent: vnet
  name: 'snet-db'
  properties: {
    addressPrefix: '10.70.1.0/24'
    delegations: [
      {
        name: 'postgresql-flex-delegation'
        properties: {
          serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
        }
      }
    ]
  }
}

resource subnetPe 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' = {
  parent: vnet
  name: 'snet-pe'
  properties: {
    addressPrefix: '10.70.2.0/24'
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

output vnetId string = vnet.id
output appSubnetId string = subnetApp.id
output postgresSubnetId string = subnetDb.id
