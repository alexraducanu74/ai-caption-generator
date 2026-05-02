param location string = resourceGroup().location
param siteName string = 'caption-generator-web'
param skuName string = 'Standard'
param repositoryUrl string = ''
param repositoryBranch string = 'main'
param repositoryToken string = ''

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: siteName
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    repositoryUrl: repositoryUrl
    branch: repositoryBranch
    buildProperties: {
      appLocation: 'CaptionGenerator.Web'
      apiLocation: 'api'
      outputLocation: 'bin/Release/net10.0/publish/wwwroot'
      appBuildCommand: 'dotnet build'
      apiBuildCommand: ''
    }
    basicAuth: {
      environments: 'production,staging'
      secretsKeyVaultUri: ''
    }
  }
}

// Add custom domain (optional)
resource customDomain 'Microsoft.Web/staticSites/customDomains@2023-01-01' = {
  parent: staticWebApp
  name: 'www'
  properties: {
    validationMethod: 'cname'
  }
}

output deploymentToken string = staticWebApp.listSecrets().properties[0].value
output defaultHostname string = staticWebApp.properties.defaultHostname

