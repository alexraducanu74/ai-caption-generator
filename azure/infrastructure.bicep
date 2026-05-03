param location string = resourceGroup().location
param environment string = 'dev'
param projectName string = 'caption-generator'

@secure()
param postgresAdminPassword string

param createContainerApps bool = true
param postgresLocation string = location
param createPostgres bool = true

var suffix = uniqueString(resourceGroup().id)
var tags = {
  environment: environment
  project: projectName
}

// ============ Container Registry ============
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: '${replace(projectName, '-', '')}${suffix}'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
  }
}

// ============ Log Analytics Workspace ============
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-12-01-preview' = {
  name: '${projectName}-logs-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ============ Key Vault ============
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${replace(projectName, '-', '')}${substring(suffix, 0, 8)}'
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enableSoftDelete: true
  }
}

// ============ Azure Container Apps Environment ============
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${projectName}-env-${suffix}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

// ============ Azure Database for PostgreSQL ============
resource sqlServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = if (createPostgres) {
  name: '${projectName}-db-${toLower(substring(replace(postgresLocation, ' ', ''), 0, 4))}-${suffix}'
  location: postgresLocation
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: 'pgadmin'
    administratorLoginPassword: postgresAdminPassword
    version: '15'
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      delegatedSubnetResourceId: ''
      privateDnsZoneArmResourceId: ''
    }
  }
}

// PostgreSQL Database
resource captionGeneratorDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = if (createPostgres) {
  parent: sqlServer
  name: 'caption_generator'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// PostgreSQL Firewall Rule (allow all Azure services)
resource sqlFirewallRule 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = if (createPostgres) {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ============ Azure Blob Storage ============
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${replace(projectName, '-', '')}${substring(suffix, 0, 8)}'
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// Blob Storage Container for uploads
resource uploadContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/uploads'
  properties: {
  }
}

// ============ Container Apps - AI Service ============
resource aiServiceContainerApp 'Microsoft.App/containerApps@2023-05-01' = if (createContainerApps) {
  name: '${projectName}-ai'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8000
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.name
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'caption-service'
          image: '${containerRegistry.properties.loginServer}/caption-service:latest'
           resources: {
             cpu: 2
             memory: '4Gi'
           }
          env: [
            {
              name: 'MODEL_NAME'
              value: 'llava-hf/llava-1.5-7b-hf'
            }
            {
              name: 'CAPTION_MAX_LENGTH'
              value: '280'
            }
            {
              name: 'HF_HOME'
              value: '/app/.cache/huggingface'
            }
          ]
           probes: [
             {
               type: 'liveness'
               httpGet: {
                 path: '/health'
                 port: 8000
               }
               initialDelaySeconds: 60
               periodSeconds: 30
               failureThreshold: 10
             }
           ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
}

// ============ Container Apps - API Service ============
resource apiContainerApp 'Microsoft.App/containerApps@2023-05-01' = if (createContainerApps) {
  name: '${projectName}-api'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 5000
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.name
          passwordSecretRef: 'registry-password'
        }
      ]
       secrets: concat(
         [
           {
             name: 'registry-password'
             value: containerRegistry.listCredentials().passwords[0].value
           }
         ],
         createPostgres ? [
           {
             name: 'db-connection'
             value: 'Host=${sqlServer.properties.fullyQualifiedDomainName};Port=5432;Database=caption_generator;Username=pgadmin;Password=${postgresAdminPassword};SslMode=Require;'
           }
         ] : []
       )
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${containerRegistry.properties.loginServer}/caption-api:latest'
           resources: {
             cpu: 1
             memory: '2Gi'
           }
           env: concat(
             [
               {
                 name: 'ASPNETCORE_ENVIRONMENT'
                 value: environment
               }
               {
                 name: 'CaptionGeneration__BaseUrl'
                 value: 'https://${aiServiceContainerApp.properties.configuration.ingress.fqdn}'
               }
               {
                 name: 'Jwt__Key'
                 value: 'your-secure-jwt-key-${suffix}'
               }
               {
                 name: 'Jwt__Issuer'
                 value: 'CaptionGenerator.API'
               }
               {
                 name: 'Jwt__Audience'
                 value: 'CaptionGenerator.Web'
               }
               {
                 name: 'AzureBlobStorage__ConnectionString'
                 value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
               }
               {
                 name: 'AzureBlobStorage__ContainerName'
                 value: 'uploads'
               }
             ],
             createPostgres ? [
               {
                 name: 'ConnectionStrings__DefaultConnection'
                 secretRef: 'db-connection'
               }
             ] : []
           )
          probes: [
            {
              type: 'liveness'
              httpGet: {
                path: '/health'
                port: 5000
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
   }
}

// ============ Container Apps - Web Service ============
resource webContainerApp 'Microsoft.App/containerApps@2023-05-01' = if (createContainerApps) {
  name: '${projectName}-web'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.name
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${containerRegistry.properties.loginServer}/caption-web:latest'
          resources: {
            cpu: 1
            memory: '2Gi'
          }
           env: [
             {
               name: 'ASPNETCORE_ENVIRONMENT'
               value: environment
             }
             {
               name: 'ApiBaseUrl'
               value: 'https://${apiContainerApp.properties.configuration.ingress.fqdn}/'
             }
           ]
          probes: [
            {
              type: 'liveness'
              httpGet: {
                path: '/'
                port: 80
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
}

// ============ Outputs ============
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output containerRegistryName string = containerRegistry.name
output aiServiceUrl string = createContainerApps ? 'https://${aiServiceContainerApp.properties.configuration.ingress.fqdn}' : ''
output apiServiceUrl string = createContainerApps ? 'https://${apiContainerApp.properties.configuration.ingress.fqdn}' : ''
output webServiceUrl string = createContainerApps ? 'https://${webContainerApp.properties.configuration.ingress.fqdn}' : ''
output databaseServer string = createPostgres ? sqlServer.properties.fullyQualifiedDomainName : ''
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name





















