param(
    [Parameter(Mandatory=$true)]
    [string]$DockerHubUsername,

    [string]$ImageTag = "latest"
)

$ErrorActionPreference = "Stop"

Write-Host "This will push your caption-service to Docker Hub as a public image"
Write-Host "Docker Hub username: $DockerHubUsername"
Write-Host ""

# Check if logged into Docker Hub
Write-Host "Checking Docker login status..."
$loginStatus = docker info 2>&1 | Select-String "Username"
if (-not $loginStatus) {
    Write-Host "Please login to Docker Hub:"
    docker login
}

# Pull from ACR
Write-Host "`nPulling image from Azure Container Registry..."
docker pull captiongeneratoreqh42kmb646vq.azurecr.io/caption-service:latest

# Tag for Docker Hub
$dockerHubImage = "${DockerHubUsername}/caption-service:${ImageTag}"
Write-Host "`nTagging image as: $dockerHubImage"
docker tag captiongeneratoreqh42kmb646vq.azurecr.io/caption-service:latest $dockerHubImage

# Push to Docker Hub
Write-Host "`nPushing to Docker Hub (this may take a few minutes)..."
docker push $dockerHubImage

Write-Host "`n========================================="
Write-Host "SUCCESS! Image pushed to Docker Hub"
Write-Host "========================================="
Write-Host "Public Image: $dockerHubImage"
Write-Host ""
Write-Host "You can now deploy this on any GPU platform:"
Write-Host ""
Write-Host "RunPod:"
Write-Host "  docker run -d -p 8000:8000 --gpus all $dockerHubImage"
Write-Host ""
Write-Host "Vast.ai:"
Write-Host "  docker run -d -p 8000:8000 --gpus all -e MODEL_NAME=llava-hf/llava-1.5-7b-hf $dockerHubImage"
Write-Host ""
Write-Host "Lambda Labs:"
Write-Host "  docker run -d -p 8000:8000 --gpus all $dockerHubImage"
Write-Host ""
Write-Host "Hugging Face Spaces:"
Write-Host "  Use image: $dockerHubImage in your Space settings"
