/*
Managed VNet for Endatix private PostgreSQL and API VNet integration.
Includes NSGs, subnets, VPN gateway, and public IP for point-to-site VPN.
*/

param location string
param tags object
param vnetName string
param vnetAddressPrefix string
param appSubnetPrefix string
param dbSubnetPrefix string
param gatewaySubnetPrefix string
param vpnAddressPoolPrefix string
param appNsgName string
param dbNsgName string
param vgwName string
param vgwPublicIpName string

resource appNsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: appNsgName
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowPostgresToDbSubnet'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '5432'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: dbSubnetPrefix
        }
      }
      {
        name: 'AllowHttpsToStorage'
        properties: {
          priority: 110
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'Storage'
        }
      }
      {
        name: 'AllowHttpsToInternet'
        properties: {
          priority: 120
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'Internet'
        }
      }
      {
        name: 'DenyOutboundVirtualNetwork'
        properties: {
          priority: 4000
          direction: 'Outbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'VirtualNetwork'
        }
      }
    ]
  }
}

resource dbNsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: dbNsgName
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowPostgresFromAppSubnet'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '5432'
          sourceAddressPrefix: appSubnetPrefix
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'AllowPostgresFromVpnPool'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '5432'
          sourceAddressPrefix: vpnAddressPoolPrefix
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'AllowAzureLoadBalancer'
        properties: {
          priority: 3900
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'AzureLoadBalancer'
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'DenyInboundVirtualNetwork'
        properties: {
          priority: 4000
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'VirtualNetwork'
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'DenyOutboundInternet'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'Internet'
        }
      }
    ]
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'snet-app'
        properties: {
          addressPrefix: appSubnetPrefix
          networkSecurityGroup: {
            id: appNsg.id
          }
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage'
            }
          ]
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
      {
        name: 'snet-db'
        properties: {
          addressPrefix: dbSubnetPrefix
          networkSecurityGroup: {
            id: dbNsg.id
          }
          delegations: [
            {
              name: 'postgresql-delegation'
              properties: {
                serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
              }
            }
          ]
        }
      }
      {
        name: 'GatewaySubnet'
        properties: {
          addressPrefix: gatewaySubnetPrefix
        }
      }
    ]
  }
}

resource vgwPublicIp 'Microsoft.Network/publicIPAddresses@2024-05-01' = {
  name: vgwPublicIpName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  zones: [
    '1'
    '2'
    '3'
  ]
  properties: {
    publicIPAllocationMethod: 'Static'
  }
}

resource vgw 'Microsoft.Network/virtualNetworkGateways@2024-05-01' = {
  name: vgwName
  location: location
  tags: tags
  properties: {
    ipConfigurations: [
      {
        name: 'vnetGatewayConfig'
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          subnet: {
            id: '${vnet.id}/subnets/GatewaySubnet'
          }
          publicIPAddress: {
            id: vgwPublicIp.id
          }
        }
      }
    ]
    sku: {
      name: 'VpnGw2AZ'
      tier: 'VpnGw2AZ'
    }
    gatewayType: 'Vpn'
    vpnType: 'RouteBased'
    vpnClientConfiguration: {
      vpnClientAddressPool: {
        addressPrefixes: [
          vpnAddressPoolPrefix
        ]
      }
    }
  }
}

output vnetId string = vnet.id
output appSubnetId string = '${vnet.id}/subnets/snet-app'
output postgresSubnetId string = '${vnet.id}/subnets/snet-db'
