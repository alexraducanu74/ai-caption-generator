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
    ,
    [Parameter(Mandatory = $false)]
    [string]$PostgresLocation = ""
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

function New-RandomPassword {
    param([int]$Length = 24)

    if ($Length -lt 12) {
        throw "Password length must be at least 12 characters."
    }

    $lower = 'abcdefghijkmnopqrstuvwxyz'
    $upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
    $digits = '23456789'
    $special = '!@#$%_-+=' 
    $all = ($lower + $upper + $digits + $special).ToCharArray()

    $chars = New-Object System.Collections.Generic.List[char]
    foreach ($set in @($lower, $upper, $digits, $special)) {
        $chars.Add($set[(Get-Random -Minimum 0 -Maximum $set.Length)])
    }

    while ($chars.Count -lt $Length) {
        $chars.Add($all[(Get-Random -Minimum 0 -Maximum $all.Length)])
    }

    for ($i = $chars.Count - 1; $i -gt 0; $i--) {
        $j = Get-Random -Minimum 0 -Maximum ($i + 1)
        $tmp = $chars[$i]
        $chars[$i] = $chars[$j]
        $chars[$j] = $tmp
    }

    return -join $chars
}

function Get-OrCreateLocalPassword {
    param(
        [string]$ResourceGroup,
        [string]$Environment,
        [string]$SecretName
    )

    $secretDir = Join-Path $PSScriptRoot ".secrets"
    New-Item -ItemType Directory -Path $secretDir -Force | Out-Null

    $safeName = ($SecretName -replace '[^a-zA-Z0-9._-]', '_')
    $secretPath = Join-Path $secretDir "$safeName.txt"

    if (Test-Path $secretPath) {
        return (Get-Content $secretPath -Raw).Trim()
    }

    $password = New-RandomPassword -Length 24
    Set-Content -Path $secretPath -Value $password -NoNewline
    Write-Info "Created local secret file: $secretPath"
    return $password
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

# Ensure Postgres location defaults to the main location if not provided
if (-not $PostgresLocation -or $PostgresLocation -eq "") {
    $PostgresLocation = $Location
}
# Deploy infrastructure in two phases:
# 1) Deploy core infra (ACR, storage, key vault, logs, DB network) WITHOUT Container Apps so we can build/push images first
# 2) Build & push images to ACR
# 3) Re-run deployment to create Container Apps which will reference images that now exist
Write-Info "Deploying core infrastructure (without Container Apps) using Bicep template..."
$postgresAdminPassword = Get-OrCreateLocalPassword -ResourceGroup $ResourceGroup -Environment $Environment -SecretName "$ResourceGroup-$Environment-postgres-admin-password"

$deploymentRaw1 = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file azure/infrastructure.bicep `
    --parameters `
        location=$Location `
        environment=$Environment `
        projectName=$ProjectName `
        postgresAdminPassword=$postgresAdminPassword `
        postgresLocation=$PostgresLocation `
        createPostgres=false `
        createContainerApps=false `
    --only-show-errors `
    --output json 2>$null

if ($LASTEXITCODE -ne 0 -or -not $deploymentRaw1) {
    Write-Error "Core infrastructure deployment failed. Check the Azure deployment operations for details."
    exit 1
}

$deploymentOutput1 = ($deploymentRaw1 | Out-String) | ConvertFrom-Json

Write-Success "Core infrastructure deployed"

# Extract outputs from first deployment (container app URLs will be empty until second deployment)
$registryServer = $deploymentOutput1.properties.outputs.containerRegistryLoginServer.value
$registryName = $deploymentOutput1.properties.outputs.containerRegistryName.value
$storageAccount = $deploymentOutput1.properties.outputs.storageAccountName.value
$keyVaultName = $deploymentOutput1.properties.outputs.keyVaultName.value

Write-Info "Core Deployment Outputs:"
Write-Host "  Registry Server: $registryServer"
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
docker build -f caption-service/Dockerfile -t "$registryServer/caption-service:latest" -t "$registryServer/caption-service:$timestamp" caption-service
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
# Now that images are in ACR, deploy Container Apps resources by re-running the deployment with createContainerApps=true
Write-Info "Deploying Container Apps (will reference images now present in ACR)..."
$deploymentRaw2 = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file azure/infrastructure.bicep `
    --parameters `
        location=$Location `
        environment=$Environment `
        projectName=$ProjectName `
        postgresAdminPassword=$postgresAdminPassword `
        postgresLocation=$PostgresLocation `
        createPostgres=false `
        createContainerApps=true `
    --only-show-errors `
    --output json 2>$null

if ($LASTEXITCODE -ne 0 -or -not $deploymentRaw2) {
    Write-Error "Final infrastructure deployment (Container Apps) failed. Retrieving failed operations..."
    $failedOps = az deployment operation group list --resource-group $ResourceGroup --name infrastructure --query "[?properties.provisioningState=='Failed'].{resource:properties.targetResource.resourceType,name:properties.targetResource.resourceName,message:properties.statusMessage}" -o json
    Write-Host $failedOps
    exit 1
}

$deploymentOutput = ($deploymentRaw2 | Out-String) | ConvertFrom-Json

Write-Success "Full infrastructure deployed (including Container Apps)"

# Extract outputs from final deployment
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

