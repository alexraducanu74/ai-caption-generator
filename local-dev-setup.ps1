# Local Development Setup Script
# This script sets up the local environment for development

param(
    [switch]$BuildImages = $false,
    [switch]$IncludeGPU = $false
)

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
$requiredTools = @("docker", "docker-compose")
foreach ($tool in $requiredTools) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        Write-Error "$tool is not installed. Please install Docker Desktop."
        exit 1
    }
}
Write-Success "All prerequisites installed"

# Create .env file if it doesn't exist
if (-not (Test-Path ".env")) {
    Write-Info "Creating .env file..."
    Copy-Item ".env.template" ".env"
    Write-Success ".env file created. Please review and update values if needed."
}

# Create necessary directories
Write-Info "Creating necessary directories..."
$directories = @(
    "CaptionGenerator.API/bin",
    "CaptionGenerator.Web/bin",
    "caption-service/__pycache__"
)
foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}
Write-Success "Directories created"

# Build Docker images if requested
if ($BuildImages) {
    Write-Info "Building Docker images..."
    
    Write-Info "Building API image..."
    docker build -f Dockerfile.api -t caption-generator-api:dev .
    Write-Success "API image built"
    
    Write-Info "Building AI Service image..."
    if ($IncludeGPU) {
        Write-Info "Building with GPU support..."
        # Would need a separate Dockerfile for GPU
    }
    docker build -f caption-service/Dockerfile -t caption-generator-ai:dev .
    Write-Success "AI Service image built"
    
    Write-Info "Building Web image..."
    docker build -f Dockerfile.web -t caption-generator-web:dev .
    Write-Success "Web image built"
}

# Start services
Write-Info "Starting services with docker-compose..."
docker-compose --env-file .env up -d

# Wait for services to be ready
Write-Info "Waiting for services to start (this may take a few minutes for AI model to load)..."
$maxAttempts = 120
$attempt = 0

while ($attempt -lt $maxAttempts) {
    $apiHealthy = $false
    $aiHealthy = $false
    $dbHealthy = $false
    
    try {
        $apiResponse = Invoke-WebRequest -Uri "http://localhost:5000/health" -ErrorAction SilentlyContinue
        if ($apiResponse.StatusCode -eq 200) { $apiHealthy = $true }
    } catch { }
    
    try {
        $aiResponse = Invoke-WebRequest -Uri "http://localhost:8000/health" -ErrorAction SilentlyContinue
        if ($aiResponse.StatusCode -eq 200) { $aiHealthy = $true }
    } catch { }
    
    try {
        $dbHealthy = docker-compose exec -T postgres pg_isready -U postgres 2>$null | Select-String "accepting" | $null
    } catch { }
    
    if ($dbHealthy -and $apiHealthy -and $aiHealthy) {
        Write-Success "All services are healthy!"
        break
    }
    
    Write-Host "." -NoNewline
    Start-Sleep -Seconds 1
    $attempt++
}

if ($attempt -ge $maxAttempts) {
    Write-Error "Services did not become healthy in time. Check logs with: docker-compose logs"
    exit 1
}

# Display access information
Write-Host ""
Write-Host "🚀 Development Environment Ready!" -ForegroundColor Cyan
Write-Host ""
Write-Host "📊 Service URLs:" -ForegroundColor Cyan
Write-Host "  Frontend:   http://localhost:3000" -ForegroundColor White
Write-Host "  API Docs:   http://localhost:5000/openapi/v1.json" -ForegroundColor White
Write-Host "  AI Service: http://localhost:8000/docs" -ForegroundColor White
Write-Host ""
Write-Host "💾 Database:" -ForegroundColor Cyan
Write-Host "  Host:     localhost" -ForegroundColor White
Write-Host "  Port:     5433" -ForegroundColor White
Write-Host "  Database: caption_generator" -ForegroundColor White
Write-Host "  User:     postgres" -ForegroundColor White
Write-Host "  Password: postgres" -ForegroundColor White
Write-Host ""
Write-Host "🔧 Useful Commands:" -ForegroundColor Cyan
Write-Host "  View logs:        docker-compose logs -f" -ForegroundColor White
Write-Host "  Restart services: docker-compose restart" -ForegroundColor White
Write-Host "  Stop services:    docker-compose down" -ForegroundColor White
Write-Host "  Rebuild images:   docker-compose build --no-cache" -ForegroundColor White
Write-Host ""
Write-Host "📚 Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Open browser to http://localhost:3000" -ForegroundColor White
Write-Host "  2. Register a new account" -ForegroundColor White
Write-Host "  3. Upload an image to test caption generation" -ForegroundColor White
Write-Host "  4. Check API docs at http://localhost:5000/openapi/v1.json" -ForegroundColor White
Write-Host ""

Write-Success "Setup complete!"

