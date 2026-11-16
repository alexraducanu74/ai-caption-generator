#!/usr/bin/env powershell
# Diagnostic script to check and fix timeout issues

param(
    [switch]$Rebuild,
    [switch]$ResetAll
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "         Caption Generator - Timeout Diagnostic Tool" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# 1. Check Docker availability
Write-Host "1️⃣  Checking Docker..." -ForegroundColor Yellow
try {
    $dockerVersion = docker --version
    Write-Host "✅ Docker: $dockerVersion" -ForegroundColor Green
}
catch {
    Write-Host "❌ Docker not found!" -ForegroundColor Red
    exit 1
}

# 2. Check container status
Write-Host ""
Write-Host "2️⃣  Container Status:" -ForegroundColor Yellow
docker ps --format "table {{.Names}}\t{{.Status}}" | Where-Object { $_ -match "caption|postgres|web" }

# 3. Check AI Service health
Write-Host ""
Write-Host "3️⃣  AI Service Health:" -ForegroundColor Yellow
try {
    $aiHealth = Invoke-WebRequest -Uri "http://localhost:8000/health" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
    if ($aiHealth.StatusCode -eq 200) {
        $json = $aiHealth.Content | ConvertFrom-Json
        Write-Host "✅ AI Service Status: $($json.status)" -ForegroundColor Green
        if ($json.status -eq "ready") {
            Write-Host "✅ Model is READY" -ForegroundColor Green
        }
        else {
            Write-Host "⏳ Model is still loading... (Current: $($json.status))" -ForegroundColor Yellow
        }
    }
}
catch {
    Write-Host "❌ AI Service unreachable" -ForegroundColor Red
}

# 4. Check API health
Write-Host ""
Write-Host "4️⃣  API Health:" -ForegroundColor Yellow
try {
    $apiHealth = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
    if ($apiHealth.StatusCode -eq 200) {
        Write-Host "✅ API is healthy" -ForegroundColor Green
    }
}
catch {
    Write-Host "❌ API unreachable" -ForegroundColor Red
}

# 5. Check Frontend
Write-Host ""
Write-Host "5️⃣  Frontend Health:" -ForegroundColor Yellow
try {
    $webHealth = Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
    if ($webHealth.StatusCode -eq 200) {
        Write-Host "✅ Frontend is ready" -ForegroundColor Green
    }
}
catch {
    Write-Host "❌ Frontend unreachable" -ForegroundColor Red
}

# 6. Show last AI logs
Write-Host ""
Write-Host "6️⃣  AI Service Recent Logs:" -ForegroundColor Yellow
docker logs --tail 10 caption-generator-caption 2>&1 | Select-Object -Last 10

# 7. Recommendation
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "                      Recommendations" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

$aiStatus = (Invoke-WebRequest -Uri "http://localhost:8000/health" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue).Content | ConvertFrom-Json

if ($aiStatus.status -ne "ready") {
    Write-Host ""
    Write-Host "⏳ AI Model Still Loading" -ForegroundColor Yellow
    Write-Host "   Action: Wait 2-3 minutes for model to fully load on first startup" -ForegroundColor White
    Write-Host "   Monitor: docker logs -f caption-generator-caption" -ForegroundColor Gray
    Write-Host "   Then retry creating a post"
}
else {
    Write-Host ""
    Write-Host "✅ System Ready!" -ForegroundColor Green
    Write-Host "   Model is loaded and ready for use" -ForegroundColor White
    Write-Host "   If still getting timeouts, try: $PSScriptRoot/Diagnostic-Timeout.ps1 -Rebuild" -ForegroundColor Gray
}

# Options
if ($Rebuild) {
    Write-Host ""
    Write-Host " Rebuilding API (increased timeout to 15 minutes)..." -ForegroundColor Yellow
    Push-Location $(Split-Path $PROFILE).TrimEnd('\Microsoft.PowerShell_profile.ps1')
    docker-compose build --no-cache api
    docker-compose restart api
    Write-Host "✅ Rebuild complete. Waiting for API to start..." -ForegroundColor Green
    Start-Sleep 10
    Pop-Location
}

if ($ResetAll) {
    Write-Host ""
    Write-Host " Performing Full Reset..." -ForegroundColor Yellow
    docker-compose down -v
    docker system prune -f
    Write-Host "✅ Cleanup complete. Starting fresh..." -ForegroundColor Green
    docker-compose up -d --build
    Write-Host "⏳ Waiting for services to start..." -ForegroundColor Yellow
    Start-Sleep 30
    Write-Host "✅ Services started. Model may take 2-3 minutes to load." -ForegroundColor Green
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "                    Access Services:" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Frontend:   http://localhost:3000" -ForegroundColor Cyan
Write-Host "API:        http://localhost:5000" -ForegroundColor Cyan
Write-Host "AI Service: http://localhost:8000" -ForegroundColor Cyan
Write-Host ""
