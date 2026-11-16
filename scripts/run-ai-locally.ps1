param(
    [switch]$UseDocker,
    [switch]$SkipNgrok,
    [int]$Port = 8000
)

$ErrorActionPreference = "Continue"

Write-Host "=== Local AI Service Setup ===" -ForegroundColor Cyan
Write-Host ""

# Check for NVIDIA GPU
$hasGPU = $false
try {
    nvidia-smi 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GPU detected!" -ForegroundColor Green
        nvidia-smi --query-gpu=name --format=csv,noheader
        $hasGPU = $true
    }
} catch {
    Write-Host "No NVIDIA GPU - using CPU" -ForegroundColor Yellow
}

if ($UseDocker) {
    Write-Host "`nStarting AI service in Docker..."

    docker stop caption-service 2>$null
    docker rm caption-service 2>$null

    $dockerCmd = "docker run -d --name caption-service -p ${Port}:8000 -e MODEL_NAME=llava-hf/llava-1.5-7b-hf -e CAPTION_MAX_LENGTH=280"

    if ($hasGPU) {
        $dockerCmd += " --gpus all"
        Write-Host "Running with GPU..." -ForegroundColor Green
    } else {
        Write-Host "Running with CPU..." -ForegroundColor Yellow
    }

    $dockerCmd += " captiongeneratoreqh42kmb646vq.azurecr.io/caption-service:latest"

    Invoke-Expression $dockerCmd

    Write-Host "Container started! Waiting..."
    Start-Sleep -Seconds 10

} else {
    Write-Host "`nStarting with Python..."

    if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
        Write-Error "Python not found. Use -UseDocker instead"
    }

    Set-Location caption-service

    if ($hasGPU) {
        pip install torch==2.5.1 torchvision==0.20.1 --index-url https://download.pytorch.org/whl/cu124
    } else {
        pip install torch==2.5.1 torchvision==0.20.1
    }

    pip install -r requirements.txt

    Start-Process python -ArgumentList "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", $Port -NoNewWindow
    Start-Sleep -Seconds 5
    Set-Location ..
}

Write-Host "`nTesting service..."
Start-Sleep -Seconds 3

try {
    $health = Invoke-RestMethod -Uri "http://localhost:${Port}/health"
    Write-Host "Service running!" -ForegroundColor Green
    Write-Host "  Status: $($health.status)"
    Write-Host "  Model: $($health.model)"
    Write-Host "  Device: $($health.device)"
} catch {
    Write-Warning "Service not responding yet. Check http://localhost:${Port}/health"
}

if (-not $SkipNgrok) {
    Write-Host "`n=== Setting up Ngrok ===" -ForegroundColor Cyan

    if (-not (Get-Command ngrok -ErrorAction SilentlyContinue)) {
        Write-Host ""
        Write-Host "Ngrok not found. Install it:" -ForegroundColor Yellow
        Write-Host "1. Download from https://ngrok.com/download"
        Write-Host "2. Extract to C:\ngrok\"
        Write-Host "3. Add C:\ngrok to PATH"
        Write-Host "4. Run: ngrok config add-authtoken YOUR_TOKEN"
        Write-Host ""
        Write-Host "Then run: ngrok http $Port"
        exit 0
    }

    Write-Host "Starting ngrok tunnel..."
    Start-Process ngrok -ArgumentList "http", $Port
    Start-Sleep -Seconds 4

    try {
        $ngrokApi = Invoke-RestMethod -Uri "http://localhost:4040/api/tunnels"
        $publicUrl = $ngrokApi.tunnels[0].public_url

        Write-Host ""
        Write-Host "=== SUCCESS ===" -ForegroundColor Green
        Write-Host ""
        Write-Host "Local URL: http://localhost:${Port}" -ForegroundColor Cyan
        Write-Host "Public URL: $publicUrl" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Update Azure with:" -ForegroundColor Yellow
        Write-Host "  .\scripts\update-azure-to-local.ps1"
        Write-Host ""
        Write-Host "Ngrok dashboard: http://localhost:4040"

    } catch {
        Write-Warning "Check ngrok dashboard at http://localhost:4040"
    }
}

Write-Host ""
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

if ($UseDocker) {
    docker logs -f caption-service
} else {
    pause
}
