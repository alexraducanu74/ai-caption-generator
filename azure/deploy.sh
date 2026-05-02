#!/bin/bash
# Azure Deployment Script for Caption Generator

set -e

# Configuration
SUBSCRIPTION_ID=${AZURE_SUBSCRIPTION_ID}
RESOURCE_GROUP=${AZURE_RESOURCE_GROUP:-caption-generator-rg}
LOCATION=${AZURE_LOCATION:-eastus}
ENVIRONMENT=${ENVIRONMENT:-dev}
PROJECT_NAME=${PROJECT_NAME:-caption-generator}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Functions
print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ️  $1${NC}"
}

# Check prerequisites
print_info "Checking prerequisites..."
if ! command -v az &> /dev/null; then
    print_error "Azure CLI not found. Please install it first: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    print_error "Docker not found. Please install it first: https://docs.docker.com/get-docker/"
    exit 1
fi

print_success "Prerequisites check passed"

# Set subscription
print_info "Setting Azure subscription..."
az account set --subscription "$SUBSCRIPTION_ID"
print_success "Subscription set to: $SUBSCRIPTION_ID"

# Create resource group
print_info "Creating resource group: $RESOURCE_GROUP in $LOCATION..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --tags \
    environment="$ENVIRONMENT" \
    project="$PROJECT_NAME" \
    createdDate=$(date -u +'%Y-%m-%dT%H:%M:%SZ') || print_error "Failed to create resource group"
print_success "Resource group created/updated"

# Deploy infrastructure
print_info "Deploying infrastructure using Bicep template..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file azure/infrastructure.bicep \
    --parameters \
        location="$LOCATION" \
        environment="$ENVIRONMENT" \
        projectName="$PROJECT_NAME" \
    --output json)

print_success "Infrastructure deployed"

# Extract outputs
REGISTRY_SERVER=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.containerRegistryLoginServer.value')
REGISTRY_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.containerRegistryName.value')
API_SERVICE_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.apiServiceUrl.value')
AI_SERVICE_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.aiServiceUrl.value')
STORAGE_ACCOUNT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.storageAccountName.value')

print_info "Deployment Outputs:"
echo "  Registry Server: $REGISTRY_SERVER"
echo "  AI Service URL: $AI_SERVICE_URL"
echo "  API Service URL: $API_SERVICE_URL"
echo "  Storage Account: $STORAGE_ACCOUNT"

# Login to Container Registry
print_info "Logging in to Azure Container Registry..."
az acr login --name "$REGISTRY_NAME"
print_success "Logged in to ACR"

# Build and push images
print_info "Building and pushing Docker images..."

# AI Service image
print_info "Building caption-service image..."
docker build -f caption-service/Dockerfile -t "${REGISTRY_SERVER}/caption-service:latest" -t "${REGISTRY_SERVER}/caption-service:$(date +%Y%m%d%H%M%S)" .
print_success "caption-service image built"

print_info "Pushing caption-service to ACR..."
docker push "${REGISTRY_SERVER}/caption-service:latest"
docker push "${REGISTRY_SERVER}/caption-service:$(date +%Y%m%d%H%M%S)"
print_success "caption-service pushed to ACR"

# API image
print_info "Building caption-api image..."
docker build -f Dockerfile.api -t "${REGISTRY_SERVER}/caption-api:latest" -t "${REGISTRY_SERVER}/caption-api:$(date +%Y%m%d%H%M%S)" .
print_success "caption-api image built"

print_info "Pushing caption-api to ACR..."
docker push "${REGISTRY_SERVER}/caption-api:latest"
docker push "${REGISTRY_SERVER}/caption-api:$(date +%Y%m%d%H%M%S)"
print_success "caption-api pushed to ACR"

# Trigger Container App deployments
print_info "Updating Container Apps..."
# Note: In production, you might want to use Azure DevOps or GitHub Actions for CI/CD
# This is a manual deployment trigger
print_info "Container apps will be automatically updated with new images from ACR"

# Deploy Static Web App (Frontend)
print_info "Building Blazor WASM for Azure Static Web Apps..."
if [ ! -d "CaptionGenerator.Web/bin/Release/net10.0/publish/wwwroot" ]; then
    print_info "Building Blazor WASM application..."
    dotnet build CaptionGenerator.Web/CaptionGenerator.Web.csproj -c Release
    dotnet publish CaptionGenerator.Web/CaptionGenerator.Web.csproj -c Release -o CaptionGenerator.Web/bin/Release/net10.0/publish
    print_success "Blazor WASM built"
fi

# Create or update Static Web App
STATIC_APP_NAME="${PROJECT_NAME}-web-${ENVIRONMENT}"
print_info "Creating/Updating Azure Static Web App: $STATIC_APP_NAME..."

# Check if Static Web App exists
if az staticwebapp show --name "$STATIC_APP_NAME" --resource-group "$RESOURCE_GROUP" &>/dev/null; then
    print_info "Static Web App already exists"
else
    print_info "Creating new Static Web App..."
    az staticwebapp create \
        --name "$STATIC_APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --location "$LOCATION" \
        --source-path "CaptionGenerator.Web/bin/Release/net10.0/publish/wwwroot" \
        --tags environment="$ENVIRONMENT" project="$PROJECT_NAME"
fi

print_success "Static Web App ready"

# Output summary
print_info "Deployment completed successfully!"
echo ""
echo "📊 Deployment Summary:"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location: $LOCATION"
echo "  Environment: $ENVIRONMENT"
echo ""
echo "🔗 Service URLs:"
echo "  API Service: $API_SERVICE_URL"
echo "  AI Service: $AI_SERVICE_URL"
echo "  Static Web App: https://${STATIC_APP_NAME}.azurestaticapps.net"
echo ""
echo "💾 Storage:"
echo "  Storage Account: $STORAGE_ACCOUNT"
echo "  Container: uploads"
echo ""
echo "🔐 Key Vault:"
print_info "Configure secrets in Azure Key Vault as needed"
echo ""
echo "Next steps:"
echo "1. Update your frontend with the API_SERVICE_URL"
echo "2. Configure Static Web App authentication if needed"
echo "3. Set up CI/CD pipeline for automated deployments"
echo ""

