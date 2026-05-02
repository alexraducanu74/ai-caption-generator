# Azure Deployment Script for Caption Generator - PowerShell Version
# Usage: ./Deploy-Azure.ps1 -SubscriptionId "your-sub-id" -Environment "dev"

param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "caption-generator-rg",
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory = $false)]
    [string]$Environment = "dev",
    
    [Parameter(Mandatory = $false)]
    [string]$ProjectName = "caption-generator"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Helper functions
function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Yellow
}

# Check prerequisites
Write-Info "Checking prerequisites..."
$requiredTools = @("az", "docker")
foreach ($tool in $requiredTools) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        Write-Error "$tool is not installed. Please install it first."
        exit 1
    }
}
Write-Success "Prerequisites check passed"

# Set subscription
Write-Info "Setting Azure subscription..."
az account set --subscription $SubscriptionId
Write-Success "Subscription set to: $SubscriptionId"

# Create resource group
Write-Info "Creating resource group: $ResourceGroup in $Location..."
az group create `
    --name $ResourceGroup `
    --location $Location `
    --tags environment=$Environment project=$ProjectName createdDate=(Get-Date -Format "u")
Write-Success "Resource group created/updated"

# Deploy infrastructure
Write-Info "Deploying infrastructure using Bicep template..."
$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file azure/infrastructure.bicep `
    --parameters `
        location=$Location `
        environment=$Environment `
        projectName=$ProjectName `
    --output json | ConvertFrom-Json

Write-Success "Infrastructure deployed"

# Extract outputs
$registryServer = $deploymentOutput.properties.outputs.containerRegistryLoginServer.value
$registryName = $deploymentOutput.properties.outputs.containerRegistryName.value
$apiServiceUrl = $deploymentOutput.properties.outputs.apiServiceUrl.value
$aiServiceUrl = $deploymentOutput.properties.outputs.aiServiceUrl.value
$storageAccount = $deploymentOutput.properties.outputs.storageAccountName.value
$keyVaultName = $deploymentOutput.properties.outputs.keyVaultName.value

Write-Info "Deployment Outputs:"
Write-Host "  Registry Server: $registryServer"
Write-Host "  AI Service URL: $aiServiceUrl"
Write-Host "  API Service URL: $apiServiceUrl"
Write-Host "  Storage Account: $storageAccount"
Write-Host "  Key Vault: $keyVaultName"

# Login to Container Registry
Write-Info "Logging in to Azure Container Registry..."
az acr login --name $registryName
Write-Success "Logged in to ACR: $registryName"

# Build and push Docker images
Write-Info "Building and pushing Docker images to ACR..."

# Get timestamp for image tags
$timestamp = Get-Date -Format "yyyyMMddHHmmss"

# AI Service image
Write-Info "Building caption-service image..."
docker build -f caption-service/Dockerfile -t "$registryServer/caption-service:latest" -t "$registryServer/caption-service:$timestamp" .
if ($LASTEXITCODE -ne 0) { Write-Error "Failed to build caption-service image"; exit 1 }
Write-Success "caption-service image built"

Write-Info "Pushing caption-service to ACR..."
docker push "$registryServer/caption-service:latest"
docker push "$registryServer/caption-service:$timestamp"
Write-Success "caption-service pushed to ACR"

# API image
Write-Info "Building caption-api image..."
docker build -f Dockerfile.api -t "$registryServer/caption-api:latest" -t "$registryServer/caption-api:$timestamp" .
if ($LASTEXITCODE -ne 0) { Write-Error "Failed to build caption-api image"; exit 1 }
Write-Success "caption-api image built"

Write-Info "Pushing caption-api to ACR..."
docker push "$registryServer/caption-api:latest"
docker push "$registryServer/caption-api:$timestamp"
Write-Success "caption-api pushed to ACR"

# Web image
Write-Info "Building caption-web image..."
docker build -f Dockerfile.web -t "$registryServer/caption-web:latest" -t "$registryServer/caption-web:$timestamp" .
if ($LASTEXITCODE -ne 0) { Write-Error "Failed to build caption-web image"; exit 1 }
Write-Success "caption-web image built"

Write-Info "Pushing caption-web to ACR..."
docker push "$registryServer/caption-web:latest"
docker push "$registryServer/caption-web:$timestamp"
Write-Success "caption-web pushed to ACR"

# Output summary
Write-Info "Deployment completed successfully!"
Write-Host ""
Write-Host "📊 Deployment Summary:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  Location: $Location"
Write-Host "  Environment: $Environment"
Write-Host ""
Write-Host "🔗 Service URLs:" -ForegroundColor Cyan
Write-Host "  API Service: $apiServiceUrl"
Write-Host "  AI Service: $aiServiceUrl"
Write-Host ""
Write-Host "💾 Storage:" -ForegroundColor Cyan
Write-Host "  Storage Account: $storageAccount"
Write-Host "  Container: uploads"
Write-Host ""
Write-Host "🔐 Key Vault:" -ForegroundColor Cyan
Write-Host "  Key Vault: $keyVaultName"
Write-Host ""
Write-Host "📦 Container Registry:" -ForegroundColor Cyan
Write-Host "  Server: $registryServer"
Write-Host "  Images pushed:"
Write-Host "    - caption-api:latest"
Write-Host "    - caption-api:$timestamp"
Write-Host "    - caption-service:latest"
Write-Host "    - caption-service:$timestamp"
Write-Host "    - caption-web:latest"
Write-Host "    - caption-web:$timestamp"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Configure environment variables in Key Vault"
Write-Host "2. Set up Static Web App for frontend deployment"
Write-Host "3. Configure CI/CD pipeline for automated deployments"
Write-Host "4. Store secrets securely in Key Vault"
Write-Host ""

# Save deployment info to file
$deploymentInfo = @{
    SubscriptionId    = $SubscriptionId
    ResourceGroup     = $ResourceGroup
    Location          = $Location
    Environment       = $Environment
    RegistryServer    = $registryServer
    RegistryName      = $registryName
    ApiServiceUrl     = $apiServiceUrl
    AiServiceUrl      = $aiServiceUrl
    StorageAccount    = $storageAccount
    KeyVault          = $keyVaultName
    DeploymentDate    = Get-Date -Format "u"
} | ConvertTo-Json

$deploymentInfo | Out-File -FilePath "azure/deployment-info-$($Environment)-$($timestamp).json" -Encoding UTF8
Write-Success "Deployment info saved to azure/deployment-info-$($Environment)-$($timestamp).json"

