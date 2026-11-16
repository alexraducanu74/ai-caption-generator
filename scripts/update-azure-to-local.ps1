param(
    [Parameter(Mandatory=$false)]
    [string]$NgrokUrl,
    [string]$ResourceGroup,
    [string]$ContainerAppName
)

$ErrorActionPreference = "Continue"

Write-Host "=== Update Azure API to Use Local AI Service ===" -ForegroundColor Cyan
Write-Host ""

# Auto-detect ngrok URL if not provided
if (-not $NgrokUrl) {
    Write-Host "Detecting ngrok URL..."
    try {
        $ngrokApi = Invoke-RestMethod -Uri "http://localhost:4040/api/tunnels"
        $NgrokUrl = $ngrokApi.tunnels[0].public_url

        if ($NgrokUrl) {
            Write-Host "Found ngrok URL: $NgrokUrl" -ForegroundColor Green
        } else {
            Write-Host "Ngrok is not running. Please start it first:" -ForegroundColor Yellow
            Write-Host "  C:\ngrok\ngrok.exe http 8000"
            exit 1
        }
    } catch {
        Write-Host "Could not detect ngrok URL. Is ngrok running?" -ForegroundColor Yellow
        Write-Host "Check http://localhost:4040 or start with:" -ForegroundColor Yellow
        Write-Host "  C:\ngrok\ngrok.exe http 8000"
        exit 1
    }
}

# Validate URL
if (-not $NgrokUrl.StartsWith("http")) {
    Write-Error "Invalid ngrok URL. Must start with http:// or https://"
    exit 1
}

Write-Host ""
Write-Host "Ngrok URL: $NgrokUrl" -ForegroundColor Cyan

# Auto-detect Azure resources if not provided
if (-not $ResourceGroup -or -not $ContainerAppName) {
    Write-Host ""
    Write-Host "Finding Azure Container Apps..."

    $appsJson = az containerapp list --query "[?contains(name, 'api')]" -o json
    $containerApps = $appsJson | ConvertFrom-Json

    if (-not $containerApps -or $containerApps.Count -eq 0) {
        Write-Host "No container apps found. Specify manually:" -ForegroundColor Yellow
        $ResourceGroup = Read-Host "Enter Resource Group name"
        $ContainerAppName = Read-Host "Enter Container App name"
    }
    elseif ($containerApps.Count -eq 1) {
        $ResourceGroup = $containerApps[0].resourceGroup
        $ContainerAppName = $containerApps[0].name
        Write-Host "Found: $ContainerAppName in $ResourceGroup" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "Multiple container apps found:" -ForegroundColor Yellow
        foreach ($app in $containerApps) {
            Write-Host "  - $($app.name) in $($app.resourceGroup)"
        }
        Write-Host ""
        $ContainerAppName = Read-Host "Enter Container App name"
        $ResourceGroup = Read-Host "Enter Resource Group name"
    }
}

Write-Host ""
Write-Host "Updating Azure configuration..." -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  Container App: $ContainerAppName"
Write-Host "  AI Service URL: $NgrokUrl"
Write-Host ""

# Update the container app
Write-Host "Running update command..."
az containerapp update --name $ContainerAppName --resource-group $ResourceGroup --set-env-vars "CaptionGeneration__BaseUrl=$NgrokUrl" --output none

if ($LASTEXITCODE -eq 0) {
    Write-Host "Update successful!" -ForegroundColor Green

    # Verify the configuration
    Write-Host ""
    Write-Host "Verifying configuration..."

    $envVars = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query "properties.template.containers[0].env[?name=='CaptionGeneration__BaseUrl'].value" -o tsv

    if ($envVars -eq $NgrokUrl) {
        Write-Host "Configuration verified!" -ForegroundColor Green
    } else {
        Write-Host "Current value: $envVars" -ForegroundColor Yellow
    }
}
else {
    Write-Error "Failed to update Azure"
    exit 1
}

# Get the web app URL
Write-Host ""
Write-Host "Getting your web app URL..."
$webApp = az containerapp list --resource-group $ResourceGroup --query "[?contains(name, 'web')].properties.configuration.ingress.fqdn" -o tsv

if ($webApp) {
    Write-Host ""
    Write-Host "=== SUCCESS ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your Azure web app is now using your local AI service!" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Web App URL: https://$webApp" -ForegroundColor Cyan
    Write-Host "Local AI Service: http://localhost:8000"
    Write-Host "Ngrok Tunnel: $NgrokUrl"
    Write-Host "Ngrok Dashboard: http://localhost:4040"
    Write-Host ""
    Write-Host "Test your app at: https://$webApp" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Monitor requests: http://localhost:4040" -ForegroundColor Yellow
}
else {
    Write-Host ""
    Write-Host "=== SUCCESS ===" -ForegroundColor Green
    Write-Host "Azure API configured to use: $NgrokUrl"
}

Write-Host ""
Write-Host "IMPORTANT:" -ForegroundColor Yellow
Write-Host "- Keep your local AI service running (Docker)"
Write-Host "- Keep ngrok running (C:\ngrok\ngrok.exe http 8000)"
Write-Host "- If ngrok URL changes, run this script again"
Write-Host ""
